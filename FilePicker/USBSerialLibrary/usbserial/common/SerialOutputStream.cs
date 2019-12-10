using Java.IO;

namespace com.felhr.usbserial
{
    public class SerialOutputStream : OutputStream
    {
        private int timeout = 0;
        protected UsbSerialInterface device;

        public SerialOutputStream(UsbSerialInterface device)
        {
            this.device = device;
        }

        public override void Write(int b)
        {
            device.SyncWrite(new byte[] { (byte)b }, timeout);
        }

        public override void Write(byte[] b)
        {
            device.SyncWrite(b, timeout);
        }

        public void SetTimeout(int timeout)
        {
            this.timeout = timeout;
        }
    }
}