using System;

using Android.Hardware.Usb;
using Android.Util;
using com.felhr.utils;
using USBSerialLibrary.utils;

namespace com.felhr.usbserial
{
    public class PL2303SerialDevice : UsbSerialDevice
    {
        private HexData hexData;
        private const string CLASS_ID = nameof(PL2303SerialDevice);
        private const int USB_RECIP_INTERFACE = 0x01;
        private const int PL2303_REQTYPE_HOST2DEVICE_VENDOR = 0x40;
        private const int PL2303_REQTYPE_DEVICE2HOST_VENDOR = 0xC0;
        private const int PL2303_REQTYPE_HOST2DEVICE = 0x21;

        private const int PL2303_VENDOR_WRITE_REQUEST = 0x01;
        private const int PL2303_VENDOR_READ_REQUEST = 0x01;
        private const int PL2303_SET_LINE_CODING = 0x20;
        private const int PL2303_SET_CONTROL_REQUEST = 0x22;

        private const int PROLIFIC_VENDOR_OUT_REQTYPE = UsbSupport.UsbDirOut
                                                         | UsbConstants.UsbTypeVendor;

        private const int PROLIFIC_VENDOR_IN_REQTYPE = UsbSupport.UsbDirIn
                                                        | UsbConstants.UsbTypeVendor;

        private const int PROLIFIC_CTRL_OUT_REQTYPE = UsbSupport.UsbDirOut
                                                       | UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        private const int WRITE_ENDPOINT = 0x02;
        private const int READ_ENDPOINT = 0x83;
        private const int INTERRUPT_ENDPOINT = 0x81;

        private const int FLUSH_RX_REQUEST = 0x08;
        private const int FLUSH_TX_REQUEST = 0x09;

        private const int CONTROL_DTR = 0x01;
        private const int CONTROL_RTS = 0x02;

        private const int STATUS_FLAG_CD = 0x01;
        private const int STATUS_FLAG_DSR = 0x02;
        private const int STATUS_FLAG_RI = 0x08;
        private const int STATUS_FLAG_CTS = 0x80;

        private const int STATUS_BUFFER_SIZE = 10;
        private const int STATUS_BYTE_IDX = 8;

        private readonly byte[] defaultSetLine = new byte[]{
            (byte) 0x80, // [0:3] Baud rate (reverse hex encoding 9600:00 00 25 80 -> 80 25 00 00)
            (byte) 0x25,
            (byte) 0x00,
            (byte) 0x00,
            (byte) 0x00, // [4] Stop Bits (0=1, 1=1.5, 2=2)
            (byte) 0x00, // [5] Parity (0=NONE 1=ODD 2=EVEN 3=MARK 4=SPACE)
            (byte) 0x08  // [6] Data Bits (5=5, 6=6, 7=7, 8=8)
    };

        private readonly UsbInterface mInterface;
        private UsbEndpoint inEndpoint;
        private UsbEndpoint outEndpoint;
        private const int DEVICE_TYPE_HX = 0;
        private const int DEVICE_TYPE_0 = 1;
        private const int DEVICE_TYPE_1 = 2;

        private int mDeviceType = DEVICE_TYPE_HX;
        private int mControlLinesValue = 0;

        public PL2303SerialDevice(UsbDevice device, UsbDeviceConnection connection) : this(device, connection, -1)
        {
            hexData = new HexData();
        }

        public PL2303SerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            hexData = new HexData();
            if (iface > 1)
            {
                throw new ArgumentException("Multi-interface PL2303 devices not supported!");
            }

            mInterface = device.GetInterface(iface >= 0 ? iface : 0);
        }

        public override bool Open()
        {
            bool ret = OpenPL2303();

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
            KillWorkingThread();
            KillWriteThread();
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override bool SyncOpen()
        {
            bool ret = OpenPL2303();
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
            connection.ReleaseInterface(mInterface);
            IsOpen = false;
        }

        public override void SetBaudRate(int baudRate)
        {
            byte[] tempBuffer = new byte[4];
            tempBuffer[0] = (byte)(baudRate & 0xff);
            tempBuffer[1] = (byte)((baudRate >> 8) & 0xff);
            tempBuffer[2] = (byte)((baudRate >> 16) & 0xff);
            tempBuffer[3] = (byte)((baudRate >> 24) & 0xff);
            if (tempBuffer[0] != defaultSetLine[0] || tempBuffer[1] != defaultSetLine[1] || tempBuffer[2] != defaultSetLine[2]
                    || tempBuffer[3] != defaultSetLine[3])
            {
                defaultSetLine[0] = tempBuffer[0];
                defaultSetLine[1] = tempBuffer[1];
                defaultSetLine[2] = tempBuffer[2];
                defaultSetLine[3] = tempBuffer[3];
                if(SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine) >= 0)
                {
                    CurrentBaudRate = baudRate;
                }
            }
            else
            {
                CurrentBaudRate = baudRate;
            }
        }

        public override void SetDataBits(int dataBits)
        {
            switch (dataBits)
            {
                case UsbSerialInterface.DATA_BITS_5:
                    if (defaultSetLine[6] != 0x05)
                    {
                        defaultSetLine[6] = 0x05;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.DATA_BITS_6:
                    if (defaultSetLine[6] != 0x06)
                    {
                        defaultSetLine[6] = 0x06;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.DATA_BITS_7:
                    if (defaultSetLine[6] != 0x07)
                    {
                        defaultSetLine[6] = 0x07;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.DATA_BITS_8:
                    if (defaultSetLine[6] != 0x08)
                    {
                        defaultSetLine[6] = 0x08;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                default:
                    return;
            }
        }

        public override void SetStopBits(int stopBits)
        {
            switch (stopBits)
            {
                case UsbSerialInterface.STOP_BITS_1:
                    if (defaultSetLine[4] != 0x00)
                    {
                        defaultSetLine[4] = 0x00;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.STOP_BITS_15:
                    if (defaultSetLine[4] != 0x01)
                    {
                        defaultSetLine[4] = 0x01;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.STOP_BITS_2:
                    if (defaultSetLine[4] != 0x02)
                    {
                        defaultSetLine[4] = 0x02;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                default:
                    return;
            }
        }

        public override void SetParity(int parity)
        {
            switch (parity)
            {
                case UsbSerialInterface.PARITY_NONE:
                    if (defaultSetLine[5] != 0x00)
                    {
                        defaultSetLine[5] = 0x00;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.PARITY_ODD:
                    if (defaultSetLine[5] != 0x01)
                    {
                        defaultSetLine[5] = 0x01;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.PARITY_EVEN:
                    if (defaultSetLine[5] != 0x02)
                    {
                        defaultSetLine[5] = 0x02;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.PARITY_MARK:
                    if (defaultSetLine[5] != 0x03)
                    {
                        defaultSetLine[5] = 0x03;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                case UsbSerialInterface.PARITY_SPACE:
                    if (defaultSetLine[5] != 0x04)
                    {
                        defaultSetLine[5] = 0x04;
                        SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine);
                    }
                    break;

                default:
                    return;
            }
        }

        public override void SetFlowControl(int flowControl)
        {
            // TODO
        }

        public  bool GetRTS()
        {
            return (mControlLinesValue & CONTROL_RTS) == CONTROL_RTS;
        }

        public override void SetRTS(bool state)
        {
            int newControlLinesValue;
            if (state)
            {
                newControlLinesValue = mControlLinesValue | CONTROL_RTS;
            }
            else
            {
                newControlLinesValue = mControlLinesValue & ~CONTROL_RTS;
            }

            SetControlCommand(PROLIFIC_CTRL_OUT_REQTYPE, PL2303_SET_CONTROL_REQUEST, newControlLinesValue, 0, null);
            mControlLinesValue = newControlLinesValue;
        }

        public  bool GetDTR()
        {
            return (mControlLinesValue & CONTROL_DTR) == CONTROL_DTR;
        }

        public override void SetDTR(bool state)
        {
            int newControlLinesValue;
            if (state)
            {
                newControlLinesValue = mControlLinesValue | CONTROL_DTR;
            }
            else
            {
                newControlLinesValue = mControlLinesValue & ~CONTROL_DTR;
            }
            SetControlCommand(PROLIFIC_CTRL_OUT_REQTYPE, PL2303_SET_CONTROL_REQUEST, newControlLinesValue, 0, null);
            mControlLinesValue = newControlLinesValue;
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

        private bool OpenPL2303()
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

            if (device.DeviceClass == (UsbClass)0x02)
            {
                mDeviceType = DEVICE_TYPE_0;
            }
            else
            {
                try
                {
                    //Method getRawDescriptorsMethod
                    //    = mConnection.getClass().getMethod("getRawDescriptors");
                    //byte[] rawDescriptors
                    //    = (byte[])getRawDescriptorsMethod.invoke(mConnection);

                    byte[] rawDescriptors = connection.GetRawDescriptors();

                    byte maxPacketSize0 = rawDescriptors[7];
                    if (maxPacketSize0 == 64)
                    {
                        mDeviceType = DEVICE_TYPE_HX;
                    }
                    else if ((device.DeviceClass == 0x00)
                             || (device.DeviceClass == (UsbClass)0xff))
                    {
                        mDeviceType = DEVICE_TYPE_1;
                    }
                    else
                    {
                        Log.Warn("com.felhr.usbserial.PL2303SerialDevice.OpenPL2303", "Could not detect PL2303 subtype, "
                                      + "Assuming that it is a HX device");
                        mDeviceType = DEVICE_TYPE_HX;
                    }
                }
                catch (Java.Lang.NoSuchMethodException)
                {
                    Log.Warn("com.felhr.usbserial.PL2303SerialDevice.OpenPL2303", "Method UsbDeviceConnection.getRawDescriptors, "
                                  + "required for PL2303 subtype detection, not "
                                  + "available! Assuming that it is a HX device");
                    mDeviceType = DEVICE_TYPE_HX;
                }
                catch (Exception e)
                {
                    Log.Error("com.felhr.usbserial.PL2303SerialDevice.OpenPL2303", "An unexpected exception occured while trying "
                                   + "to detect PL2303 subtype", e);
                }
            }

            //Default Setup
            byte[] buf = new byte[1];
            //Specific vendor stuff that I barely understand but It is on linux drivers, So I trust :)
            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8484, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0404, 0, null) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8484, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8383, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8484, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0404, 1, null) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8484, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_DEVICE2HOST_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x8383, 0, buf) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0000, 1, null) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0001, 0, null) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0002, (mDeviceType == DEVICE_TYPE_HX) ? 0x0044 : 0x0024, null) < 0)
            {
                return false;
            }
            // End of specific vendor stuff
            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_CONTROL_REQUEST, 0x0003, 0, null) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE, PL2303_SET_LINE_CODING, 0x0000, 0, defaultSetLine) < 0)
            {
                return false;
            }

            if (SetControlCommand(PL2303_REQTYPE_HOST2DEVICE_VENDOR, PL2303_VENDOR_WRITE_REQUEST, 0x0505, 0x1311, null) < 0)
            {
                return false;
            }

            PurgeHwBuffers(true, true);

            return true;
        }

        private int SetControlCommand(int reqType, int request, int value, int index, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }

            int response = connection.ControlTransfer((UsbAddressing)reqType, request, value, index, data, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, String.Format("Control Transfer Command: reqType: {0} req: {1} value: {2} index: {3} data: {4} || Response: {5}", reqType, request, value, index, hexData.BytesToString(data), response));
            return response;
        }

        public override void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (purgeReadBuffers)
            {
                SetControlCommand(PROLIFIC_VENDOR_OUT_REQTYPE, PL2303_VENDOR_WRITE_REQUEST, FLUSH_RX_REQUEST, 0, null);
            }

            if (purgeWriteBuffers)
            {
                SetControlCommand(PROLIFIC_VENDOR_OUT_REQTYPE, PL2303_VENDOR_WRITE_REQUEST, FLUSH_TX_REQUEST, 0, null);
            }
        }
    }
}