using System;
using System.Threading;
using Android.Hardware.Usb;
using Android.Util;
using com.felhr.utils;
using Java.Lang;
using USBSerialLibrary.utils;

namespace com.felhr.usbserial
{
    public class CP2102SerialDevice : UsbSerialDevice
    {
        private HexData hexData;
        internal const string CLASS_ID = nameof(CP2102SerialDevice);

        private const int CP210x_IFC_ENABLE = 0x00;
        private const int CP210x_SET_BAUDDIV = 0x01;
        private const int CP210x_SET_LINE_CTL = 0x03;
        private const int CP210x_GET_LINE_CTL = 0x04;
        private const int CP210x_SET_MHS = 0x07;
        private const int CP210x_SET_BAUDRATE = 0x1E;
        private const int CP210x_SET_FLOW = 0x13;
        private const int CP210x_SET_XON = 0x09;
        private const int CP210x_SET_XOFF = 0x0A;
        private const int CP210x_SET_CHARS = 0x19;
        private const int CP210x_GET_MDMSTS = 0x08;
        private const int CP210x_GET_COMM_STATUS = 0x10;

        private const int CP210x_REQTYPE_HOST2DEVICE = 0x41;
        private const int CP210x_REQTYPE_DEVICE2HOST = 0xC1;

        private const int CP210x_MHS_RTS_ON = 0x202;
        private const int CP210x_MHS_RTS_OFF = 0x200;
        private const int CP210x_MHS_DTR_ON = 0x101;
        private const int CP210x_MHS_DTR_OFF = 0x100;

        private const int SILABSER_FLUSH_REQUEST_CODE = 0x12;

        private const int FLUSH_READ_CODE = 0x0a;
        private const int FLUSH_WRITE_CODE = 0x05;

        private const int CP210x_PURGE = 0x12;
        private const int CP210x_PURGE_ALL = 0x000f;

        /***
         *  Default Serial Configuration
         *  Baud rate: 9600
         *  Data bits: 8
         *  Stop bits: 1
         *  Parity: None
         *  Flow Control: Off
         */
        /*
           * SILABSER_SET_BAUDDIV_REQUEST_CODE
           */
        private const int BAUD_RATE_GEN_FREQ = 0x384000;

        /*
         * SILABSER_SET_MHS_REQUEST_CODE
         */
        private const int MCR_DTR = 0x0001;
        private const int MCR_RTS = 0x0002;
        private const int MCR_ALL = 0x0003;

        private const int CP210x_UART_ENABLE = 0x0001;
        private const int CP210x_UART_DISABLE = 0x0000;
        private const int CP210x_LINE_CTL_DEFAULT = 0x0800;
        private const int CP210x_MHS_DEFAULT = 0x0000;
        private const int CP210x_MHS_DTR = 0x0001;
        private const int CP210x_MHS_RTS = 0x0010;
        private const int CP210x_MHS_ALL = 0x0011;
        private const int CP210x_XON = 0x0000;
        private const int CP210x_XOFF = 0x0000;
        private const int DEFAULT_BAUDRATE = 9600;

        /**
         * Flow control variables
         */
        internal bool rtsCtsEnabled;
        internal bool dtrDsrEnabled;
        internal bool ctsState;
        internal bool dsrState;

        internal IUsbCTSCallback ctsCallback;
        internal IUsbDSRCallback dsrCallback;

        private readonly UsbInterface mInterface;
        private UsbEndpoint inEndpoint;
        private UsbEndpoint outEndpoint;

        private FlowControlThreadCP2102 flowControlThread;

        // COMM_STATUS callbacks
        internal IUsbParityCallback parityCallback;

        internal IUsbBreakCallback breakCallback;
        internal IUsbFrameCallback frameCallback;
        internal IUsbOverrunCallback overrunCallback;

        public CP2102SerialDevice(UsbDevice device, UsbDeviceConnection connection) : this(device, connection, -1)
        {
            hexData = new HexData();
        }

        public CP2102SerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            hexData = new HexData();
            rtsCtsEnabled = false;
            dtrDsrEnabled = false;
            ctsState = true;
            dsrState = true;
            mInterface = device.GetInterface(iface >= 0 ? iface : 0);
        }

        public override bool Open()
        {
            bool ret = OpenCP2102();

            if (ret)
            {
                // Initialize UsbRequest
                UsbRequest requestIN = new SafeUsbRequest();
                requestIN.Initialize(connection, inEndpoint);

                // Restart the working thread if it has been killed before and  get and claim interface
                RestartWorkingThread();
                RestartWriteThread();

                // Create Flow control thread but it will only be started if necessary
                CreateFlowControlThread();

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
            SetControlCommand(CP210x_PURGE, CP210x_PURGE_ALL, null);
            SetControlCommand(CP210x_IFC_ENABLE, CP210x_UART_DISABLE, null);
            KillWorkingThread();
            KillWriteThread();
            StopFlowControlThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override bool SyncOpen()
        {
            bool ret = OpenCP2102();
            if (ret)
            {
                // Create Flow control thread but it will only be started if necessary
                CreateFlowControlThread();
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
            SetControlCommand(CP210x_PURGE, CP210x_PURGE_ALL, null);
            SetControlCommand(CP210x_IFC_ENABLE, CP210x_UART_DISABLE, null);
            StopFlowControlThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override void SetBaudRate(int baudRate)
        {
            byte[] data = new byte[] {
                (byte) (baudRate & 0xff),
                (byte) ((baudRate >> 8) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff)
            };

            if(SetControlCommand(CP210x_SET_BAUDRATE, 0, data) >= 0)
            {
                CurrentBaudRate = baudRate;
            }
        }

        public override void SetDataBits(int dataBits)
        {
            short wValue = GetCTL();
            wValue &= ~ 0x0F00;
            switch (dataBits)
            {
                case UsbSerialInterface.DATA_BITS_5:
                    wValue |= 0x0500;
                    break;

                case UsbSerialInterface.DATA_BITS_6:
                    wValue |= 0x0600;
                    break;

                case UsbSerialInterface.DATA_BITS_7:
                    wValue |= 0x0700;
                    break;

                case UsbSerialInterface.DATA_BITS_8:
                    wValue |= 0x0800;
                    break;

                default:
                    return;
            }
            SetControlCommand(CP210x_SET_LINE_CTL, wValue, null);
        }

        public override void SetStopBits(int stopBits)
        {
            short wValue = GetCTL();
            wValue &= ~ 0x0003;

            switch (stopBits)
            {
                case UsbSerialInterface.STOP_BITS_1:
                    wValue |= 0;
                    break;

                case UsbSerialInterface.STOP_BITS_15:
                    wValue |= 1;
                    break;

                case UsbSerialInterface.STOP_BITS_2:
                    wValue |= 2;
                    break;

                default:
                    return;
            }
            SetControlCommand(CP210x_SET_LINE_CTL, wValue, null);
        }

        public override void SetParity(int parity)
        {
            short wValue = GetCTL();
            wValue &= ~ 0x00F0;
            switch (parity)
            {
                case UsbSerialInterface.PARITY_NONE:
                    wValue |= 0x0000;
                    break;

                case UsbSerialInterface.PARITY_ODD:
                    wValue |= 0x0010;
                    break;

                case UsbSerialInterface.PARITY_EVEN:
                    wValue |= 0x0020;
                    break;

                case UsbSerialInterface.PARITY_MARK:
                    wValue |= 0x0030;
                    break;

                case UsbSerialInterface.PARITY_SPACE:
                    wValue |= 0x0040;
                    break;

                default:
                    return;
            }
            SetControlCommand(CP210x_SET_LINE_CTL, wValue, null);
        }

        public override void SetFlowControl(int flowControl)
        {
            switch (flowControl)
            {
                case UsbSerialInterface.FLOW_CONTROL_OFF:
                    byte[] dataOff = new byte[]{
                        (byte) 0x01, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x40, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x80, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x20, (byte) 0x00, (byte) 0x00
                };
                    rtsCtsEnabled = false;
                    dtrDsrEnabled = false;
                    SetControlCommand(CP210x_SET_FLOW, 0, dataOff);
                    break;

                case UsbSerialInterface.FLOW_CONTROL_RTS_CTS:
                    byte[] dataRTSCTS = new byte[]{
                        (byte) 0x09, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x40, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x80, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x20, (byte) 0x00, (byte) 0x00
                };
                    rtsCtsEnabled = true;
                    dtrDsrEnabled = false;
                    SetControlCommand(CP210x_SET_FLOW, 0, dataRTSCTS);
                    SetControlCommand(CP210x_SET_MHS, CP210x_MHS_RTS_ON, null);
                    byte[] commStatusCTS = GetCommStatus();
                    ctsState = (commStatusCTS[4] & 0x01) == 0x00;
                    StartFlowControlThread();
                    break;

                case UsbSerialInterface.FLOW_CONTROL_DSR_DTR:
                    byte[] dataDSRDTR = new byte[]{
                        (byte) 0x11, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x40, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x80, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x20, (byte) 0x00, (byte) 0x00
                };
                    dtrDsrEnabled = true;
                    rtsCtsEnabled = false;
                    SetControlCommand(CP210x_SET_FLOW, 0, dataDSRDTR);
                    SetControlCommand(CP210x_SET_MHS, CP210x_MHS_DTR_ON, null);
                    byte[] commStatusDSR = GetCommStatus();
                    dsrState = (commStatusDSR[4] & 0x02) == 0x00;
                    StartFlowControlThread();
                    break;

                case UsbSerialInterface.FLOW_CONTROL_XON_XOFF:
                    byte[] dataXONXOFF = new byte[]{
                        (byte) 0x01, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x43, (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x80, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x20, (byte) 0x00, (byte) 0x00
                };

                    byte[] dataChars = new byte[]{
                        (byte) 0x00, (byte) 0x00, (byte) 0x00,
                        (byte) 0x00, (byte) 0x11, (byte) 0x13
                };
                    SetControlCommand(CP210x_SET_CHARS, 0, dataChars);
                    SetControlCommand(CP210x_SET_FLOW, 0, dataXONXOFF);
                    break;

                default:
                    return;
            }
        }

        public override void SetRTS(bool state)
        {
            if (state)
            {
                SetControlCommand(CP210x_SET_MHS, CP210x_MHS_RTS_ON, null);
            }
            else
            {
                SetControlCommand(CP210x_SET_MHS, CP210x_MHS_RTS_OFF, null);
            }
        }

        public override void SetDTR(bool state)
        {
            if (state)
            {
                SetControlCommand(CP210x_SET_MHS, CP210x_MHS_DTR_ON, null);
            }
            else
            {
                SetControlCommand(CP210x_SET_MHS, CP210x_MHS_DTR_OFF, null);
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
            StartFlowControlThread();
        }

        private bool OpenCP2102()
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
            if (SetControlCommand(CP210x_IFC_ENABLE, CP210x_UART_ENABLE, null) < 0)
            {
                return false;
            }

            SetControlCommand(CP210x_SET_BAUDDIV, BAUD_RATE_GEN_FREQ / DEFAULT_BAUDRATE, null);

            //SetBaudRate(DEFAULT_BAUDRATE);
            //if (SetControlCommand(CP210x_SET_LINE_CTL, CP210x_LINE_CTL_DEFAULT, null) < 0)
            //{
            //    return false;
            //}

            SetFlowControl(UsbSerialInterface.FLOW_CONTROL_OFF);
            //SetControlCommand(CP210x_SET_MHS, MCR_ALL | CP210x_MHS_DTR_OFF | CP210x_MHS_RTS_OFF, null);

            if (SetControlCommand(CP210x_SET_MHS, CP210x_MHS_DEFAULT, null) < 0)
            {
                return false;
            }

            PurgeHwBuffers(true, true);

            return true;
        }

        private void CreateFlowControlThread()
        {
            flowControlThread = new FlowControlThreadCP2102(this);
        }

        private void StartFlowControlThread()
        {
            if (!flowControlThread.IsAlive)
            {
                flowControlThread.Start();
            }
        }

        private void StopFlowControlThread()
        {
            if (flowControlThread != null)
            {
                flowControlThread.StopThread();
                flowControlThread = null;
            }
        }

        public void SetParameters(int baudRate, int dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            int configDataBits = 0;
            switch (dataBits)
            {
                case DATA_BITS_5:
                    configDataBits |= 0x0500;
                    break;
                case DATA_BITS_6:
                    configDataBits |= 0x0600;
                    break;
                case DATA_BITS_7:
                    configDataBits |= 0x0700;
                    break;
                case DATA_BITS_8:
                    configDataBits |= 0x0800;
                    break;
                default:
                    configDataBits |= 0x0800;
                    break;
            }

            switch (parity)
            {
                case Parity.Odd:
                    configDataBits |= 0x0010;
                    break;
                case Parity.Even:
                    configDataBits |= 0x0020;
                    break;
            }

            switch (stopBits)
            {
                case StopBits.One:
                    configDataBits |= 0;
                    break;
                case StopBits.Two:
                    configDataBits |= 2;
                    break;
            }
            SetControlCommand(CP210x_SET_LINE_CTL, configDataBits, null);
        }

        private int SetControlCommand(int request, int value, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }
            int response = connection.ControlTransfer((UsbAddressing)CP210x_REQTYPE_HOST2DEVICE, request, value, mInterface.Id, data, dataLength, USB_TIMEOUT);
            Log.Info("CP2102SerialDevice.SetControlCommand", System.String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CP210x_REQTYPE_HOST2DEVICE, request, value, mInterface.Id, hexData.BytesToString(data), response));
            return response;
        }

        internal byte[] GetModemState()
        {
            byte[] data = new byte[1];
            int response = connection.ControlTransfer((UsbAddressing)CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_MDMSTS, 0, mInterface.Id, data, 1, USB_TIMEOUT);
            Log.Info("CP2102SerialDevice.GetModemState", System.String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_MDMSTS, 0, mInterface.Id, hexData.BytesToString(data), response));

            return data;
        }

        internal byte[] GetCommStatus()
        {
            byte[] data = new byte[19];
            int response = connection.ControlTransfer((UsbAddressing)CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_COMM_STATUS, 0, mInterface.Id, data, 19, USB_TIMEOUT);
            Log.Info("CP2102SerialDevice.GetCommStatus", System.String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_COMM_STATUS, 0, mInterface.Id, hexData.BytesToString(data), response));
            return data;
        }

        private short GetCTL()
        {
            byte[] data = new byte[2];
            int response = connection.ControlTransfer((UsbAddressing)CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_LINE_CTL, 0, mInterface.Id, data, data.Length, USB_TIMEOUT);
            Log.Info("CP2102SerialDevice.GetCTL", System.String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", CP210x_REQTYPE_DEVICE2HOST, CP210x_GET_LINE_CTL, 0, mInterface.Id, hexData.BytesToString(data), response));

            return (short)((data[1] << 8) | (data[0] & 0xFF));
        }

        public override void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            // TODO
            int value = (purgeReadBuffers ? FLUSH_READ_CODE : 0)
                        | (purgeWriteBuffers ? FLUSH_WRITE_CODE : 0);

            if (value != 0)
            {
                SetControlCommand(SILABSER_FLUSH_REQUEST_CODE, value, null);
            }
        }
    }

    /*
      Thread to check every X time if flow signals CTS or DSR have been raised
   */

    internal class FlowControlThreadCP2102 : AbstractWorkerThread
    {
        private const long time = 40; // 40ms

        private CP2102SerialDevice device;

        private readonly object LockObject = new object();

        public FlowControlThreadCP2102(CP2102SerialDevice device)
        {
            this.device = device;
            firstTime = true;
        }

        public override void DoRun()
        {
            if (!firstTime) // Only execute the callback when the status change
            {
                byte[] modemState = PollLines();
                byte[] commStatus = device.GetCommStatus();

                // Check CTS status
                if (device.rtsCtsEnabled)
                {
                    if (device.ctsState != ((modemState[0] & 0x10) == 0x10))
                    {
                        device.ctsState = !device.ctsState;
                        device.ctsCallback?.OnCTSChanged(device.ctsState);
                    }
                }

                // Check DSR status
                if (device.dtrDsrEnabled)
                {
                    if (device.dsrState != ((modemState[0] & 0x20) == 0x20))
                    {
                        device.dsrState = !device.dsrState;
                        device.dsrCallback?.OnDSRChanged(device.dsrState);
                    }
                }

                //Check Parity Errors
                if (device.parityCallback != null)
                {
                    if ((commStatus[0] & 0x10) == 0x10)
                    {
                        device.parityCallback.OnParityError();
                    }
                }

                // Check frame error
                if (device.frameCallback != null)
                {
                    if ((commStatus[0] & 0x02) == 0x02)
                    {
                        device.frameCallback.OnFramingError();
                    }
                }

                // Check break interrupt
                if (device.breakCallback != null)
                {
                    if ((commStatus[0] & 0x01) == 0x01)
                    {
                        device.breakCallback.OnBreakInterrupt();
                    }
                }

                // Check Overrun error

                if (device.overrunCallback != null)
                {
                    if ((commStatus[0] & 0x04) == 0x04
                            || (commStatus[0] & 0x8) == 0x08)
                    {
                        device.overrunCallback.OnOverrunError();
                    }
                }
            }
            else // Execute the callback always the first time
            {
                if (device.rtsCtsEnabled && device.ctsCallback != null)
                {
                    device.ctsCallback.OnCTSChanged(device.ctsState);
                }

                if (device.dtrDsrEnabled && device.dsrCallback != null)
                {
                    device.dsrCallback.OnDSRChanged(device.dsrState);
                }

                firstTime = false;
            }
        }

        private byte[] PollLines()
        {
            //lock (this)
            using (LockObject.Lock(5000))
            {
                try
                {
                    Wait(time);
                }
                catch (InterruptedException e)
                {
                    Log.Info(CP2102SerialDevice.CLASS_ID, e.Message);
                }
            }

            return device.GetModemState();
        }
    }
}