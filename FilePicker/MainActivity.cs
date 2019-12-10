using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using System.IO;
using Java.Lang;
using AlertDialog = Android.App.AlertDialog;
using Android.Content;
using Android.Animation;
using Android.Bluetooth;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Hardware.Usb;
using Android.Support.Constraints;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Views;
using com.felhr.usbserial;
using com.felhr.utils;
using LNG.CMRI.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exception = System.Exception;

namespace FilePicker
{
    /// <summary>
    /// Defines the <see cref="CrossfadeAnimatorListenerAdapter" />
    /// </summary>
    public class CrossfadeAnimatorListenerAdapter : AnimatorListenerAdapter
    {
        private View viewOut;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossfadeAnimatorListenerAdapter"/> class.
        /// </summary>
        /// <param name="view">The view<see cref="View"/></param>
        public CrossfadeAnimatorListenerAdapter(View view)
        {
            viewOut = view;
        }

        /// <summary>
        /// The OnAnimationEnd
        /// </summary>
        /// <param name="animation">The animation<see cref="Animator"/></param>
        public override void OnAnimationEnd(Animator animation)
        {
            viewOut.Visibility = ViewStates.Gone;
        }
    }

    /// <summary>
    /// Defines the <see cref="LGBroadcastReceiver" />
    /// </summary>
    public class LGBroadcastReceiver : BroadcastReceiver
    {
        private readonly MainActivity activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="LGBroadcastReceiver"/> class.
        /// </summary>
        /// <param name="activity">The activity<see cref="MainActivity"/></param>
        public LGBroadcastReceiver(MainActivity activity)
        {
            this.activity = activity;
        }

        /// <summary>
        /// The OnReceive
        /// </summary>
        /// <param name="context">The context<see cref="Context"/></param>
        /// <param name="intent">The intent<see cref="Intent"/></param>
        public override void OnReceive(Context context, Intent intent)
        {
            switch (intent.Action)
            {
                case ServiceConstants.ACTION_USB_PERMISSION_GRANTED: // USB PERMISSION GRANTED
                    Toast.MakeText(context, "USB Ready", ToastLength.Short).Show();
                    break;

                case ServiceConstants.ACTION_USB_PERMISSION_NOT_GRANTED: // USB PERMISSION NOT GRANTED
                    Toast.MakeText(context, "USB Permission not granted", ToastLength.Short).Show();
                    break;

                case ServiceConstants.ACTION_NO_USB: // NO USB CONNECTED
                    Toast.MakeText(context, "No USB connected", ToastLength.Short).Show();
                    break;

                case ServiceConstants.ACTION_USB_DISCONNECTED: // USB DISCONNECTED
                    Toast.MakeText(context, "USB disconnected", ToastLength.Short).Show();
                    //await activity.PopulateListAsync();
                    break;

                case ServiceConstants.ACTION_USB_NOT_SUPPORTED: // USB NOT SUPPORTED
                    Toast.MakeText(context, "USB device not supported", ToastLength.Short).Show();
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Defines the <see cref="MainActivity" />
    /// </summary>

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = false, NoHistory = false, WindowSoftInputMode = SoftInput.StateHidden | SoftInput.AdjustPan, ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        string _filePath="";
        TextView filepath;
        Button button;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            filepath = FindViewById<TextView>(Resource.Id.filePath);
            // Set our view from the "main" layout resource

            button = FindViewById<Button>(Resource.Id.filebtn);

            button.Click += (e , o) => {
                OnFileSelect();
            };
            
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected async void OnFileSelect()
        {
            try
            {
                    var crossFilePicker = Plugin.FilePicker.CrossFilePicker.Current;

                    var myResult = await crossFilePicker.PickFile();
                    System.Console.WriteLine("deepak 1                   " + myResult);
                    _filePath = myResult.FilePath;
                    System.Console.WriteLine("deepak 1                   " + _filePath);
                    if (!string.IsNullOrEmpty(myResult.FileName))//Just the file name, it doesn't has the path
                    {
                        _filePath = myResult.FilePath;
                        if (File.Exists(_filePath))
                        {
                            filepath.Text = File.ReadAllText(_filePath); ;
                        }
                    }
                else
                {
                    System.Console.WriteLine("deepak                   " + _filePath);
                    AlertDialog.Builder dialog = new AlertDialog.Builder(this);
                    AlertDialog alert = dialog.Create();
                    alert.SetTitle("Title");
                    alert.SetMessage("Simple Alert");
                    alert.SetButton("OK", (c, ev) =>
                    {
                        Intent refresh = new Intent(this, typeof(MainActivity));
                        refresh.AddFlags(ActivityFlags.NoAnimation);
                        Finish();
                        StartActivity(refresh);
                    });
                    alert.Show();
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("deepak       " + e);
            }


            ////SIR KA CODE STARTING FROM HERE
            ///



            /// <summary>
            /// The SetFilters
            /// </summary>
            private void SetFilters()
            {
                IntentFilter filter = new IntentFilter();
                filter.AddAction(ServiceConstants.ACTION_USB_PERMISSION_GRANTED);
                filter.AddAction(ServiceConstants.ACTION_NO_USB);
                filter.AddAction(ServiceConstants.ACTION_USB_DISCONNECTED);
                filter.AddAction(ServiceConstants.ACTION_USB_NOT_SUPPORTED);
                filter.AddAction(ServiceConstants.ACTION_USB_PERMISSION_NOT_GRANTED);
                RegisterReceiver(detachedReceiver, filter);
            }


            /// <summary>
            /// The StartService
            /// </summary>
            /// <param name="serviceConnection">The serviceConnection<see cref="IServiceConnection"/></param>
            /// <param name="extras">The extras<see cref="Bundle"/></param>
            private void StartUSBService(IServiceConnection serviceConnection, Bundle extras)
            {
                if (!LGUsbService.SERVICE_CONNECTED)
                {
                    Intent startService = new Intent(this, typeof(LGUsbService));
                    if (extras?.IsEmpty == false)
                    {
                        List<string> keys = (List<string>)extras.KeySet();
                        foreach (string key in keys)
                        {
                            string extra = extras.GetString(key);
                            startService.PutExtra(key, extra);
                        }
                    }
                    StartService(startService);
                }
                Intent bindingIntent = new Intent(this, typeof(LGUsbService));
                BindService(bindingIntent, serviceConnection, Bind.AutoCreate);
            }


            private void StopUSBService()
            {
                if (usbService != null)
                {
                    usbService.StopService();
                    UnbindService(usbConnection);
                    UnregisterReceiver(detachedReceiver);
                    usbService = null;
                }
            }



            /// <summary>
            /// Defines the <see cref="MyHandler" />
            /// </summary>
            private class MyHandler : Handler
            {
            private MainActivity mActivity;
            private WeakReference<MainActivity> ref_mActivity;

            /// <summary>
            /// Initializes a new instance of the <see cref="MyHandler"/> class.
            /// </summary>
            /// <param name="activity">The activity<see cref="MainActivity"/></param>
            public MyHandler(MainActivity activity)
            {
                ref_mActivity = new WeakReference<MainActivity>(activity);
                ref_mActivity.TryGetTarget(out mActivity);
            }

            /// <summary>
            /// The HandleMessage
            /// </summary>
            /// <param name="msg">The msg<see cref="Message"/></param>
            public override void HandleMessage(Message msg)
            {
                switch (msg.What)
                {
                    case ServiceConstants.MESSAGE_FROM_SERIAL_PORT:
                        byte[] recvData = (byte[])msg.Obj;
                        string data = HexDump.ToHexString(recvData) + "\n";
                        //mActivity.display.Append(data);
                        break;

                    case ServiceConstants.CTS_CHANGE:
                        Toast.MakeText(mActivity, "CTS_CHANGE", ToastLength.Long).Show();
                        break;

                    case ServiceConstants.DSR_CHANGE:
                        Toast.MakeText(mActivity, "DSR_CHANGE", ToastLength.Long).Show();
                        break;

                    case ServiceConstants.MESSAGE_STATE_CHANGE:
                        switch (msg.What)
                        {
                            case LGBluetoothService.STATE_CONNECTED:
                                //chatFrag.SetStatus(chatFrag.GetString(Resource.String.title_connected_to, chatFrag.connectedDeviceName));
                                Toast.MakeText(mActivity, "Connected", ToastLength.Short).Show();
                                break;

                            case LGBluetoothService.STATE_CONNECTING:
                                //chatFrag.SetStatus(Resource.String.title_connecting);
                                Toast.MakeText(mActivity, "Connecting...", ToastLength.Short).Show();
                                break;

                            case LGBluetoothService.STATE_LISTEN:
                                //chatFrag.SetStatus(Resource.String.not_connected);
                                Toast.MakeText(mActivity, "Listening...", ToastLength.Short).Show();
                                break;

                            case LGBluetoothService.STATE_NONE:
                                //chatFrag.SetStatus(Resource.String.not_connected);
                                Toast.MakeText(mActivity, "Not Connected", ToastLength.Short).Show();
                                break;
                        }
                        break;

                    case ServiceConstants.MESSAGE_WRITE:
                        var writeBuffer = (byte[])msg.Obj;
                        string writeMessage = Encoding.ASCII.GetString(writeBuffer);

                        writeMessage = System.BitConverter.ToString(writeBuffer);
                        break;

                    case ServiceConstants.MESSAGE_READ:
                        var readBuffer = (byte[])msg.Obj;
                        string readMessage = Encoding.ASCII.GetString(readBuffer);

                        readMessage = System.BitConverter.ToString(readBuffer);
                        break;

                    case ServiceConstants.MESSAGE_DEVICE_NAME:
                        if (mActivity != null)
                        {
                            Toast.MakeText(mActivity, $"Connected to {msg.Data.GetString(ServiceConstants.DEVICE_NAME)}.", ToastLength.Long).Show();
                        }
                        break;

                    case ServiceConstants.MESSAGE_TOAST:
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// The OnCreate
        /// </summary>
        /// <param name="savedInstanceState">The savedInstanceState<see cref="Bundle"/></param>
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);

            mHandler = new MyHandler(this);



            usbManager = GetSystemService(Context.UsbService) as UsbManager;


            PowerManager powerManager = GetSystemService(PowerService) as PowerManager;
            wakeLock = powerManager.NewWakeLock(WakeLockFlags.ScreenDim, TAG);


        }

        /// <summary>
        /// The OnDestroy
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            StopUSBService();
        }

        /// <summary>
        /// The OnPause
        /// </summary>
        protected override void OnPause()
        {
            base.OnPause();
            wakeLock.Release();
        }

        /// <summary>
        /// The OnResume
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            SetupUsbService();
            wakeLock.Acquire();
        }


        public bool IsServiceBound { get; set; } = false;


        /// <summary>
        /// The GetHandler
        /// </summary>
        /// <returns>The <see cref="Handler"/></returns>
        public Handler GetHandler()
        {
            return mHandler;
        }



        /// <summary>
        /// The GetSerialPort
        /// </summary>
        /// <returns>The <see cref="UsbSerialDevice"/></returns>
        public UsbSerialDevice GetSerialPort()
        {
            return usbService?.serialPort;
        }

        public ISharedPreferences GetSharedPreferences()
        {
            return prefs;
        }

        /// <summary>
        /// The GetUsbManager
        /// </summary>
        /// <returns>The <see cref="UsbManager"/></returns>
        public UsbManager GetUsbManager()
        {
            return usbManager;
        }

        /// <summary>
        /// The GetUsbService
        /// </summary>
        /// <returns>The <see cref="LGUsbService"/></returns>
        public LGUsbService GetUsbService()
        {
            return usbService;
        }




        /// <summary>
        /// The OnCreateOptionsMenu
        /// </summary>
        /// <param name="menu">The menu<see cref="IMenu"/></param>
        /// <returns>The <see cref="bool"/></returns>
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            return false;
        }



        public void SetupUsbService()
        {

            if (usbService == null)
            {
                detachedReceiver = new LGBroadcastReceiver(this);
                SetFilters();
                usbConnection = new USBConnection(this);
                StartUSBService(usbConnection, null);
                Android.Util.Log.Info("LNG.CMRI.MainActivity.SetupUsbService", "LGUsbService Started");
            }
        }
    }
}
