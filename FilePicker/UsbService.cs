using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Support.V4.App;

using com.felhr.usbserial;
using LNG.CommonFramework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="LGUsbService" />
    /// </summary>
    [Service(Name = "com.landisgyr.cmri.UsbService")]
    public class LGUsbService : Service, SerialPortCallback
    {
        private const string CHANNEL_ID = "LGCMRI_Notification_Channel";

        private const string defaulttext = "Service is running...";

        private const string prefix = "Landis+Gyr CMRI";

        private static int Notification_ID = StaticVariables.Notification_ID;

        private NotificationCompat.BigTextStyle bigTextStyle;
        private PendingIntent exitPendingIntent;
        private NotificationCompat.Builder mBuilder;
        private Notification mNotify;
        private NotificationManager notificationManager;
        private List<UsbSerialDevice> serialPorts;

        /// <summary>
        /// The CreateNotificationChannel
        /// </summary>
        private void CreateNotificationChannel()
        {
            // Create the NotificationChannel, but only on API 26+ because
            // the NotificationChannel class is new and not in the support library
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                string name = GetString(Resource.String.channel_name);
                string description = GetString(Resource.String.channel_description);

                NotificationChannel channel = new NotificationChannel(CHANNEL_ID, name, NotificationImportance.Max)
                {
                    Description = description,
                    LightColor = Android.Graphics.Color.SkyBlue,
                    LockscreenVisibility = NotificationVisibility.Public
                };
                channel.SetShowBadge(false);
                //channel.SetSound(null, null);
                // Register the channel with the system; you can't change the importance
                // or other notification behaviors after this

                notificationManager.CreateNotificationChannel(channel);
            }
        }

        /// <summary>
        /// The RequestUserPermission
        /// </summary>
        private void RequestUserPermission()
        {
            PendingIntent mPendingIntent = PendingIntent.GetBroadcast(this, 0, new Intent(ServiceConstants.ACTION_USB_PERMISSION), 0);
            usbManager.RequestPermission(device, mPendingIntent);
        }

        /// <summary>
        /// The SetFilter
        /// </summary>
        private void SetFilter()
        {
            IntentFilter filter = new IntentFilter();
            filter.AddAction(ServiceConstants.ACTION_USB_PERMISSION);
            filter.AddAction(ServiceConstants.ACTION_USB_DETACHED);
            filter.AddAction(ServiceConstants.ACTION_USB_ATTACHED);
            filter.AddAction(ServiceConstants.ACTION_OPEN_SERIAL_PORT);
            filter.AddAction(ServiceConstants.ACTION_CLOSE_SERIAL_PORT);
            filter.AddAction(ServiceConstants.ACTION_SET_BAUD_RATE);
            filter.AddAction("LNG.CMRI.EXIT");
            RegisterReceiver(usbReceiver, filter);
        }

        internal IBinder binder;
        internal UsbDeviceConnection connection;
        internal Context context;
        internal UsbCTSCallback ctsCallback;
        internal UsbDevice device;
        internal UsbDSRCallback dsrCallback;
        internal int index = 0;
        internal UsbReadCallback mCallback;
        internal Handler mHandler;
        internal UsbSerialDevice serialPort;
        internal bool serialPortConnected;
        internal ReadThread syncreadThread;
        internal UsbManager usbManager;
        internal BroadcastReceiver usbReceiver;

        //internal SerialPortBuilder builder;
        public static bool SERVICE_CONNECTED = false;

        public bool isFirstRun = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="LGUsbService"/> class.
        /// </summary>
        public LGUsbService()
        {
            //binder = new UsbBinder(this);
            mCallback = new UsbReadCallback(ref mHandler);
            ctsCallback = new UsbCTSCallback(ref mHandler);
            dsrCallback = new UsbDSRCallback(ref mHandler);
            usbReceiver = new LGServiceBroadcastReceiver(this);
        }

        /// <summary>
        /// The ChangeBaudRate
        /// </summary>
        /// <param name="baudRate">The baudRate<see cref="int"/></param>
        public void ChangeBaudRate(int baudRate)
        {
            serialPort?.SetBaudRate(baudRate);
        }

        /// <summary>
        /// The FindSerialPortDevice
        /// </summary>
        public void FindSerialPortDevice()
        {
            isFirstRun = false;
            // This snippet will try to open the first encountered usb device connected, excluding usb root hubs
            IDictionary<string, UsbDevice> usbDevices = usbManager.DeviceList;
            if (usbDevices.Count > 0)
            {
                bool keep = true;
                foreach (KeyValuePair<string, UsbDevice> entry in usbDevices)
                {
                    device = entry.Value;
                    int deviceVID = device.VendorId;
                    int devicePID = device.ProductId;

                    Android.Util.Log.Info("USB DEVICE CONNECTED", " VID: " + deviceVID + " PID: " + devicePID);
                    Android.Util.Log.Info("USB DEVICE CONNECTED", " VID: " + deviceVID.ToString("X2") + " PID: " + devicePID.ToString("X2"));

                    //if (deviceVID != 0x1d6b && (devicePID != 0x0001 && devicePID != 0x0002 && devicePID != 0x0003) && deviceVID != 0x5c6 && devicePID != 0x904c)
                    if (deviceVID != 0x1d6b && devicePID != 0x0001 && devicePID != 0x0002 && devicePID != 0x0003)
                    {
                        // There is a device connected to our Android device. Try to open it as a Serial Port.
                        RequestUserPermission();
                        keep = false;
                    }
                    else
                    {
                        connection = null;
                        device = null;
                    }

                    if (!keep)
                    {
                        break;
                    }
                }
                if (!keep)
                {
                    // There is no USB devices connected (but usb host were listed). Send an intent to MainActivity.
                    Intent intent = new Intent(ServiceConstants.ACTION_NO_USB);
                    SendBroadcast(intent);
                }
            }
            else
            {
                // There is no USB devices connected. Send an intent to MainActivity
                Intent intent = new Intent(ServiceConstants.ACTION_NO_USB);
                SendBroadcast(intent);
            }
        }

        /// <summary>
        /// The OnBind
        /// </summary>
        /// <param name="intent">The intent<see cref="Intent"/></param>
        /// <returns>The <see cref="IBinder"/></returns>
        public override IBinder OnBind(Intent intent)
        {
            binder = new SerialBinder(this);
            return binder;
        }

        /// <summary>
        /// The OnCreate
        /// </summary>
        public override void OnCreate()
        {
            context = this;
            serialPortConnected = false;

            Intent intent = PackageManager
                                    .GetLaunchIntentForPackage(PackageName)
                                    .SetPackage(null)
                                    .SetFlags(ActivityFlags.NewTask | ActivityFlags.ResetTaskIfNeeded);

            PendingIntent pendingIntent = PendingIntent.GetActivity(context, 0, intent, 0);

            exitPendingIntent = PendingIntent.GetBroadcast(this, 0, new Intent("LNG.CMRI.EXIT"), 0);

            bigTextStyle = new NotificationCompat.BigTextStyle();
            bigTextStyle.SetBigContentTitle(prefix);
            bigTextStyle.BigText(defaulttext);

            mBuilder = new NotificationCompat.Builder(this, CHANNEL_ID)
                                    .SetSmallIcon(Resource.Drawable.ic_gas_meter_free_icon_3)
                                    .SetContentTitle("L+G CMRI")
                                    .SetContentText("CMRI Service Started")
                                    .SetStyle(bigTextStyle)
                                    //.SetWhen(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                                    //.SetAutoCancel(false)
                                    .SetOnlyAlertOnce(true)
                                    .SetProgress(0, 0, false)
                                    .SetPriority((int)NotificationPriority.Max)
                                    .SetOngoing(true)
                                    .SetContentIntent(pendingIntent)
                                    //.AddAction(Resource.Drawable.ic_mtrl_chip_close_circle, "EXIT APPLICATION", exitPendingIntent)
                                    //.SetFullScreenIntent(pendingIntent,true)
                                    ;

            notificationManager = GetSystemService(Context.NotificationService) as NotificationManager;
            CreateNotificationChannel();

            mNotify = mBuilder.Build();

            StartForeground(Notification_ID, mNotify);

            SERVICE_CONNECTED = true;
            SetFilter();
            usbManager = (UsbManager)GetSystemService(Context.UsbService);

            FindSerialPortDevice();
        }

        /// <summary>
        /// The OnDestroy
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            UnregisterReceiver(usbReceiver);
            //if (builder != null)
            //    builder.UnregisterListeners(context);
            SERVICE_CONNECTED = false;
            Android.Util.Log.Info("LNG.CMRI.LGUsbService.OnDestroy", "LGUsbService Stopped");
        }

        /// <summary>
        /// The onSerialPortsDetected
        /// </summary>
        /// <param name="serialPorts">The serialPorts<see cref="List{UsbSerialDevice}"/></param>
        public void OnSerialPortsDetected(List<UsbSerialDevice> serialPorts)
        {
            this.serialPorts = serialPorts;
            if (serialPorts.Count > 0)
            {
                if (serialPort == null)
                {
                    serialPort = serialPorts[0];
                }

                if (!serialPorts.Contains(serialPort))
                {
                    serialPort = serialPorts[0];
                }
            }
        }

        /// <summary>
        /// The OnStartCommand
        /// </summary>
        /// <param name="intent">The intent<see cref="Intent"/></param>
        /// <param name="flags">The flags<see cref="StartCommandFlags"/></param>
        /// <param name="startId">The startId<see cref="int"/></param>
        /// <returns>The <see cref="StartCommandResult"/></returns>
        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            return StartCommandResult.NotSticky;
        }

        /// <summary>
        /// The SelectSerialPort
        /// </summary>
        /// <param name="index">The index<see cref="int"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public bool SelectSerialPort(int index)
        {
            bool success = false;

            if (serialPorts?.Count > 0 && serialPorts.Count > index)
            {
                serialPort = serialPorts[index];
                success = true;
            }

            return success;
        }

        /// <summary>
        /// The SetHandler
        /// </summary>
        /// <param name="handler">The handler<see cref="Handler"/></param>
        public void SetHandler(Handler handler)
        {
            mHandler = handler;
            mCallback.mHandler = mHandler;
            ctsCallback.mHandler = mHandler;
            dsrCallback.mHandler = mHandler;
        }

        /// <summary>
        /// The StopService
        /// </summary>
        public void StopService()
        {
            StopSelf();
            SERVICE_CONNECTED = false;
            Android.Util.Log.Info("LNG.CMRI.LGUsbService.StopService", "LGUsbService Stopped");
        }

        /// <summary>
        /// The UpdateNotification
        /// </summary>
        /// <param name="title">The title<see cref="string"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="bShowProgressBar">The bShowProgressBar<see cref="bool"/></param>
        public void UpdateNotification(string title, string message, bool bShowProgressBar)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (title != null)
                {
                    bigTextStyle.SetBigContentTitle(prefix + " : " + title);
                }

                if (message != null)
                {
                    bigTextStyle.BigText(string.IsNullOrWhiteSpace(message) ? defaulttext : message);
                }

                mBuilder.SetProgress(0, 0, bShowProgressBar)
                        .SetStyle(bigTextStyle)
                        ;
            }
            else
            {
                if (title != null)
                {
                    mBuilder.SetContentTitle(prefix + " : " + title);
                }

                if (message != null)
                {
                    mBuilder.SetContentText(string.IsNullOrWhiteSpace(message) ? defaulttext : message);
                }
            }
            notificationManager.Notify(Notification_ID, mBuilder.Build());
        }

        /// <summary>
        /// The Write
        /// </summary>
        /// <param name="data">The data<see cref="byte[]"/></param>
        public void Write(byte[] data)
        {
            serialPort?.Write(data);
        }
    }
}