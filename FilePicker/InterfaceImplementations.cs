using Android.OS;
using Android.Util;

using com.felhr.usbserial;
using com.felhr.utils;

using System;

namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="UsbCTSCallback" />
    /// </summary>
    public class UsbCTSCallback : Java.Lang.Object, IUsbCTSCallback
    {
        public Handler mHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbCTSCallback"/> class.
        /// </summary>
        /// <param name="handler">The handler<see cref="Handler"/></param>
        public UsbCTSCallback(ref Handler handler)
        {
            mHandler = handler;
        }

        /// <summary>
        /// The OnCTSChanged
        /// </summary>
        /// <param name="p0">The p0<see cref="bool"/></param>
        public void OnCTSChanged(bool p0)
        {
            if (mHandler != null)
            {
                mHandler.ObtainMessage(ServiceConstants.CTS_CHANGE).SendToTarget();
            }
        }
    }

    /// <summary>
    /// Defines the <see cref="UsbDSRCallback" />
    /// </summary>
    public class UsbDSRCallback : Java.Lang.Object, IUsbDSRCallback
    {
        public Handler mHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbDSRCallback"/> class.
        /// </summary>
        /// <param name="handler">The handler<see cref="Handler"/></param>
        public UsbDSRCallback(ref Handler handler)
        {
            mHandler = handler;
        }

        /// <summary>
        /// The OnDSRChanged
        /// </summary>
        /// <param name="p0">The p0<see cref="bool"/></param>
        public void OnDSRChanged(bool p0)
        {
            if (mHandler != null)
            {
                mHandler.ObtainMessage(ServiceConstants.DSR_CHANGE).SendToTarget();
            }
        }
    }

    /// <summary>
    /// Defines the <see cref="UsbReadCallback" />
    /// </summary>
    public class UsbReadCallback : Java.Lang.Object, IUsbReadCallback
    {
        private static SerialDataReceivedArgs serialDataReceivedArgs = new SerialDataReceivedArgs(null);
        public Handler mHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbReadCallback"/> class.
        /// </summary>
        /// <param name="handler">The handler<see cref="Handler"/></param>
        public UsbReadCallback(ref Handler handler)
        {
            mHandler = handler;
        }

        public event EventHandler<SerialDataReceivedArgs> DataReceived;

        /// <summary>
        /// The Init
        /// </summary>
        public void Init()
        {
        }

        /// <summary>
        /// The OnReceivedData
        /// </summary>
        /// <param name="arg0">The arg0<see cref="byte[]"/></param>
        public void OnReceivedData(byte[] arg0)
        {
            try
            {
                //string data = new String(arg0, "UTF-8");
                //string data = HexDump.ToHexString(arg0) + "\n";
                //if (mHandler != null)
                //    mHandler.ObtainMessage(ServiceConstants.MESSAGE_FROM_SERIAL_PORT, arg0).SendToTarget();
                serialDataReceivedArgs.Data = arg0;
                DataReceived.Raise(this, serialDataReceivedArgs);
                //Android_DataReceived(arg0);
            }
            catch (Exception e)
            {
                Log.Debug("UsbReadCallback", e.StackTrace);
            }
        }

        /// <summary>
        /// The SetHandler
        /// </summary>
        /// <param name="handler">The handler<see cref="Handler"/></param>
        public void SetHandler(Handler handler)
        {
            mHandler = handler;
        }
    }
}