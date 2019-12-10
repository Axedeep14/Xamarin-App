using System.Threading;

using Android.Util;

using Java.Nio;
using Java.Util;

using USBSerialLibrary.utils;

using Buffer = Square.OkIO.OkBuffer;

namespace com.felhr.usbserial
{
    public class SerialBuffer
    {
        public const int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;     //16 * 1024;
        public const int MAX_BULK_BUFFER = 16 * 1024;
        private ByteBuffer readBuffer;
        private SynchronizedBuffer writeBuffer;
        private byte[] readBufferCompatible; // Read buffer for android < 4.2

#if DEBUG
        private static bool debugging = false;
#else
        private static bool debugging = false;
#endif
        private readonly object LockObject = new object();

        public SerialBuffer(bool version)
        {
            writeBuffer = new SynchronizedBuffer();
            if (version)
            {
                readBuffer = ByteBuffer.Allocate(DEFAULT_READ_BUFFER_SIZE);
            }
            else
            {
                readBufferCompatible = new byte[DEFAULT_READ_BUFFER_SIZE];
            }
        }

        /*
         * Print debug messages
         */

        public void Debug(bool value)
        {
            debugging = value;
        }

        public ByteBuffer GetReadBuffer()
        {
            //lock (this)
            using (LockObject.Lock(5000))
            {
                return readBuffer;
            }
        }

        public byte[] GetDataReceived()
        {
            using (LockObject.Lock(5000))
            {
                byte[] dst = new byte[readBuffer.Position()];
                readBuffer.Position(0);
                readBuffer.Get(dst, 0, dst.Length);
                //if (debugging)
                //{
                //    UsbSerialDebugger.PrintReadLogGet(dst, true);
                //}

                return dst;
            }
        }

        public void ClearReadBuffer()
        {
            using (LockObject.Lock(5000))
            {
                readBuffer.Clear();
            }
        }

        public byte[] GetWriteBuffer()
        {
            return writeBuffer.Get();
        }

        public void PutWriteBuffer(byte[] data)
        {
            writeBuffer.Put(data);
        }

        public byte[] GetBufferCompatible()
        {
            return readBufferCompatible;
        }

        public byte[] GetDataReceivedCompatible(int numberBytes)
        {
            return Arrays.CopyOfRange(readBufferCompatible, 0, numberBytes);
        }

        private class SynchronizedBuffer
        {
            private readonly Buffer buffer;
            private readonly object LockObject = new object();

            public SynchronizedBuffer()
            {
                buffer = new Buffer();
            }

            public void Put(byte[] src)
            {
                if (!Monitor.TryEnter(LockObject, 5000))
                {
                    throw new System.TimeoutException("LNGCommunication.PhysicalLayer.Channel.CreateChannel Lock_Exception");
                }
                try
                {
                    if (src == null || src.Length == 0)
                    {
                        return;
                    }

                    if (debugging)
                    {
                        UsbSerialDebugger.PrintLogPut(src, true);
                    }

                    buffer.Write(src);
                    Monitor.Pulse(LockObject);
                }
                finally
                {
                    Monitor.Exit(LockObject);
                }
            }

            public byte[] Get()
            {
                if (!Monitor.TryEnter(LockObject, 5000))
                {
                    throw new System.TimeoutException("LNGCommunication.PhysicalLayer.Channel.CreateChannel Lock_Exception");
                }
                try
                {
                    //if (position == -1)
                    if (buffer.Size() == 0)
                    {
                        try
                        {
                            Monitor.Wait(LockObject);
                        }
                        catch (Java.Lang.InterruptedException e)
                        {
                            e.PrintStackTrace();
                            Thread.CurrentThread.Interrupt();
                        }
                    }

                    byte[] dst;
                    if (buffer.Size() <= MAX_BULK_BUFFER)
                    {
                        dst = buffer.ReadByteArray();
                    }
                    else
                    {
                        try
                        {
                            dst = buffer.ReadByteArray(MAX_BULK_BUFFER);
                        }
                        catch (Java.IO.EOFException e)
                        {
                            Log.Error("com.felhr.usbserial.SerialBuffer.SynchronizedBuffer.Get", e.Message);
                            return new byte[0];
                        }
                    }

                    if (debugging)
                    {
                        UsbSerialDebugger.PrintLogGet(dst, true);
                    }
                    return dst;
                }
                finally
                {
                    Monitor.Exit(LockObject);
                }
            }
        }
    }
}