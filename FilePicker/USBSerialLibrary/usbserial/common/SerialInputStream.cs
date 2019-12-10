using Java.IO;

namespace com.felhr.usbserial
{
    public class SerialInputStream : InputStream
    {
        private readonly byte[] buffer;
        private int bufferSize;
        private int maxBufferSize = 16 * 1024;
        private int pointer;
        private int timeout = 0;
        protected readonly UsbSerialInterface device;

        public SerialInputStream(UsbSerialInterface localdevice)
        {
            this.device = localdevice;
            this.buffer = new byte[maxBufferSize];
            this.pointer = 0;
            this.bufferSize = -1;
        }

        public SerialInputStream(UsbSerialInterface device, int maxBufferSize)
        {
            this.device = device;
            this.maxBufferSize = maxBufferSize;
            this.buffer = new byte[this.maxBufferSize];
            this.pointer = 0;
            this.bufferSize = -1;
        }

        private int CheckFromBuffer()
        {
            if (bufferSize > 0 && pointer < bufferSize)
            {
                return buffer[pointer++] & 0xff;
            }
            else
            {
                pointer = 0;
                bufferSize = -1;
                return -1;
            }
        }

        public override int Available()
        {
            try
            {
                if (bufferSize > 0)
                {
                    return bufferSize - pointer;
                }
                else
                {
                    return 0;
                }
            }
            catch (IOException)
            {
                throw;
            }
        }

        public override int Read()
        {
            int value = CheckFromBuffer();
            if (value >= 0)
            {
                return value;
            }

            int ret = device.SyncRead(buffer, timeout);
            if (ret >= 0)
            {
                bufferSize = ret;
                return buffer[pointer++] & 0xff;
            }
            else
            {
                return -1;
            }
        }

        public override int Read(byte[] b)
        {
            return device.SyncRead(b, timeout);
        }

        public void SetTimeout(int timeout)
        {
            this.timeout = timeout;
        }
    }
}