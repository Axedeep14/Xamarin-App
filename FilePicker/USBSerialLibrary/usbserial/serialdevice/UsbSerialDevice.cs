
using System;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using com.felhr.deviceids;
using com.felhr.utils;
using Java.Lang;

namespace com.felhr.usbserial
{
    public abstract class UsbSerialDevice : UsbSerialInterface
    {
        private const string CLASS_ID = nameof(UsbSerialDevice);

        private const string CDC = "cdc";
        private const string CH34x = "ch34x";
        private const string CP210x = "cp210x";
        private const string FTDI = "ftdi";
        private const string PL2303 = "pl2303";

        public const string COM_PORT = "COM ";

        public readonly UsbDevice device;
        public readonly UsbDeviceConnection connection;

        public const int USB_TIMEOUT = 5000;

        public SerialBuffer serialBuffer;

        protected WorkerThread workerThread;
        protected WriteThread writeThread;
        protected ReadThread readThread;

        // InputStream and OutputStream (only for sync api)
        protected SerialInputStream inputStream;

        protected SerialOutputStream outputStream;

        // Endpoints for synchronous read and write operations
        private UsbEndpoint inEndpoint;

        private UsbEndpoint outEndpoint;

        protected bool asyncMode;

        private string portName = "";

#if DEBUG
        private static bool debugging = false;
#else
        private static bool debugging = false;
#endif

        public readonly System.Threading.ReaderWriterLockSlim _workerlock = new System.Threading.ReaderWriterLockSlim();

        // Get Android version if version < 4.3 It is not going to be asynchronous read operations
        private static string[] manufacturers = { "samsung", "xiaomi" };
        protected static bool mr1Version = Build.VERSION.SdkInt > BuildVersionCodes.JellyBeanMr1;
        protected static bool isAPI26Version = Build.VERSION.SdkInt >= BuildVersionCodes.O;

        private IUsbReadCallback readCallback;
        public int CurrentBaudRate { get; protected set; }

        public UsbSerialDevice(UsbDevice device, UsbDeviceConnection connection)
        {
            this.device = device;
            this.connection = connection;
            this.asyncMode = true;
            serialBuffer = new SerialBuffer(mr1Version); 
        }

        public static UsbSerialDevice CreateUsbSerialDevice(UsbDevice device, UsbDeviceConnection connection)
        {
            return CreateUsbSerialDevice(device, connection, -1);
        }

        public static UsbSerialDevice CreateUsbSerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface)
        {
            /*
             * It checks given vid and pid and will return a custom driver or a CDC serial driver.
             * When CDC is returned open() method is even more important, its response will inform about if it can be really
             * opened as a serial device with a generic CDC serial driver
             */
            int vid = device.VendorId;
            int pid = device.ProductId;

            if (FTDISioIds.IsDeviceSupported(vid, pid))
            {
                return new FTDISerialDevice(device, connection, iface);
            }
            else if (CP210xIds.IsDeviceSupported(vid, pid))
            {
                return new CP2102SerialDevice(device, connection, iface);
            }
            else if (PL2303Ids.IsDeviceSupported(vid, pid))
            {
                return new PL2303SerialDevice(device, connection, iface);
            }
            else if (CH34xIds.IsDeviceSupported(vid, pid))
            {
                return new CH34xSerialDevice(device, connection, iface);
            }
            else if (IsCdcDevice(device))
            {
                return new CDCSerialDevice(device, connection, iface);
            }
            else
            {
                return null;
            }
        }

        public static UsbSerialDevice CreateUsbSerialDevice(string type, UsbDevice device, UsbDeviceConnection connection, int iface)
        {
            if (type.Equals(FTDI))
            {
                return new FTDISerialDevice(device, connection, iface);
            }
            else if (type.Equals(CP210x))
            {
                return new CP2102SerialDevice(device, connection, iface);
            }
            else if (type.Equals(PL2303))
            {
                return new PL2303SerialDevice(device, connection, iface);
            }
            else if (type.Equals(CH34x))
            {
                return new CH34xSerialDevice(device, connection, iface);
            }
            else if (type.Equals(CDC))
            {
                return new CDCSerialDevice(device, connection, iface);
            }
            else
            {
                throw new IllegalArgumentException("Invalid type argument. Must be:cdc, ch34x, cp210x, ftdi or pl2303");
            }
        }

        public static bool IsSupported(UsbDevice device)
        {
            int vid = device.VendorId;
            int pid = device.ProductId;

            if (FTDISioIds.IsDeviceSupported(vid, pid))
            {
                return true;
            }
            else if (CP210xIds.IsDeviceSupported(vid, pid))
            {
                return true;
            }
            else if (PL2303Ids.IsDeviceSupported(vid, pid))
            {
                return true;
            }
            else if (CH34xIds.IsDeviceSupported(vid, pid))
            {
                return true;
            }
            else if (IsCdcDevice(device))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // Common Usb Serial Operations (I/O Asynchronous)

        public abstract override bool Open();

        public override void Write(byte[] buffer)
        {
            if (asyncMode)
            {
                serialBuffer.PutWriteBuffer(buffer);
            }
        }

         /*
         * 
         *     Use this setter before calling { #open()} to override the default baud rate defined in this particular class.
         * 
         *     This is a workaround for devices where calling { #setBaudRate(int)} has no effect once { #open()} has been called.
         * 
         *     param initialBaudRate baud rate to be used when initializing the serial connection
         */

        public virtual void SetInitialBaudRate(int initialBaudRate)
        {
            // this class does not implement initialBaudRate
        }

        /**
         * Classes that do not implement {@link #setInitialBaudRate(int)} should always return -1
         *
         * @return initial baud rate used when initializing the serial connection
         */

        public virtual int GetInitialBaudRate()
        {
            return -1;
        }

        public override int Read(IUsbReadCallback mCallback)
        {
            readCallback = mCallback;

            if (!asyncMode)
            {
                return -1;
            }

            if (mr1Version)
            {
                if (workerThread != null)
                {
                    workerThread.SetCallback(readCallback);
                    if (isAPI26Version)
                    {
                        workerThread.GetUsbRequest().Queue(serialBuffer.GetReadBuffer());
                    }
                    else
                    {
                        workerThread.GetUsbRequest().Queue(serialBuffer.GetReadBuffer(), SerialBuffer.DEFAULT_READ_BUFFER_SIZE);
                    }
                }
            }
            else
            {
                readThread.SetCallback(readCallback);
                //readThread.start();
            }
            return 0;
        }

        public abstract override void Close();

        // Common Usb Serial Operations (I/O Synchronous)

        public abstract override bool SyncOpen();

        public abstract override void SyncClose();

        public override int SyncWrite(byte[] buffer, int timeout)
        {
            if (!asyncMode)
            {
                if (buffer == null)
                {
                    return 0;
                }
                if (debugging)
                {
                    UsbSerialDebugger.PrintLogPut(buffer, true);
                }
                return connection.BulkTransfer(outEndpoint, buffer, buffer.Length, timeout);
            }
            else
            {
                return -1;
            }
        }

        public override int SyncRead(byte[] buffer, int timeout)
        {
            if (asyncMode)
            {
                return -1;
            }

            if (buffer == null)
            {
                return 0;
            }

            return connection.BulkTransfer(inEndpoint, buffer, buffer.Length, timeout);
        }

        // Serial port configuration

        public abstract override void SetBaudRate(int baudRate);

        public abstract override void SetDataBits(int dataBits);

        public abstract override void SetStopBits(int stopBits);

        public abstract override void SetParity(int parity);

        public abstract override void SetFlowControl(int flowControl);

        //Debug options
        public void Debug(bool value)
        {
            serialBuffer?.Debug(value);
        }

        public bool IsFTDIDevice()
        {
            return this is FTDISerialDevice;
        }

        public static bool IsCdcDevice(UsbDevice device)
        {
            int iIndex = device.InterfaceCount;
            for (int i = 0; i <= iIndex - 1; i++)
            {
                UsbInterface iface = device.GetInterface(i);
                if (iface.InterfaceClass == UsbClass.CdcData)
                {
                    return true;
                }
            }
            return false;
        }

        public int GetVid()
        {
            return device.VendorId;
        }

        public int GetPid()
        {
            return device.ProductId;
        }

        public int GetDeviceId()
        {
            return device.DeviceId;
        }

        public void SetPortName(string portName)
        {
            this.portName = portName;
        }

        public string GetPortName()
        {
            return this.portName;
        }

        public bool IsOpen { get; set; }

        public SerialInputStream GetInputStream()
        {
            if (asyncMode)
            {
                throw new IllegalStateException("InputStream only available in Sync mode. \n" +
                        "Open the port with syncOpen()");
            }

            return inputStream;
        }

        public SerialOutputStream GetOutputStream()
        {
            if (asyncMode)
            {
                throw new IllegalStateException("OutputStream only available in Sync mode. \n" +
                        "Open the port with syncOpen()");
            }

            return outputStream;
        }

        protected void SetSyncParams(UsbEndpoint inEndpoint, UsbEndpoint outEndpoint)
        {
            this.inEndpoint = inEndpoint;
            this.outEndpoint = outEndpoint;
        }

        protected void SetThreadsParams(UsbRequest request, UsbEndpoint endpoint)
        {
            writeThread.SetUsbEndpoint(endpoint);
            if (mr1Version)
            {
                workerThread.SetUsbRequest(request);
            }
            else
            {
                readThread.SetUsbEndpoint(request.Endpoint);
            }
        }

        /*
         * Kill workingThread; This must be called when closing a device
         */

        protected void KillWorkingThread()
        {
            if (mr1Version && workerThread != null)
            {
                workerThread.StopThread();
                //while (workerThread.IsAlive) { } // Busy waiting
                Log.Info("UsbSerialDevice", "Read Thread stopped.");
                workerThread = null;
            }
            else if (!mr1Version && readThread != null)
            {
                readThread.StopThread();
                //while (readThread.IsAlive) { } // Busy waiting
                Log.Info("UsbSerialDevice", "Read Thread stopped.");
                readThread = null;
            }
            //Thread.Sleep(1000);
        }

        /*
         * Restart workingThread if it has been killed before
         */

        protected void RestartWorkingThread()
        {
            if (mr1Version && workerThread == null)
            {
                workerThread = new WorkerThread(this);
                workerThread.Start();
                workerThread.SetCallback(readCallback);
                while (!workerThread.IsAlive) { } // Busy waiting
                Log.Info("UsbSerialDevice", "Read Thread started.");
            }
            else if (!mr1Version && readThread == null)
            {
                readThread = new ReadThread(this);
                readThread.Start();
                readThread.SetCallback(readCallback);
                while (!readThread.IsAlive) { } // Busy waiting
                Log.Info("UsbSerialDevice", "Read Thread started.");
            }
        }

        protected void KillWriteThread()
        {
            if (writeThread != null)
            {
                writeThread.StopThread();
                Log.Info("UsbSerialDevice", "Write Thread stopped.");
                writeThread = null;
            }
        }

        protected void RestartWriteThread()
        {
            if (writeThread == null)
            {
                writeThread = new WriteThread(this);
                writeThread.Start();
                while (!writeThread.IsAlive) { } // Busy waiting
                Log.Info("UsbSerialDevice", "Write Thread started.");
            }
        }

        public class ReadThread : AbstractWorkerThread
        {
            private UsbSerialDevice device;

            private IUsbReadCallback callback;
            private UsbEndpoint inEndpoint;
            //private Java.Util.Concurrent.Atomic.AtomicBoolean working;

            public ReadThread(UsbSerialDevice usbSerialDevice)
            {
                this.device = usbSerialDevice;
                Android.OS.Process.SetThreadPriority(ThreadPriority.UrgentAudio);
                //working = new Java.Util.Concurrent.Atomic.AtomicBoolean(true);
            }

            public void SetCallback(IUsbReadCallback callback)
            {
                this.callback = callback;
            }

            public override void DoRun()
            {
                byte[] dataReceived = null;

                int numberBytes;
                if (inEndpoint != null)
                {
                    numberBytes = device.connection.BulkTransfer(inEndpoint, device.serialBuffer.GetBufferCompatible(),
                            SerialBuffer.DEFAULT_READ_BUFFER_SIZE, 0);
                }
                else
                {
                    numberBytes = 0;
                }

                if (numberBytes > 0)
                {
                    dataReceived = device.serialBuffer.GetDataReceivedCompatible(numberBytes);

                    // FTDI devices reserve two first bytes of an IN endpoint with info about
                    // modem and Line.
                    if (device.IsFTDIDevice())
                    {
                        ((FTDISerialDevice)device).ftdiUtilities.CheckModemStatus(dataReceived);

                        if (dataReceived.Length > 2)
                        {
                            dataReceived = FTDISerialDevice.AdaptArray(dataReceived);
                            OnReceivedData(dataReceived);
                        }
                    }
                    else
                    {
                        OnReceivedData(dataReceived);
                    }
                }
            }

            public void SetUsbEndpoint(UsbEndpoint inEndpoint)
            {
                this.inEndpoint = inEndpoint;
            }

            //public void StopReadThread()
            //{
            //    working.Set(false);
            //}

            private void OnReceivedData(byte[] data)
            {
                callback?.OnReceivedData(data);
            }
        }

        public class WriteThread : AbstractWorkerThread
        {
            private UsbEndpoint outEndpoint;
            private UsbSerialDevice device;

            public WriteThread(UsbSerialDevice usbSerialDevice)
            {
                this.device = usbSerialDevice;
            }

            public override void DoRun()
            {
                byte[] data = device.serialBuffer.GetWriteBuffer();
                if (data.Length > 0)
                {
                    device.connection.BulkTransfer(outEndpoint, data, data.Length, UsbSerialDevice.USB_TIMEOUT);
                }
            }

            public void SetUsbEndpoint(UsbEndpoint outEndpoint)
            {
                this.outEndpoint = outEndpoint;
            }
        }

        public class WorkerThread : AbstractWorkerThread
        {
            public static bool instanceExists ;
            private bool isSpecialCase;
            private UsbSerialDevice device;
            private IUsbReadCallback callback;
            private UsbRequest requestIN;

            public WorkerThread(UsbSerialDevice usbSerialDevice)
            {
                this.device = usbSerialDevice;
                //working = new Java.Util.Concurrent.Atomic.AtomicBoolean(true);
                Android.OS.Process.SetThreadPriority(ThreadPriority.UrgentAudio);
            }

            public override void DoRun()
            {
                UsbRequest request = null;

                /**
                *blocking call on usb device.
                *Make sure for UsbRequest object RequestWait and Queue functions do not deadlock from different threads
                **/

                //request = device.connection.RequestWait();
                if (!isSpecialCase)
                {
                    try
                    {
                        request = device.connection.RequestWait(2000);  //blocking call on usbdevice with optional timeout
                    }
                    catch (Java.Lang.NoSuchMethodError)
                    {
                        isSpecialCase = true;
                        Log.Error("com.felhr.usbserial.UsbSerialDevice.WorkerThread.DoRun", "Exception at RequestWait(): Special Case");
                    }
                    catch (Java.Util.Concurrent.TimeoutException)
                    {
                        /*do nothing and continue*/
                    }
                }
                else
                {
                    try
                    {
                        request = device.connection.RequestWait();  //blocking call on usbdevice
                    }
                    catch (Java.Lang.Exception)
                    {
                        /*do nothing and continue*/
                        Log.Error("com.felhr.usbserial.UsbSerialDevice.WorkerThread.DoRun", "Exception at RequestWait()");
                    }
                }

                if (request?.Endpoint.Type == UsbAddressing.XferBulk
                            && request.Endpoint.Direction == UsbAddressing.In)
                {
                    byte[] data = device.serialBuffer.GetDataReceived();

                    // FTDI devices reserves two first bytes of an IN endpoint with info about
                    // modem and Line.
                    if (device.IsFTDIDevice())
                    {
                        ((FTDISerialDevice)device).ftdiUtilities.CheckModemStatus(data); //Check the Modem status
                        device.serialBuffer.ClearReadBuffer();

                        if (data.Length > 2)
                        {
                            data = FTDISerialDevice.AdaptArray(data);
                            OnReceivedData(data);
                        }
                    }
                    else
                    {
                        // Clear buffer, execute the callback
                        device.serialBuffer.ClearReadBuffer();
                        OnReceivedData(data);
                    }

                    if (debugging && !(data.Length<=2 && device.IsFTDIDevice()))
                    {
                        UsbSerialDebugger.PrintReadLogGet(data, true);
                    }

                    // Queue a new request
                    //System.Threading.Thread.Sleep(10);

                    if (isAPI26Version) //required check for Java.Lang.NoSuchMethodError thrown for some manufacturers on Queue(buffer); deprecation of Queue(buffer, size) since API 26
                    {
                        requestIN?.Queue(device.serialBuffer.GetReadBuffer());
                    }
                    else
                    {
                        requestIN?.Queue(device.serialBuffer.GetReadBuffer(), SerialBuffer.DEFAULT_READ_BUFFER_SIZE);      //blocking call on usbdevice
                    }
                }
            }

            public new void Dispose()
            {
                base.Dispose();
            }

            public void SetCallback(IUsbReadCallback callback)
            {
                this.callback = callback;
            }

            public void SetUsbRequest(UsbRequest request)
            {
                this.requestIN = request;
            }

            public UsbRequest GetUsbRequest()
            {
                return requestIN;
            }

            private void OnReceivedData(byte[] data)
            {
                callback?.OnReceivedData(data);
            }
        }
    }
}