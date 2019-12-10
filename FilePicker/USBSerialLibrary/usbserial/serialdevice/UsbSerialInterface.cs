using System;

using Android.OS;

using com.felhr.utils;

namespace com.felhr.usbserial
{
    public abstract class UsbSerialInterface
    {
        // Common values
        public const int BAUD_RATE_300 = 300;

        public const int BAUD_RATE_600 = 600;
        public const int BAUD_RATE_1200 = 1200;
        public const int BAUD_RATE_2400 = 2400;
        public const int BAUD_RATE_4800 = 4800;
        public const int BAUD_RATE_9600 = 9600;
        public const int BAUD_RATE_19200 = 19200;
        public const int BAUD_RATE_38400 = 38400;
        public const int BAUD_RATE_57600 = 57600;
        public const int BAUD_RATE_115200 = 115200;
        public const int BAUD_RATE_230400 = 230400;
        public const int BAUD_RATE_460800 = 460800;
        public const int BAUD_RATE_921600 = 921600;

        public const int DATA_BITS_5 = 5;
        public const int DATA_BITS_6 = 6;
        public const int DATA_BITS_7 = 7;
        public const int DATA_BITS_8 = 8;

        public const int STOP_BITS_1 = 1;
        public const int STOP_BITS_15 = 3;
        public const int STOP_BITS_2 = 2;

        public const int PARITY_NONE = 0;
        public const int PARITY_ODD = 1;
        public const int PARITY_EVEN = 2;
        public const int PARITY_MARK = 3;
        public const int PARITY_SPACE = 4;

        public const int FLOW_CONTROL_OFF = 0;
        public const int FLOW_CONTROL_RTS_CTS = 1;
        public const int FLOW_CONTROL_DSR_DTR = 2;
        public const int FLOW_CONTROL_XON_XOFF = 3;

        // Common Usb Serial Operations (I/O Asynchronous)
        public abstract bool Open();

        public abstract void Write(byte[] buffer);

        public abstract int Read(IUsbReadCallback mCallback);

        public abstract void Close();

        // Common Usb Serial Operations (I/O Synchronous)
        public abstract bool SyncOpen();

        public abstract int SyncWrite(byte[] buffer, int timeout);

        public abstract int SyncRead(byte[] buffer, int timeout);

        public abstract void SyncClose();
        public abstract void PurgeHwBuffers(bool purgeReadBuffers, bool purgeWriteBuffers);

        // Serial port configuration
        public abstract void SetBaudRate(int baudRate);

        public abstract void SetDataBits(int dataBits);

        public abstract void SetStopBits(int stopBits);

        public abstract void SetParity(int parity);

        public abstract void SetFlowControl(int flowControl);

        // Flow control commands and interface callback
        public abstract void SetRTS(bool state);

        public abstract void SetDTR(bool state);

        public abstract void GetCTS(IUsbCTSCallback ctsCallback);

        public abstract void GetDSR(IUsbDSRCallback dsrCallback);

        // Status methods
        public abstract void GetBreak(IUsbBreakCallback breakCallback);

        public abstract void GetFrame(IUsbFrameCallback frameCallback);

        public abstract void GetOverrun(IUsbOverrunCallback overrunCallback);

        public abstract void GetParity(IUsbParityCallback parityCallback);
    }

    public interface IUsbCTSCallback
    {
        void OnCTSChanged(bool state);
    }

    public interface IUsbDSRCallback
    {
        void OnDSRChanged(bool state);
    }

    // Error signals callbacks
    public interface IUsbBreakCallback
    {
        void OnBreakInterrupt();
    }

    public interface IUsbFrameCallback
    {
        void OnFramingError();
    }

    public interface IUsbOverrunCallback
    {
        void OnOverrunError();
    }

    public interface IUsbParityCallback
    {
        void OnParityError();
    }

    // Usb Read Callback
    public interface IUsbReadCallback
    {
        event EventHandler<SerialDataReceivedArgs> DataReceived;

        void OnReceivedData(byte[] data);

        void Init();

        void SetHandler(Handler handler);
    }
}