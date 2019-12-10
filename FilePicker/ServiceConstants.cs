namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="ServiceConstants" />
    /// </summary>
    public static class ServiceConstants
    {
        public const string ACTION_CDC_DRIVER_NOT_WORKING = "com.felhr.connectivityservices.ACTION_CDC_DRIVER_NOT_WORKING";

        public const string ACTION_CLOSE_SERIAL_PORT = "com.felhr.connectivityservices.ACTION_CLOSE_SERIAL_PORT";

        public const string ACTION_NO_USB = "com.felhr.usbservice.NO_USB";

        public const string ACTION_OPEN_SERIAL_PORT = "com.felhr.connectivityservices.ACTION_OPEN_SERIAL_PORT";

        public const string ACTION_SET_BAUD_RATE = "com.felhr.connectivityservices.ACTION_SET_BAUD_RATE";

        public const string ACTION_USB_ATTACHED = "android.hardware.usb.action.USB_DEVICE_ATTACHED";

        public const string ACTION_USB_DETACHED = "android.hardware.usb.action.USB_DEVICE_DETACHED";

        public const string ACTION_USB_DEVICE_NOT_WORKING = "com.felhr.connectivityservices.ACTION_USB_DEVICE_NOT_WORKING";

        public const string ACTION_USB_DISCONNECTED = "com.felhr.usbservice.USB_DISCONNECTED";

        public const string ACTION_USB_NOT_SUPPORTED = "com.felhr.usbservice.USB_NOT_SUPPORTED";

        public const string ACTION_USB_PERMISSION = "com.android.example.USB_PERMISSION";

        public const string ACTION_USB_PERMISSION_GRANTED = "com.felhr.usbservice.USB_PERMISSION_GRANTED";

        public const string ACTION_USB_PERMISSION_NOT_GRANTED = "com.felhr.usbservice.USB_PERMISSION_NOT_GRANTED";

        public const string ACTION_USB_READY = "com.felhr.connectivityservices.USB_READY";

        public const int CTS_CHANGE = 1;
        public const string DEVICE_NAME = "device_name";
        public const int DSR_CHANGE = 2;
        public const int MESSAGE_DEVICE_NAME = 7;
        public const int MESSAGE_FROM_SERIAL_PORT = 0;
        public const int MESSAGE_READ = 5;
        public const int MESSAGE_STATE_CHANGE = 4;
        public const int MESSAGE_TOAST = 8;
        public const int MESSAGE_WRITE = 6;
        public const int SYNC_READ = 3;
        public const string TAG_BAUD_RATE = "baudrate";
        public const string TOAST = "toast";
    }
}