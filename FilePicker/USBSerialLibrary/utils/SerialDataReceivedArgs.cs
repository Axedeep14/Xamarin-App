using System;

namespace com.felhr.utils
{
    public class SerialDataReceivedArgs : EventArgs
    {
        public SerialDataReceivedArgs(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; set; }
    }
}