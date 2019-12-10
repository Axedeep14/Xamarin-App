using System.Text;

using Android.Util;

using com.felhr.utils;

namespace com.felhr.usbserial
{
    public class UsbSerialDebugger
    {
        private static HexData hexDataForReader = new HexData();
        private static HexData hexDataForWriter = new HexData();
        private const string CLASS_ID = nameof(UsbSerialDebugger);
        private const string DataObtainedWriteBuffer = "Data obtained from write buffer: ";
        private const string DataObtainedPushedWriteBuffer = "Data obtained pushed to write buffer: ";
        private const string DataObtainedReadBuffer = "Data obtained from Read buffer: ";
        private const string DataObtainedPushedReadBuffer = "Data obtained pushed to read buffer: ";
        private const string ByteSuffix = "B || ";
        public const string ENCODING = "UTF-8";

        private UsbSerialDebugger()
        {
            
        }

        public static void PrintLogGet(byte[] src, bool verbose)
        {
            if (!verbose)
            {
                Log.Info(CLASS_ID, DataObtainedWriteBuffer + Encoding.ASCII.GetString(src));
            }
            else
            {
                //Log.Info(CLASS_ID, DataObtainedWriteBuffer + src.Length + ByteSuffix + Encoding.ASCII.GetString(src));
                Log.Info(CLASS_ID, DataObtainedWriteBuffer + src.Length + ByteSuffix + hexDataForWriter.HexToString(src));  //HexData.HexToString(src));
                //Log.Info(CLASS_ID, "Number of bytes obtained from write buffer: " + src.Length);
            }
        }

        public static void PrintLogPut(byte[] src, bool verbose)
        {
            if (!verbose)
            {
                Log.Info(CLASS_ID, DataObtainedPushedWriteBuffer + Encoding.ASCII.GetString(src));
            }
            else
            {
                //Log.Info(CLASS_ID, DataObtainedPushedWriteBuffer + Encoding.ASCII.GetString(src));
                Log.Info(CLASS_ID, DataObtainedPushedWriteBuffer + src.Length + ByteSuffix + hexDataForWriter.HexToString(src));
                //Log.Info(CLASS_ID, "Number of bytes pushed from write buffer: " + src.Length);
            }
        }

        public static void PrintReadLogGet(byte[] src, bool verbose)
        {
            if (!verbose)
            {
                Log.Info(CLASS_ID, DataObtainedReadBuffer + Encoding.ASCII.GetString(src));
            }
            else
            {
                //Log.Info(CLASS_ID, "Data obtained from Read buffer: " + Encoding.ASCII.GetString(src));
                Log.Info(CLASS_ID, DataObtainedReadBuffer + src.Length + ByteSuffix + hexDataForReader.HexToString(src));
                //Log.Info(CLASS_ID, "Number of bytes obtained from Read buffer: " + src.Length);
            }
        }

        public static void PrintReadLogPut(byte[] src, bool verbose)
        {
            if (!verbose)
            {
                Log.Info(CLASS_ID, DataObtainedPushedReadBuffer + Encoding.ASCII.GetString(src));
            }
            else
            {
                //Log.Info(CLASS_ID, "Data obtained pushed to read buffer: " + Encoding.ASCII.GetString(src));
                Log.Info(CLASS_ID, DataObtainedPushedReadBuffer + src.Length + ByteSuffix + hexDataForReader.HexToString(src));
                //Log.Info(CLASS_ID, "Number of bytes pushed from read buffer: " + src.Length);
            }
        }
    }
}