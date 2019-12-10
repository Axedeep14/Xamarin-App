using Android.Content;
using Android.OS;

namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="USBConnection" />
    /// </summary>
    public class USBConnection : Java.Lang.Object, IServiceConnection
    {
        private MainActivity mActivity;

        /// <summary>
        /// Initializes a new instance of the <see cref="USBConnection"/> class.
        /// </summary>
        /// <param name="mActivity">The mActivity<see cref="MainActivity"/></param>
        public USBConnection(MainActivity mActivity)
        {
            this.mActivity = mActivity;
        }

        /// <summary>
        /// The OnServiceConnected
        /// </summary>
        /// <param name="name">The name<see cref="ComponentName"/></param>
        /// <param name="service">The service<see cref="IBinder"/></param>
        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            mActivity.usbService = (service as SerialBinder)?.GetService() as LGUsbService;
            mActivity.usbService.SetHandler(mActivity.mHandler);

            mActivity.IsServiceBound = true;

            if (mActivity.usbService.isFirstRun)
            {
                mActivity.usbService.FindSerialPortDevice();
            }
        }

        /// <summary>
        /// The OnServiceDisconnected
        /// </summary>
        /// <param name="name">The name<see cref="ComponentName"/></param>
        public void OnServiceDisconnected(ComponentName name)
        {
            mActivity.IsServiceBound = false;
            mActivity.usbService = null;
        }
    }
}