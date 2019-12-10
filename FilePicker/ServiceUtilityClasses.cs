using Android.Content;
using Android.Hardware.Usb;

using com.felhr.usbserial;

using System;

namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="ConnectionThread" />
    /// </summary>
    public class ConnectionThread : Java.Lang.Thread
    {
        #region Fields

        private LGUsbService usbService;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionThread"/> class.
        /// </summary>
        /// <param name="usbService">The usbService<see cref="LGUsbService"/></param>
        public ConnectionThread(LGUsbService usbService)
        {
            this.usbService = usbService;
        }

        #endregion Constructors

        #region Methods

        private void DoOpenActions(bool isAsync)
        {
            usbService.serialPortConnected = true;
            usbService.serialPort.SetBaudRate(UsbSerialInterface.BAUD_RATE_9600);
            usbService.serialPort.SetDataBits(UsbSerialInterface.DATA_BITS_8);
            usbService.serialPort.SetStopBits(UsbSerialInterface.STOP_BITS_1);
            usbService.serialPort.SetParity(UsbSerialInterface.PARITY_NONE);
            /**
                * Current flow control Options:
                * UsbSerialInterface.FLOW_CONTROL_OFF
                * UsbSerialInterface.FLOW_CONTROL_RTS_CTS only for CP2102 and FT232
                * UsbSerialInterface.FLOW_CONTROL_DSR_DTR only for CP2102 and FT232
                */
            usbService.serialPort.SetFlowControl(UsbSerialInterface.FLOW_CONTROL_OFF);
            usbService.serialPort.GetCTS(usbService.ctsCallback);
            usbService.serialPort.GetDSR(usbService.dsrCallback);

            if(isAsync)
                usbService.serialPort.Read(usbService.mCallback);
            else
            {
                usbService.syncreadThread = new ReadThread(usbService);
                usbService.syncreadThread.Start();
            }
            //
            // Some Arduinos would need some sleep because firmware wait some time to know whether a new sketch is going
            // to be uploaded or not
            //Thread.sleep(2000); // sleep some. YMMV with different chips.

            // Everything went as expected. Send an intent to MainActivity
            Intent intent = new Intent(ServiceConstants.ACTION_USB_READY);
            usbService.context.SendBroadcast(intent);
        }

        //private void DoSyncOpenActions()
        //{
        //    usbService.serialPortConnected = true;
        //    usbService.serialPort.SetBaudRate(UsbSerialInterface.BAUD_RATE_9600);
        //    usbService.serialPort.SetDataBits(UsbSerialInterface.DATA_BITS_8);
        //    usbService.serialPort.SetStopBits(UsbSerialInterface.STOP_BITS_1);
        //    usbService.serialPort.SetParity(UsbSerialInterface.PARITY_NONE);
        //    /**
        //        * Current flow control Options:
        //        * UsbSerialInterface.FLOW_CONTROL_OFF
        //        * UsbSerialInterface.FLOW_CONTROL_RTS_CTS only for CP2102 and FT232
        //        * UsbSerialInterface.FLOW_CONTROL_DSR_DTR only for CP2102 and FT232
        //        */
        //    usbService.serialPort.SetFlowControl(UsbSerialInterface.FLOW_CONTROL_OFF);
        //    //serialPort.Read(usbService.mCallback);
        //    usbService.serialPort.GetCTS(usbService.ctsCallback);
        //    usbService.serialPort.GetDSR(usbService.dsrCallback);


        //    usbService.syncreadThread = new ReadThread(usbService);
        //    usbService.syncreadThread.Start();
        //    //
        //    // Some Arduinos would need some sleep because firmware wait some time to know whether a new sketch is going
        //    // to be uploaded or not
        //    //Thread.sleep(2000); // sleep some. YMMV with different chips.

        //    // Everything went as expected. Send an intent to MainActivity
        //    Intent intent = new Intent(ServiceConstants.ACTION_USB_READY);
        //    usbService.context.SendBroadcast(intent);
        //}

        /// <summary>
        /// The Run
        /// </summary>
        public override void Run()
        {
            usbService.serialPort = UsbSerialDevice.CreateUsbSerialDevice(usbService.device, usbService.connection);
            //serialPort = null;// UsbSerialDevice.CreateUsbSerialDevice(usbService.device, usbService.connection);

            if (usbService.serialPort != null)
            {
                if (LNG.CommonFramework.StaticMethods.IsAsync)
                {
                    if (usbService.serialPort.Open())
                    {
                        DoOpenActions(true);
                    }
                    else
                    {
                        // Serial port could not be opened, maybe an I/O error or if CDC driver was chosen, it does not really fit
                        // Send an Intent to Main Activity

                        if (usbService.serialPort is CDCSerialDevice)
                        {
                            Intent intent = new Intent(ServiceConstants.ACTION_CDC_DRIVER_NOT_WORKING);
                            usbService.context.SendBroadcast(intent);
                        }
                        else
                        {
                            Intent intent = new Intent(ServiceConstants.ACTION_USB_DEVICE_NOT_WORKING);
                            usbService.context.SendBroadcast(intent);
                        }
                    }
                }
                else if (usbService.serialPort.SyncOpen())
                {
                    DoOpenActions(false);
                }
                else
                {
                    // Serial port could not be opened, maybe an I/O error or if CDC driver was chosen, it does not really fit
                    // Send an Intent to Main Activity
                    if (usbService.serialPort is CDCSerialDevice)
                    {
                        Intent intent = new Intent(ServiceConstants.ACTION_CDC_DRIVER_NOT_WORKING);
                        usbService.context.SendBroadcast(intent);
                    }
                    else
                    {
                        Intent intent = new Intent(ServiceConstants.ACTION_USB_DEVICE_NOT_WORKING);
                        usbService.context.SendBroadcast(intent);
                    }
                }
            }
            else
            {
                // No driver for given device, even generic CDC driver could not be loaded
                Intent intent = new Intent(ServiceConstants.ACTION_USB_NOT_SUPPORTED);
                usbService.context.SendBroadcast(intent);
            }
        }

        #endregion Methods
    }

    /// <summary>
    /// Defines the <see cref="LGServiceBroadcastReceiver" />
    /// </summary>
    public class LGServiceBroadcastReceiver : BroadcastReceiver
    {
        #region Fields

        private LGUsbService usbService;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LGServiceBroadcastReceiver"/> class.
        /// </summary>
        /// <param name="usbService">The usbService<see cref="LGUsbService"/></param>
        public LGServiceBroadcastReceiver(LGUsbService usbService)
        {
            this.usbService = usbService;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// The OnReceive
        /// </summary>
        /// <param name="arg0">The arg0<see cref="Context"/></param>
        /// <param name="arg1">The arg1<see cref="Intent"/></param>
        public override void OnReceive(Context arg0, Intent arg1)
        {
            Intent intent = null;
            switch (arg1.Action)
            {
                case ServiceConstants.ACTION_USB_PERMISSION:
                    bool granted = arg1.Extras.GetBoolean(UsbManager.ExtraPermissionGranted);
                    if (granted) // User accepted our USB connection. Try to open the device as a serial port
                    {
                        intent = new Intent(ServiceConstants.ACTION_USB_PERMISSION_GRANTED);
                        arg0.SendBroadcast(intent);
                        usbService.connection = usbService.usbManager.OpenDevice(usbService.device);
                        new ConnectionThread(usbService).Start();
                    }
                    else // User not accepted our USB connection. Send an Intent to the Main Activity
                    {
                        intent = new Intent(ServiceConstants.ACTION_USB_PERMISSION_NOT_GRANTED);
                        arg0.SendBroadcast(intent);
                    }
                    break;

                case ServiceConstants.ACTION_USB_ATTACHED:
                    if (!usbService.serialPortConnected && !usbService.isFirstRun)
                    {
                        usbService.FindSerialPortDevice(); // A USB device has been attached. Try to open it as a Serial port
                    }

                    break;

                case ServiceConstants.ACTION_USB_DETACHED:
                    // Usb device was disconnected. send an intent to the Main Activity
                    intent = new Intent(ServiceConstants.ACTION_USB_DISCONNECTED);
                    arg0.SendBroadcast(intent);
                    if (usbService.serialPortConnected)
                    {
                        if (LNG.CommonFramework.StaticMethods.IsAsync)
                        {
                            usbService.serialPort.Close();
                        }
                        else
                        {
                            usbService.serialPort.SyncClose();
                        }
                    }
                    if (usbService.syncreadThread != null)
                    {
                        usbService.syncreadThread.runThread = false;
                    }

                    usbService.serialPortConnected = false;
                    usbService.serialPort = null;
                    break;

                case ServiceConstants.ACTION_OPEN_SERIAL_PORT:
                    if (!usbService.serialPortConnected && usbService.serialPort != null)
                    {
                        if (LNG.CommonFramework.StaticMethods.IsAsync)
                        {
                            usbService.serialPort.Open();
                        }
                        else
                        {
                            usbService.serialPort.SyncOpen();
                        }
                    }
                    usbService.serialPortConnected = true;
                    break;

                case ServiceConstants.ACTION_CLOSE_SERIAL_PORT:
                    if (usbService.serialPortConnected)
                    {
                        if (LNG.CommonFramework.StaticMethods.IsAsync)
                        {
                            usbService.serialPort.Close();
                        }
                        else
                        {
                            usbService.serialPort.SyncClose();
                        }
                    }
                    usbService.serialPortConnected = false;
                    break;

                case ServiceConstants.ACTION_SET_BAUD_RATE:
                    usbService.serialPort.SetBaudRate(arg1.Extras.GetInt(ServiceConstants.TAG_BAUD_RATE));
                    break;

                case "LNG.CMRI.EXIT":
                    usbService.StopService();
                    break;

                default:
                    break;
            }
        }

        #endregion Methods
    }

    /// <summary>
    /// Defines the <see cref="ReadThread" />
    /// </summary>
    public class ReadThread : Java.Lang.Thread
    {
        #region Constants

        private const int MaxRecieveLength = 1024;

        #endregion Constants

        #region Fields

        public volatile bool runThread = true;
#if DEBUG
        private static bool debugging = false;
#else
        private static bool debugging = false;
#endif
        private byte[] buffer = new byte[64];
        private LGUsbService usbService;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadThread"/> class.
        /// </summary>
        /// <param name="usbService">The usbService<see cref="LGUsbService"/></param>
        public ReadThread(LGUsbService usbService)
        {
            this.usbService = usbService;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// The Run
        /// </summary>
        public override void Run()
        {
            while (runThread)
            {
                Array.Clear(buffer, 0, buffer.Length);
                int n = usbService.serialPort.SyncRead(buffer, 1500);
                if (n > 0)
                {
                    byte[] received = new byte[n];
                    Buffer.BlockCopy(buffer, 0, received, 0, n);
                    if (debugging)
                    {
                        UsbSerialDebugger.PrintReadLogPut(received, true);
                    }
                    usbService.mCallback.OnReceivedData(received);
                }
                //System.Threading.Thread.Sleep(10);
            }
        }

        #endregion Methods
    }
}