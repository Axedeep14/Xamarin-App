using System;
using Android.Hardware.Usb;
using Android.Util;
using com.felhr.utils;

namespace com.felhr.usbserial
{
    public class CDCSerialDevice : UsbSerialDevice
    {
        private const string CLASS_ID = nameof(CDCSerialDevice);

        private const int CDC_REQTYPE_HOST2DEVICE = 0x21;
        private const int CDC_REQTYPE_DEVICE2HOST = 0xA1;

        private const int CDC_SET_LINE_CODING = 0x20;
        private const int CDC_GET_LINE_CODING = 0x21;
        private const int CDC_SET_CONTROL_LINE_STATE = 0x22;

        private const int CDC_SET_CONTROL_LINE_STATE_RTS = 0x2;
        private const int CDC_SET_CONTROL_LINE_STATE_DTR = 0x1;

        private HexData hexData;

        /***
         *  Default Serial Configuration
         *  Baud rate: 115200
         *  Data bits: 8
         *  Stop bits: 1
         *  Parity: None
         *  Flow Control: Off
         */

        private static readonly byte[] CDC_DEFAULT_LINE_CODING = new byte[] {
            (byte) 0x00, // Offset 0:4 dwDTERate
            (byte) 0xC2,
            (byte) 0x01,
            (byte) 0x00,
            (byte) 0x00, // Offset 5 bCharFormat (1 Stop bit)
            (byte) 0x00, // bParityType (None)
            (byte) 0x08  // bDataBits (8)
    };

        private const int CDC_CONTROL_LINE_ON = 0x0003;
        private const int CDC_CONTROL_LINE_OFF = 0x0000;

        private readonly UsbInterface mInterface;
        private UsbEndpoint inEndpoint;
        private UsbEndpoint outEndpoint;

        private int initialBaudRate = 0;

        private int controlLineState = CDC_CONTROL_LINE_ON;

        public CDCSerialDevice(UsbDevice device, UsbDeviceConnection connection) : this(device, connection, -1)
        {
            hexData = new HexData();
        }

        public CDCSerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            hexData = new HexData();
            mInterface = device.GetInterface(iface >= 0 ? iface : FindFirstCDC(device));
        }

        public override void SetInitialBaudRate(int initialBaudRate)
        {
            this.initialBaudRate = initialBaudRate;
        }

        public override int GetInitialBaudRate()
        {
            return initialBaudRate;
        }

        public override bool Open()
        {
            bool ret = OpenCDC();

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
            SetControlCommand(CDC_SET_CONTROL_LINE_STATE, CDC_CONTROL_LINE_OFF, null);
            KillWorkingThread();
            KillWriteThread();
            connection.ReleaseInterface(mInterface);
            connection.Close();
            IsOpen = false;
        }

        public override bool SyncOpen()
        {
            bool ret = OpenCDC();
            if (ret)
            {
                SetSyncParams(inEndpoint, outEndpoint);
                asyncMode = false;
                IsOpen = true;

                // Init Streams
                inputStream = new SerialInputStream(this);
                outputStream = new SerialOutputStream(this);

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
            SetControlCommand(CDC_SET_CONTROL_LINE_STATE, CDC_CONTROL_LINE_OFF, null);
            connection.ReleaseInterface(mInterface);
            connection.Close();
            IsOpen = false;
        }

        public override void SetBaudRate(int baudRate)
        {
            byte[] data = GetLineCoding();

            data[0] = (byte)(baudRate & 0xff);
            data[1] = (byte)((baudRate >> 8) & 0xff);
            data[2] = (byte)((baudRate >> 16) & 0xff);
            data[3] = (byte)((baudRate >> 24) & 0xff);

            if(SetControlCommand(CDC_SET_LINE_CODING, 0, data) >= 0)
            {
                CurrentBaudRate = baudRate;
            }
        }

        public override void SetDataBits(int dataBits)
        {
            byte[] data = GetLineCoding();
            switch (dataBits)
            {
                case UsbSerialInterface.DATA_BITS_5:
                    data[6] = 0x05;
                    break;

                case UsbSerialInterface.DATA_BITS_6:
                    data[6] = 0x06;
                    break;

                case UsbSerialInterface.DATA_BITS_7:
                    data[6] = 0x07;
                    break;

                case UsbSerialInterface.DATA_BITS_8:
                    data[6] = 0x08;
                    break;

                default:
                    return;
            }

            SetControlCommand(CDC_SET_LINE_CODING, 0, data);
        }

        public override void SetStopBits(int stopBits)
        {
            byte[] data = GetLineCoding();
            switch (stopBits)
            {
                case UsbSerialInterface.STOP_BITS_1:
                    data[4] = 0x00;
                    break;

                case UsbSerialInterface.STOP_BITS_15:
                    data[4] = 0x01;
                    break;

                case UsbSerialInterface.STOP_BITS_2:
                    data[4] = 0x02;
                    break;

                default:
                    return;
            }

            SetControlCommand(CDC_SET_LINE_CODING, 0, data);
        }

        public override void SetParity(int parity)
        {
            byte[] data = GetLineCoding();
            switch (parity)
            {
                case UsbSerialInterface.PARITY_NONE:
                    data[5] = 0x00;
                    break;

                case UsbSerialInterface.PARITY_ODD:
                    data[5] = 0x01;
                    break;

                case UsbSerialInterface.PARITY_EVEN:
                    data[5] = 0x02;
                    break;

                case UsbSerialInterface.PARITY_MARK:
                    data[5] = 0x03;
                    break;

                case UsbSerialInterface.PARITY_SPACE:
                    data[5] = 0x04;
                    break;

                default:
                    return;
            }

            SetControlCommand(CDC_SET_LINE_CODING, 0, data);
        }

        public override void SetFlowControl(int flowControl)
        {
            // TODO Auto-generated method stub
        }

        public override void SetRTS(bool state)
        {
            if (state)
            {
                controlLineState |= CDC_SET_CONTROL_LINE_STATE_RTS;
            }
            else
            {
                controlLineState &= ~CDC_SET_CONTROL_LINE_STATE_RTS;
            }

            SetControlCommand(CDC_SET_CONTROL_LINE_STATE, controlLineState, null);
        }

        public override void SetDTR(bool state)
        {
            if (state)
            {
                controlLineState |= CDC_SET_CONTROL_LINE_STATE_DTR;
            }
            else
            {
                controlLineState &= ~CDC_SET_CONTROL_LINE_STATE_DTR;
            }

            SetControlCommand(CDC_SET_CONTROL_LINE_STATE, controlLineState, null);
        }

        public override void GetCTS(IUsbCTSCallback ctsCallback)
        {
            //TODO
        }

        public override void GetDSR(IUsbDSRCallback dsrCallback)
        {
            //TODO
        }

        public override void GetBreak(IUsbBreakCallback breakCallback)
        {
            //TODO
        }

        public override void GetFrame(IUsbFrameCallback frameCallback)
        {
            //TODO
        }

        public override void GetOverrun(IUsbOverrunCallback overrunCallback)
        {
            //TODO
        }

        public override void GetParity(IUsbParityCallback parityCallback)
        {
            //TODO
        }

        private bool OpenCDC()
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
                else if (endpoint.Type == UsbAddressing.XferBulk
                       && endpoint.Direction == UsbAddressing.Out)
                {
                    outEndpoint = endpoint;
                }
            }

            if (outEndpoint == null || inEndpoint == null)
            {
                Log.Info(CLASS_ID, "Interface does not have an IN or OUT interface");
                return false;
            }

            // Default Setup
            SetControlCommand(CDC_SET_LINE_CODING, 0, GetInitialLineCoding());
            SetControlCommand(CDC_SET_CONTROL_LINE_STATE, CDC_CONTROL_LINE_ON, null);

            return true;
        }

        protected byte[] GetInitialLineCoding()
        {
            byte[] lineCoding;

            int initialBaudRate = GetInitialBaudRate();

            if (initialBaudRate > 0)
            {
                lineCoding = (byte[])CDC_DEFAULT_LINE_CODING.Clone();
                for (int i = 0; i < 4; i++)
                {
                    lineCoding[i] = (byte)((initialBaudRate >> (i * 8)) & 0xFF);
                }
            }
            else
            {
                lineCoding = CDC_DEFAULT_LINE_CODING;
            }

            return lineCoding;
        }

        private int SetControlCommand(int request, int value, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }
            int response = connection.ControlTransfer((UsbAddressing)CDC_REQTYPE_HOST2DEVICE, request, value, 0, data, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CDC_REQTYPE_HOST2DEVICE, request, value, 0, hexData.BytesToString(data), response));
            return response;
        }

        private byte[] GetLineCoding()
        {
            byte[] data = new byte[7];
            int response = connection.ControlTransfer((UsbAddressing)CDC_REQTYPE_DEVICE2HOST, CDC_GET_LINE_CODING, 0, 0, data, data.Length, USB_TIMEOUT);
            Log.Info(CLASS_ID, String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CDC_REQTYPE_DEVICE2HOST, CDC_GET_LINE_CODING, 0, 0, hexData.BytesToString(data), response));
            return data;
        }

        private static int FindFirstCDC(UsbDevice device)
        {
            int interfaceCount = device.InterfaceCount;

            for (int iIndex = 0; iIndex < interfaceCount; ++iIndex)
            {
                if (device.GetInterface(iIndex).InterfaceClass == UsbClass.CdcData)
                {
                    return iIndex;
                }
            }

            Log.Info(CLASS_ID, "There is no CDC class interface");
            return -1;
        }

        public override void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            // TODO
        }
    }
}