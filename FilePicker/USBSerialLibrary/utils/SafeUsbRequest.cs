using System;
using Java.Lang.Reflect;
using Android.Hardware.Usb;
using Java.Nio;

namespace com.felhr.utils
{
    public class SafeUsbRequest : UsbRequest
    {
        const string usbRqBufferField = "mBuffer";
        const string usbRqLengthField = "mLength";

        [Obsolete("Queue(ByteBuffer buffer, int length) is deprecated, please use Queue(ByteBuffer buffer) instead.")]
        public override bool Queue(ByteBuffer buffer, int length)
        {
            Field usbRequestBuffer;
            Field usbRequestLength;
            try
            {
                usbRequestBuffer = Java.Lang.Class.FromType(typeof(UsbRequest)).GetDeclaredField(usbRqBufferField);
                usbRequestLength = Java.Lang.Class.FromType(typeof(UsbRequest)).GetDeclaredField(usbRqLengthField);
                usbRequestBuffer.Accessible = true;
                usbRequestLength.Accessible = true;
                usbRequestBuffer.Set(this, buffer);
                usbRequestLength.Set(this, length);
            }
            catch (Exception ex)
            {
                throw;
            }

            return base.Queue(buffer, length);
        }

        public override bool Queue(ByteBuffer buffer)
        {
            Field usbRequestBuffer;
            Field usbRequestLength;
            try
            {
                usbRequestBuffer = Java.Lang.Class.FromType(typeof(UsbRequest)).GetDeclaredField(usbRqBufferField);
                usbRequestLength = Java.Lang.Class.FromType(typeof(UsbRequest)).GetDeclaredField(usbRqLengthField);
                usbRequestBuffer.Accessible = true;
                usbRequestLength.Accessible = true;
                usbRequestBuffer.Set(this, buffer);
                //usbRequestLength.Set(this, length);
            }
            catch (Exception ex)
            {
                throw;
            }

            return base.Queue(buffer);
        }
    }
}