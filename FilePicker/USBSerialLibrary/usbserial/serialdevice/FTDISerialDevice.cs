using System;

using Android.Hardware.Usb;
using Android.Util;
using com.felhr.utils;

namespace com.felhr.usbserial
{
    public class FTDISerialDevice : UsbSerialDevice
    {
        private const string CLASS_ID = nameof(FTDISerialDevice);

        private const int FTDI_SIO_RESET = 0;
        private const int FTDI_SIO_MODEM_CTRL = 1;
        private const int FTDI_SIO_SET_FLOW_CTRL = 2;
        private const int FTDI_SIO_SET_BAUD_RATE = 3;
        private const int FTDI_SIO_SET_DATA = 4;

        private const int FTDI_REQTYPE_HOST2DEVICE = 0x40;

        /**
         *  RTS and DTR values obtained from FreeBSD FTDI driver
         *  https://github.com/freebsd/freebsd/blob/70b396ca9c54a94c3fad73c3ceb0a76dffbde635/sys/dev/usb/serial/uftdi_reg.h
         */
        private const int FTDI_SIO_SET_DTR_MASK = 0x1;
        private const int FTDI_SIO_SET_DTR_HIGH = 1 | (FTDI_SIO_SET_DTR_MASK << 8);
        private const int FTDI_SIO_SET_DTR_LOW = 0 | (FTDI_SIO_SET_DTR_MASK << 8);
        private const int FTDI_SIO_SET_RTS_MASK = 0x2;
        private const int FTDI_SIO_SET_RTS_HIGH = 2 | (FTDI_SIO_SET_RTS_MASK << 8);
        private const int FTDI_SIO_SET_RTS_LOW = 0 | (FTDI_SIO_SET_RTS_MASK << 8);

        public const int USB_TYPE_STANDARD = 0x00 << 5;
        public const int USB_TYPE_CLASS = 0x00 << 5;
        public const int USB_TYPE_VENDOR = 0x00 << 5;
        public const int USB_TYPE_RESERVED = 0x00 << 5;

        public const int USB_RECIP_DEVICE = 0x00;
        public const int USB_RECIP_INTERFACE = 0x01;
        public const int USB_RECIP_ENDPOINT = 0x02;
        public const int USB_RECIP_OTHER = 0x03;

        public const int USB_ENDPOINT_IN = 0x80;
        public const int USB_ENDPOINT_OUT = 0x00;

        private const int SIO_RESET_PURGE_RX = 1;
        private const int SIO_RESET_PURGE_TX = 2;

        public const int FTDI_DEVICE_OUT_REQTYPE =
            UsbConstants.UsbTypeVendor | USB_RECIP_DEVICE | USB_ENDPOINT_OUT;

        public const int FTDI_DEVICE_IN_REQTYPE =
            UsbConstants.UsbTypeVendor | USB_RECIP_DEVICE | USB_ENDPOINT_IN;

        public const int FTDI_BAUDRATE_300 = 0x2710;
        public const int FTDI_BAUDRATE_600 = 0x1388;
        public const int FTDI_BAUDRATE_1200 = 0x09c4;
        public const int FTDI_BAUDRATE_2400 = 0x04e2;
        public const int FTDI_BAUDRATE_4800 = 0x0271;
        public const int FTDI_BAUDRATE_9600 = 0x4138;
        public const int FTDI_BAUDRATE_19200 = 0x809c;
        public const int FTDI_BAUDRATE_38400 = 0xc04e;
        public const int FTDI_BAUDRATE_57600 = 0x0034;
        public const int FTDI_BAUDRATE_115200 = 0x001a;
        public const int FTDI_BAUDRATE_230400 = 0x000d;
        public const int FTDI_BAUDRATE_460800 = 0x4006;
        public const int FTDI_BAUDRATE_921600 = 0x8003;

        /***
         *  Default Serial Configuration
         *  Baud rate: 9600
         *  Data bits: 8
         *  Stop bits: 1
         *  Parity: None
         *  Flow Control: Off
         */
        private const int FTDI_SET_DATA_DEFAULT = 0x0008;
        private const int FTDI_SET_MODEM_CTRL_DEFAULT1 = 0x0101;
        private const int FTDI_SET_MODEM_CTRL_DEFAULT2 = 0x0202;
        private const int FTDI_SET_MODEM_CTRL_DEFAULT3 = 0x0100;
        private const int FTDI_SET_MODEM_CTRL_DEFAULT4 = 0x0200;
        private const int FTDI_SET_FLOW_CTRL_DEFAULT = 0x0000;

        private int currentSioSetData = 0x0000;

        private static readonly byte[] EMPTY_BYTE_ARRAY = Array.Empty<byte>();

        /**
         * Flow control variables
         */
        internal bool rtsCtsEnabled;
        internal bool dtrDsrEnabled;

        internal bool ctsState;
        internal bool dsrState;
        internal bool firstTime; // with this flag we set the CTS and DSR state to the first value received from the FTDI device

        internal IUsbCTSCallback ctsCallback;
        internal IUsbDSRCallback dsrCallback;

        internal readonly UsbInterface mInterface;
        internal UsbEndpoint inEndpoint;
        internal UsbEndpoint outEndpoint;

        public FTDIUtilities ftdiUtilities;

        internal IUsbParityCallback parityCallback;
        internal IUsbFrameCallback frameCallback;
        internal IUsbOverrunCallback overrunCallback;
        internal IUsbBreakCallback breakCallback;

        public FTDISerialDevice(UsbDevice device, UsbDeviceConnection connection) : this(device, connection, -1)
        {
        }

        public FTDISerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            ftdiUtilities = new FTDIUtilities(this);
            rtsCtsEnabled = false;
            dtrDsrEnabled = false;
            ctsState = true;
            dsrState = true;
            firstTime = true;
            mInterface = device.GetInterface(iface >= 0 ? iface : 0);
        }

        public override bool Open()
        {
            bool ret = OpenFTDI();

            if (ret)
            {
                // Initialize UsbRequest
                UsbRequest requestIN = new SafeUsbRequest();

                requestIN.Initialize(connection, inEndpoint);

                // Restart the working thread if it has been killed before and  get and claim interface
                RestartWorkingThread();
                RestartWriteThread();

                // Pass references to the threads
                SetThreadsParams(requestIN, outEndpoint);

                asyncMode = true;
                IsOpen = true;

                return true;
            }
            else
            {
                IsOpen = false;
                return false;
            }
        }

        public override void Close()
        {
            SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT3, 0);
            SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT4, 0);
            currentSioSetData = 0x0000;
            KillWorkingThread();
            KillWriteThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override bool SyncOpen()
        {
            bool ret = OpenFTDI();
            if (ret)
            {
                SetSyncParams(inEndpoint, outEndpoint);
                asyncMode = false;

                // Init Streams
                inputStream = new SerialInputStream(this);
                outputStream = new SerialOutputStream(this);

                IsOpen = true;

                return true;
            }
            else
            {
                IsOpen = false;
                return false;
            }
        }

        public override void SyncClose()
        {
            SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT3, 0);
            SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT4, 0);
            currentSioSetData = 0x0000;
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override void SetBaudRate(int baudRate)
        {
            int value = 0;
            if (baudRate >= 0 && baudRate <= 300)
            {
                value = FTDI_BAUDRATE_300;
            }
            else if (baudRate > 300 && baudRate <= 600)
            {
                value = FTDI_BAUDRATE_600;
            }
            else if (baudRate > 600 && baudRate <= 1200)
            {
                value = FTDI_BAUDRATE_1200;
            }
            else if (baudRate > 1200 && baudRate <= 2400)
            {
                value = FTDI_BAUDRATE_2400;
            }
            else if (baudRate > 2400 && baudRate <= 4800)
            {
                value = FTDI_BAUDRATE_4800;
            }
            else if (baudRate > 4800 && baudRate <= 9600)
            {
                value = FTDI_BAUDRATE_9600;
            }
            else if (baudRate > 9600 && baudRate <= 19200)
            {
                value = FTDI_BAUDRATE_19200;
            }
            else if (baudRate > 19200 && baudRate <= 38400)
            {
                value = FTDI_BAUDRATE_38400;
            }
            else if (baudRate > 19200 && baudRate <= 57600)
            {
                value = FTDI_BAUDRATE_57600;
            }
            else if (baudRate > 57600 && baudRate <= 115200)
            {
                value = FTDI_BAUDRATE_115200;
            }
            else if (baudRate > 115200 && baudRate <= 230400)
            {
                value = FTDI_BAUDRATE_230400;
            }
            else if (baudRate > 230400 && baudRate <= 460800)
            {
                value = FTDI_BAUDRATE_460800;
            }
            else if (baudRate > 460800 && baudRate <= 921600)
            {
                value = FTDI_BAUDRATE_921600;
            }
            else if (baudRate > 921600)
            {
                value = FTDI_BAUDRATE_921600;
            }
            else
            {
                value = FTDI_BAUDRATE_9600;
            }

            if(SetControlCommand(FTDI_SIO_SET_BAUD_RATE, value, 0)>=0)
            {
                CurrentBaudRate = baudRate;
            }
        }

        public override void SetDataBits(int dataBits)
        {
            switch (dataBits)
            {
                case UsbSerialInterface.DATA_BITS_5:
                    currentSioSetData |= 1;
                    currentSioSetData &= ~(1 << 1);
                    currentSioSetData |= 1 << 2;
                    currentSioSetData &= ~(1 << 3);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.DATA_BITS_6:
                    currentSioSetData &= ~1;
                    currentSioSetData |= 1 << 1;
                    currentSioSetData |= 1 << 2;
                    currentSioSetData &= ~(1 << 3);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.DATA_BITS_7:
                    currentSioSetData |= 1;
                    currentSioSetData |= 1 << 1;
                    currentSioSetData |= 1 << 2;
                    currentSioSetData &= ~(1 << 3);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.DATA_BITS_8:
                    currentSioSetData &= ~1;
                    currentSioSetData &= ~(1 << 1);
                    currentSioSetData &= ~(1 << 2);
                    currentSioSetData |= 1 << 3;
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                default:
                    currentSioSetData &= ~1;
                    currentSioSetData &= ~(1 << 1);
                    currentSioSetData &= ~(1 << 2);
                    currentSioSetData |= 1 << 3;
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;
            }
        }

        public override void SetStopBits(int stopBits)
        {
            switch (stopBits)
            {
                case UsbSerialInterface.STOP_BITS_1:
                    currentSioSetData &= ~(1 << 11);
                    currentSioSetData &= ~(1 << 12);
                    currentSioSetData &= ~(1 << 13);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.STOP_BITS_15:
                    currentSioSetData |= 1 << 11;
                    currentSioSetData &= ~(1 << 12);
                    currentSioSetData &= ~(1 << 13);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.STOP_BITS_2:
                    currentSioSetData &= ~(1 << 11);
                    currentSioSetData |= 1 << 12;
                    currentSioSetData &= ~(1 << 13);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                default:
                    currentSioSetData &= ~(1 << 11);
                    currentSioSetData &= ~(1 << 12);
                    currentSioSetData &= ~(1 << 13);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;
            }
        }

        public override void SetParity(int parity)
        {
            switch (parity)
            {
                case UsbSerialInterface.PARITY_NONE:
                    currentSioSetData &= ~(1 << 8);
                    currentSioSetData &= ~(1 << 9);
                    currentSioSetData &= ~(1 << 10);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.PARITY_ODD:
                    currentSioSetData |= 1 << 8;
                    currentSioSetData &= ~(1 << 9);
                    currentSioSetData &= ~(1 << 10);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.PARITY_EVEN:
                    currentSioSetData &= ~(1 << 8);
                    currentSioSetData |= 1 << 9;
                    currentSioSetData &= ~(1 << 10);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.PARITY_MARK:
                    currentSioSetData |= 1 << 8;
                    currentSioSetData |= 1 << 9;
                    currentSioSetData &= ~(1 << 10);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                case UsbSerialInterface.PARITY_SPACE:
                    currentSioSetData &= ~(1 << 8);
                    currentSioSetData &= ~(1 << 9);
                    currentSioSetData |= 1 << 10;
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;

                default:
                    currentSioSetData &= ~(1 << 8);
                    currentSioSetData &= ~(1 << 9);
                    currentSioSetData &= ~(1 << 10);
                    SetControlCommand(FTDI_SIO_SET_DATA, currentSioSetData, 0);
                    break;
            }
        }

        public override void SetFlowControl(int flowControl)
        {
            switch (flowControl)
            {
                case UsbSerialInterface.FLOW_CONTROL_OFF:
                    SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, FTDI_SET_FLOW_CTRL_DEFAULT, 0);
                    rtsCtsEnabled = false;
                    dtrDsrEnabled = false;
                    break;

                case UsbSerialInterface.FLOW_CONTROL_RTS_CTS:
                    rtsCtsEnabled = true;
                    dtrDsrEnabled = false;
                    int indexRTSCTS = 0x0001;
                    SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, FTDI_SET_FLOW_CTRL_DEFAULT, indexRTSCTS);
                    break;

                case UsbSerialInterface.FLOW_CONTROL_DSR_DTR:
                    dtrDsrEnabled = true;
                    rtsCtsEnabled = false;
                    int indexDSRDTR = 0x0002;
                    SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, FTDI_SET_FLOW_CTRL_DEFAULT, indexDSRDTR);
                    break;

                case UsbSerialInterface.FLOW_CONTROL_XON_XOFF:
                    int indexXONXOFF = 0x0004;
                    int wValue = 0x1311;
                    SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, wValue, indexXONXOFF);
                    break;

                default:
                    SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, FTDI_SET_FLOW_CTRL_DEFAULT, 0);
                    break;
            }
        }

        public override void SetRTS(bool state)
        {
            if (state)
            {
                SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SIO_SET_RTS_HIGH, 0);
            }
            else
            {
                SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SIO_SET_RTS_LOW, 0);
            }
        }

        public override void SetDTR(bool state)
        {
            if (state)
            {
                SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SIO_SET_DTR_HIGH, 0);
            }
            else
            {
                SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SIO_SET_DTR_LOW, 0);
            }
        }

        public override void GetCTS(IUsbCTSCallback ctsCallback)
        {
            this.ctsCallback = ctsCallback;
        }

        public override void GetDSR(IUsbDSRCallback dsrCallback)
        {
            this.dsrCallback = dsrCallback;
        }

        public override void GetBreak(IUsbBreakCallback breakCallback)
        {
            this.breakCallback = breakCallback;
        }

        public override void GetFrame(IUsbFrameCallback frameCallback)
        {
            this.frameCallback = frameCallback;
        }

        public override void GetOverrun(IUsbOverrunCallback overrunCallback)
        {
            this.overrunCallback = overrunCallback;
        }

        public override void GetParity(IUsbParityCallback parityCallback)
        {
            this.parityCallback = parityCallback;
        }

        private bool OpenFTDI()
        {
            if (connection.ClaimInterface(mInterface, true))
            {
                Log.Info(CLASS_ID, "Interface succesfully claimed");
            }
            else
            {
                Log.Info(CLASS_ID, "Interface could not be claimed");
                return false;
            }

            // Assign endpoints
            int numberEndpoints = mInterface.EndpointCount;
            for (int i = 0; i <= numberEndpoints - 1; i++)
            {
                UsbEndpoint endpoint = mInterface.GetEndpoint(i);
                if (endpoint.Type == UsbAddressing.XferBulk
                        && endpoint.Direction == UsbAddressing.In)
                {
                    inEndpoint = endpoint;
                }
                else
                {
                    outEndpoint = endpoint;
                }
            }

            // Default Setup
            firstTime = true;
            if (SetControlCommand(FTDI_SIO_RESET, 0x00, 0) < 0)
            {
                return false;
            }

            if (SetControlCommand(FTDI_SIO_SET_DATA, FTDI_SET_DATA_DEFAULT, 0) < 0)
            {
                return false;
            }

            currentSioSetData = FTDI_SET_DATA_DEFAULT;
            if (SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT1, 0) < 0)
            {
                return false;
            }

            if (SetControlCommand(FTDI_SIO_MODEM_CTRL, FTDI_SET_MODEM_CTRL_DEFAULT2, 0) < 0)
            {
                return false;
            }

            if (SetControlCommand(FTDI_SIO_SET_FLOW_CTRL, FTDI_SET_FLOW_CTRL_DEFAULT, 0) < 0)
            {
                return false;
            }

            if (SetControlCommand(FTDI_SIO_SET_BAUD_RATE, FTDI_BAUDRATE_9600, 0) < 0)
            {
                return false;
            }

            PurgeHwBuffers(true, true);

            // Flow control disabled by default
            rtsCtsEnabled = false;
            dtrDsrEnabled = false;

            return true;
        }

        private int SetControlCommand(int request, int value, int index)
        {
            int dataLength = 0;

            int response = connection.ControlTransfer((UsbAddressing)FTDI_REQTYPE_HOST2DEVICE, request, value, mInterface.Id + 1 + index, null, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data:  || Response: {4}", FTDI_REQTYPE_HOST2DEVICE, request, value, index, response));

            return response;
        }

        public override int SyncRead(byte[] buffer, int timeout)
        {
            long beginTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long stopTime = beginTime + timeout;

            if (asyncMode)
            {
                return -1;
            }

            if (buffer == null)
            {
                return 0;
            }

            if (mr1Version)
            {
                return ReadSyncJelly(buffer, timeout, stopTime);
            }

            int n = buffer.Length / 62;
            if (buffer.Length % 62 != 0)
            {
                n++;
            }

            byte[] tempBuffer = new byte[buffer.Length + (n * 2)];

            int readen = 0;

            do
            {
                int timeLeft = 0;
                if (timeout > 0)
                {
                    timeLeft = (int)(stopTime - (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond));
                    if (timeLeft <= 0)
                    {
                        break;
                    }
                }

                int numberBytes = connection.BulkTransfer(inEndpoint, tempBuffer, tempBuffer.Length, timeLeft);

                if (numberBytes > 2) // Data received
                {
                    byte[] newBuffer = AdaptArray(tempBuffer);
                    Array.Copy(newBuffer, 0, buffer, 0, buffer.Length);

                    int p = numberBytes / 64;
                    if (numberBytes % 64 != 0)
                    {
                        p++;
                    }
                    readen = numberBytes - (p * 2);
                }
            } while (readen <= 0);

            return readen;
        }

        private static readonly byte[] skip = new byte[2];

        /**
         * This method avoids creation of garbage by reusing the same
         * array instance for skipping header bytes and running
         * {@link UsbDeviceConnection#bulkTransfer(UsbEndpoint, byte[], int, int, int)}
         * directly.
         */
        private int ReadSyncJelly(byte[] buffer, int timeout, long stopTime)
        {
            int read = 0;
            do
            {
                int timeLeft = 0;
                if (timeout > 0)
                {
                    timeLeft = (int)(stopTime - Java.Lang.JavaSystem.CurrentTimeMillis());
                    if (timeLeft <= 0)
                    {
                        break;
                    }
                }

                int numberBytes = connection.BulkTransfer(inEndpoint, skip, skip.Length, timeLeft);

                if (numberBytes > 2) // Data received
                {
                    numberBytes = connection.BulkTransfer(inEndpoint, buffer, read, 62, timeLeft);
                    read += numberBytes;
                }
            } while (read <= 0);

            return read;
        }

        public override void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            // TODO
            if (purgeReadBuffers)
            {
                int result = connection.ControlTransfer((UsbAddressing)FTDI_DEVICE_OUT_REQTYPE, FTDI_SIO_RESET,
                        SIO_RESET_PURGE_RX, 0 /* index */, null, 0, USB_TIMEOUT);
                if (result != 0)
                {
                    throw new Java.IO.IOException("Flushing RX failed: result=" + result);
                }
            }

            if (purgeWriteBuffers)
            {
                int result = connection.ControlTransfer((UsbAddressing)FTDI_DEVICE_OUT_REQTYPE, FTDI_SIO_RESET,
                        SIO_RESET_PURGE_TX, 0 /* index */, null, 0, USB_TIMEOUT);
                if (result != 0)
                {
                    throw new Java.IO.IOException("Flushing RX failed: result=" + result);
                }
            }
        }

        // Special treatment needed to FTDI devices
        public static byte[] AdaptArray(byte[] ftdiData)
        {
            int length = ftdiData.Length;
            if (length > 64)
            {
                int n = 1;
                int p = 64;
                // Precalculate length without FTDI headers
                while (p < length)
                {
                    n++;
                    p = n * 64;
                }
                int realLength = length - (n * 2);
                byte[] data = new byte[realLength];
                CopyData(ftdiData, data);
                return data;
            }
            else if (length == 2) // special case optimization that returns the same instance.
            {
                return EMPTY_BYTE_ARRAY;
            }
            else
            {
                return Java.Util.Arrays.CopyOfRange(ftdiData, 2, length);
            }
        }

        // Copy data without FTDI headers
        private static void CopyData(byte[] src, byte[] dst)
        {
            int srcPos = 2, dstPos = 0;
            while (srcPos - 2 <= src.Length - 64)
            {
                Array.Copy(src, srcPos, dst, dstPos, 62);
                srcPos += 64;
                dstPos += 62;
            }
            int remaining = src.Length - srcPos + 2;
            if (remaining > 0)
            {
                Array.Copy(src, srcPos, dst, dstPos, remaining - 2);
            }
        }
    }

    public class FTDIUtilities
    {
        // Special treatment needed to FTDI devices
        private FTDISerialDevice fTDISerialDevice;

        public FTDIUtilities(FTDISerialDevice fTDIdevice)
        {
            this.fTDISerialDevice = fTDIdevice;
        }

        public void CheckModemStatus(byte[] data)
        {
            if (data.Length == 0) // Safeguard for zero length arrays
            {
                return;
            }

            bool cts = (data[0] & 0x10) == 0x10;
            bool dsr = (data[0] & 0x20) == 0x20;

            if (fTDISerialDevice.firstTime) // First modem status received
            {
                fTDISerialDevice.ctsState = cts;
                fTDISerialDevice.dsrState = dsr;

                if (fTDISerialDevice.rtsCtsEnabled && fTDISerialDevice.ctsCallback != null)
                {
                    fTDISerialDevice.ctsCallback.OnCTSChanged(fTDISerialDevice.ctsState);
                }

                if (fTDISerialDevice.dtrDsrEnabled && fTDISerialDevice.dsrCallback != null)
                {
                    fTDISerialDevice.dsrCallback.OnDSRChanged(fTDISerialDevice.dsrState);
                }

                fTDISerialDevice.firstTime = false;
                return;
            }

            if (fTDISerialDevice.rtsCtsEnabled
                    && cts != fTDISerialDevice.ctsState && fTDISerialDevice.ctsCallback != null) //CTS
            {
                fTDISerialDevice.ctsState = !fTDISerialDevice.ctsState;
                fTDISerialDevice.ctsCallback.OnCTSChanged(fTDISerialDevice.ctsState);
            }

            if (fTDISerialDevice.dtrDsrEnabled
                    && dsr != fTDISerialDevice.dsrState && fTDISerialDevice.dsrCallback != null) //DSR
            {
                fTDISerialDevice.dsrState = !fTDISerialDevice.dsrState;
                fTDISerialDevice.dsrCallback.OnDSRChanged(fTDISerialDevice.dsrState);
            }

            if (fTDISerialDevice.parityCallback != null) // Parity error checking
            {
                if ((data[1] & 0x04) == 0x04)
                {
                    fTDISerialDevice.parityCallback.OnParityError();
                }
            }

            if (fTDISerialDevice.frameCallback != null) // Frame error checking
            {
                if ((data[1] & 0x08) == 0x08)
                {
                    fTDISerialDevice.frameCallback.OnFramingError();
                }
            }

            if (fTDISerialDevice.overrunCallback != null) // Overrun error checking
            {
                if ((data[1] & 0x02) == 0x02)
                {
                    fTDISerialDevice.overrunCallback.OnOverrunError();
                }
            }

            if (fTDISerialDevice.breakCallback != null) // Break interrupt checking
            {
                if ((data[1] & 0x10) == 0x10)
                {
                    fTDISerialDevice.breakCallback.OnBreakInterrupt();
                }
            }
        }
    }
}