using System;
using System.Threading;
using Android.Hardware.Usb;
using Android.Util;
using com.felhr.utils;
using USBSerialLibrary.utils;

namespace com.felhr.usbserial
{
    public class CH34xSerialDevice : UsbSerialDevice
    {
        private HexData hexData;
        internal const string CLASS_ID = nameof(CH34xSerialDevice);

        private const int DEFAULT_BAUDRATE = 9600;

        private const int REQTYPE_HOST_FROM_DEVICE = UsbConstants.UsbTypeVendor | (int)UsbAddressing.In;
        private const int REQTYPE_HOST_TO_DEVICE = 0x40;

        private const int CH341_REQ_WRITE_REG = 0x9A;
        private const int CH341_REQ_READ_REG = 0x95;
        private const int CH341_REG_BREAK1 = 0x05;
        private const int CH341_REG_BREAK2 = 0x18;
        private const int CH341_NBREAK_BITS_REG1 = 0x01;
        private const int CH341_NBREAK_BITS_REG2 = 0x40;

        // Baud rates values
        private const int CH34X_300_1312 = 0xd980;

        private const int CH34X_300_0f2c = 0xeb;

        private const int CH34X_600_1312 = 0x6481;
        private const int CH34X_600_0f2c = 0x76;

        private const int CH34X_1200_1312 = 0xb281;
        private const int CH34X_1200_0f2c = 0x3b;

        private const int CH34X_2400_1312 = 0xd981;
        private const int CH34X_2400_0f2c = 0x1e;

        private const int CH34X_4800_1312 = 0x6482;
        private const int CH34X_4800_0f2c = 0x0f;

        private const int CH34X_9600_1312 = 0xb282;
        private const int CH34X_9600_0f2c = 0x08;

        private const int CH34X_19200_1312 = 0xd982;
        private const int CH34X_19200_0f2c_rest = 0x07;

        private const int CH34X_38400_1312 = 0x6483;

        private const int CH34X_57600_1312 = 0x9883;

        private const int CH34X_115200_1312 = 0xcc83;

        private const int CH34X_230400_1312 = 0xe683;

        private const int CH34X_460800_1312 = 0xf383;

        private const int CH34X_921600_1312 = 0xf387;

        // Parity values
        private const int CH34X_PARITY_NONE = 0xc3;

        private const int CH34X_PARITY_ODD = 0xcb;
        private const int CH34X_PARITY_EVEN = 0xdb;
        private const int CH34X_PARITY_MARK = 0xeb;
        private const int CH34X_PARITY_SPACE = 0xfb;

        //Flow control values
        private const int CH34X_FLOW_CONTROL_NONE = 0x0000;

        private const int CH34X_FLOW_CONTROL_RTS_CTS = 0x0101;
        private const int CH34X_FLOW_CONTROL_DSR_DTR = 0x0202;
        // XON/XOFF doesnt appear to be supported directly from hardware

        private readonly UsbInterface mInterface;
        private UsbEndpoint inEndpoint;
        private UsbEndpoint outEndpoint;

        private FlowControlThreadCH34x flowControlThread;
        internal IUsbCTSCallback ctsCallback;
        internal IUsbDSRCallback dsrCallback;
        internal bool rtsCtsEnabled;
        internal bool dtrDsrEnabled;
        private bool dtr = false;
        private bool rts = false;
        internal bool ctsState = false;
        internal bool dsrState = false;

        public CH34xSerialDevice(UsbDevice device, UsbDeviceConnection connection) : base(device, connection)
        {
            hexData = new HexData();
        }

        public CH34xSerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            hexData = new HexData();
            rtsCtsEnabled = false;
            dtrDsrEnabled = false;
            mInterface = device.GetInterface(iface >= 0 ? iface : 0);
        }

        public override bool Open()
        {
            bool ret = OpenCH34X();
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
            KillWorkingThread();
            KillWriteThread();
            StopFlowControlThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override bool SyncOpen()
        {
            bool ret = OpenCH34X();
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
            StopFlowControlThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override void SetBaudRate(int baudRate)
        {
            int ret = 0;
            if (baudRate <= 300)
            {
                ret = SetBaudRate(CH34X_300_1312, CH34X_300_0f2c); //300
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 300 && baudRate <= 600)
            {
                ret = SetBaudRate(CH34X_600_1312, CH34X_600_0f2c); //600
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 600 && baudRate <= 1200)
            {
                ret = SetBaudRate(CH34X_1200_1312, CH34X_1200_0f2c); //1200
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 1200 && baudRate <= 2400)
            {
                ret = SetBaudRate(CH34X_2400_1312, CH34X_2400_0f2c); //2400
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 2400 && baudRate <= 4800)
            {
                ret = SetBaudRate(CH34X_4800_1312, CH34X_4800_0f2c); //4800
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 4800 && baudRate <= 9600)
            {
                ret = SetBaudRate(CH34X_9600_1312, CH34X_9600_0f2c); //9600
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 9600 && baudRate <= 19200)
            {
                ret = SetBaudRate(CH34X_19200_1312, CH34X_19200_0f2c_rest); //19200
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 19200 && baudRate <= 38400)
            {
                ret = SetBaudRate(CH34X_38400_1312, CH34X_19200_0f2c_rest); //38400
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 38400 && baudRate <= 57600)
            {
                ret = SetBaudRate(CH34X_57600_1312, CH34X_19200_0f2c_rest); //57600
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 57600 && baudRate <= 115200) //115200
            {
                ret = SetBaudRate(CH34X_115200_1312, CH34X_19200_0f2c_rest);
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 115200 && baudRate <= 230400) //230400
            {
                ret = SetBaudRate(CH34X_230400_1312, CH34X_19200_0f2c_rest);
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 230400 && baudRate <= 460800) //460800
            {
                ret = SetBaudRate(CH34X_460800_1312, CH34X_19200_0f2c_rest);
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }
            else if (baudRate > 460800 && baudRate <= 921600)
            {
                ret = SetBaudRate(CH34X_921600_1312, CH34X_19200_0f2c_rest);
                if (ret == -1)
                {
                    Log.Info(CLASS_ID, "SetBaudRate failed!");
                }
            }

            if(ret>=0)
            {
                CurrentBaudRate = baudRate;
            }
        }

        public override void SetDataBits(int dataBits)
        {
            // TODO Auto-generated method stub
        }

        public override void SetStopBits(int stopBits)
        {
            // TODO Auto-generated method stub
        }

        public override void SetParity(int parity)
        {
            switch (parity)
            {
                case UsbSerialInterface.PARITY_NONE:
                    SetCh340xParity(CH34X_PARITY_NONE);
                    break;

                case UsbSerialInterface.PARITY_ODD:
                    SetCh340xParity(CH34X_PARITY_ODD);
                    break;

                case UsbSerialInterface.PARITY_EVEN:
                    SetCh340xParity(CH34X_PARITY_EVEN);
                    break;

                case UsbSerialInterface.PARITY_MARK:
                    SetCh340xParity(CH34X_PARITY_MARK);
                    break;

                case UsbSerialInterface.PARITY_SPACE:
                    SetCh340xParity(CH34X_PARITY_SPACE);
                    break;

                default:
                    break;
            }
        }

        public override void SetFlowControl(int flowControl)
        {
            switch (flowControl)
            {
                case UsbSerialInterface.FLOW_CONTROL_OFF:
                    rtsCtsEnabled = false;
                    dtrDsrEnabled = false;
                    SetCh340xFlow(CH34X_FLOW_CONTROL_NONE);
                    break;

                case UsbSerialInterface.FLOW_CONTROL_RTS_CTS:
                    rtsCtsEnabled = true;
                    dtrDsrEnabled = false;
                    SetCh340xFlow(CH34X_FLOW_CONTROL_RTS_CTS);
                    ctsState = CheckCTS();
                    StartFlowControlThread();
                    break;

                case UsbSerialInterface.FLOW_CONTROL_DSR_DTR:
                    rtsCtsEnabled = false;
                    dtrDsrEnabled = true;
                    SetCh340xFlow(CH34X_FLOW_CONTROL_DSR_DTR);
                    dsrState = CheckDSR();
                    StartFlowControlThread();
                    break;

                default:
                    break;
            }
        }

        public override void SetRTS(bool state)
        {
            rts = state;
            WriteHandshakeByte();
        }

        public override void SetDTR(bool state)
        {
            dtr = state;
            WriteHandshakeByte();
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

        private bool OpenCH34X()
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

            return Init() == 0;
        }

        private int Init()
        {
            /*
                Init the device at 9600 baud
             */

            if (SetControlCommandOut(0xa1, 0xc29c, 0xb2b9, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #1");
                return -1;
            }

            if (SetControlCommandOut(0xa4, 0xdf, 0, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #2");
                return -1;
            }

            if (SetControlCommandOut(0xa4, 0x9f, 0, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #3");
                return -1;
            }

            if (CheckState("init #4", 0x95, 0x0706, new int[] { 0x9f, 0xee }) == -1)
            {
                return -1;
            }

            if (SetControlCommandOut(0x9a, 0x2727, 0x0000, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #5");
                return -1;
            }

            if (SetControlCommandOut(0x9a, 0x1312, 0xb282, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #6");
                return -1;
            }

            if (SetControlCommandOut(0x9a, 0x0f2c, 0x0008, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #7");
                return -1;
            }

            if (SetControlCommandOut(0x9a, 0x2518, 0x00c3, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #8");
                return -1;
            }

            if (CheckState("init #9", 0x95, 0x0706, new int[] { 0x9f, 0xee }) == -1)
            {
                return -1;
            }

            if (SetControlCommandOut(0x9a, 0x2727, 0x0000, null) < 0)
            {
                Log.Info(CLASS_ID, "init failed! #10");
                return -1;
            }

            return 0;
        }

        private int SetBaudRate(int index1312, int index0f2c)
        {
            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x1312, index1312, null) < 0)
            {
                return -1;
            }

            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x0f2c, index0f2c, null) < 0)
            {
                return -1;
            }

            if (CheckState("set_baud_rate", 0x95, 0x0706, new int[] { 0x9f, 0xee }) == -1)
            {
                return -1;
            }

            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x2727, 0, null) < 0)
            {
                return -1;
            }

            return 0;
        }

        private int SetCh340xParity(int indexParity)
        {
            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x2518, indexParity, null) < 0)
            {
                return -1;
            }

            if (CheckState("set_parity", 0x95, 0x0706, new int[] { 0x9f, 0xee }) == -1)
            {
                return -1;
            }

            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x2727, 0, null) < 0)
            {
                return -1;
            }

            return 0;
        }

        private int SetCh340xFlow(int flowControl)
        {
            if (CheckState("set_flow_control", 0x95, 0x0706, new int[] { 0x9f, 0xee }) == -1)
            {
                return -1;
            }

            if (SetControlCommandOut(CH341_REQ_WRITE_REG, 0x2727, flowControl, null) == -1)
            {
                return -1;
            }

            return 0;
        }

        private int CheckState(string msg, int request, int value, int[] expected)
        {
            byte[] buffer = new byte[expected.Length];
            int ret = SetControlCommandIn(request, value, 0, buffer);

            if (ret != expected.Length)
            {
                Log.Info(CLASS_ID, "Expected " + expected.Length + " bytes, but get " + ret + " [" + msg + "]");
                return -1;
            }
            else
            {
                return 0;
            }
        }

        internal bool CheckCTS()
        {
            byte[] buffer = new byte[2];
            int ret = SetControlCommandIn(CH341_REQ_READ_REG, 0x0706, 0, buffer);

            if (ret != 2)
            {
                Log.Info(CLASS_ID, "Expected 2 bytes, but get " + ret);
                return false;
            }

            if ((buffer[0] & 0x01) == 0x00) //CTS ON
            {
                return true;
            }
            else // CTS OFF
            {
                return false;
            }
        }

        internal bool CheckDSR()
        {
            byte[] buffer = new byte[2];
            int ret = SetControlCommandIn(CH341_REQ_READ_REG, 0x0706, 0, buffer);

            if (ret != 2)
            {
                Log.Info(CLASS_ID, "Expected 2 bytes, but get " + ret);
                return false;
            }

            if ((buffer[0] & 0x02) == 0x00) //DSR ON
            {
                return true;
            }
            else // DSR OFF
            {
                return false;
            }
        }

        private int WriteHandshakeByte()
        {
            if (SetControlCommandOut(0xa4, ~((dtr ? 1 << 5 : 0) | (rts ? 1 << 6 : 0)), 0, null) < 0)
            {
                Log.Info(CLASS_ID, "Failed to set handshake byte");
                return -1;
            }
            return 0;
        }

        private int SetControlCommandOut(int request, int value, int index, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }
            int response = connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request, value, index, data, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, "Control Transfer Response: " + response.ToString());
            return response;
        }

        private int SetControlCommandIn(int request, int value, int index, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }
            int response = connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_FROM_DEVICE, request, value, index, data, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", REQTYPE_HOST_FROM_DEVICE, request, value, index, hexData.BytesToString(data), response));

            return response;
        }

        private void CreateFlowControlThread()
        {
            flowControlThread = new FlowControlThreadCH34x(this);
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

        public override void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            // TODO
        }
    }

    internal class FlowControlThreadCH34x : AbstractWorkerThread
    {
        private long time = 100; // 100ms

        private readonly object LockObject = new object();

        private CH34xSerialDevice device;

        public FlowControlThreadCH34x(CH34xSerialDevice device)
        {
            this.device = device;
        }

        public override void DoRun()
        {
            if (!firstTime)
            {
                // Check CTS status
                if (device.rtsCtsEnabled)
                {
                    bool cts = PollForCTS();
                    if (device.ctsState != cts)
                    {
                        device.ctsState = !device.ctsState;
                        device.ctsCallback?.OnCTSChanged(device.ctsState);
                    }
                }

                // Check DSR status
                if (device.dtrDsrEnabled)
                {
                    bool dsr = PollForDSR();
                    if (device.dsrState != dsr)
                    {
                        device.dsrState = !device.dsrState;
                        device.dsrCallback?.OnDSRChanged(device.dsrState);
                    }
                }
            }
            else
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

        public bool PollForCTS()
        {
            //lock (this)
            using (LockObject.Lock(5000))
            {
                try
                {
                    Wait(time);
                }
                catch (Java.Lang.InterruptedException e)
                {
                    Log.Info(CH34xSerialDevice.CLASS_ID, e.Message);
                }
            }

            return device.CheckCTS();
        }

        public bool PollForDSR()
        {
            //lock (this)
            using (LockObject.Lock(5000))
            {
                try
                {
                    Wait(time);
                }
                catch (Java.Lang.InterruptedException e)
                {
                    Log.Info(CH34xSerialDevice.CLASS_ID, e.Message);
                }
            }
            return device.CheckDSR();
        }
    }
}