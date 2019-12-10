using Android.Hardware.Usb;

using com.felhr.deviceids;

namespace com.felhr.usbserial
{
    public abstract class UsbSpiDevice : UsbSpiInterface
    {
        private const string CLASS_ID = nameof(UsbSerialDevice);

        // Endpoints for synchronous read and write operations
        private UsbEndpoint inEndpoint;

        private UsbEndpoint outEndpoint;
        protected const int USB_TIMEOUT = 5000;

        protected readonly UsbDeviceConnection connection;
        protected readonly UsbDevice device;
        protected ReadThread readThread;
        protected SerialBuffer serialBuffer;

        protected WriteThread writeThread;

        public UsbSpiDevice(UsbDevice device, UsbDeviceConnection connection)
        {
            this.device = device;
            this.connection = connection;
            this.serialBuffer = new SerialBuffer(false);
        }

        protected void KillWorkingThread()
        {
            if (readThread != null)
            {
                readThread.StopThread();
                readThread = null;
            }
        }

        protected void KillWriteThread()
        {
            if (writeThread != null)
            {
                writeThread.StopThread();
                writeThread = null;
            }
        }

        protected void RestartWorkingThread()
        {
            readThread = new ReadThread(this);
            readThread.Start();
            while (!readThread.IsAlive) { } // Busy waiting
        }

        protected void RestartWriteThread()
        {
            if (writeThread == null)
            {
                writeThread = new WriteThread(this);
                writeThread.Start();
                while (!writeThread.IsAlive) { } // Busy waiting
            }
        }

        protected void SetThreadsParams(UsbEndpoint inEndpoint, UsbEndpoint outEndpoint)
        {
            writeThread?.SetUsbEndpoint(outEndpoint);

            readThread?.SetUsbEndpoint(inEndpoint);
        }

        public static UsbSpiDevice CreateUsbSerialDevice(UsbDevice device, UsbDeviceConnection connection)
        {
            return CreateUsbSerialDevice(device, connection, -1);
        }

        public static UsbSpiDevice CreateUsbSerialDevice(UsbDevice device, UsbDeviceConnection connection, int iface)
        {
            int vid = device.VendorId;
            int pid = device.ProductId;

            if (CP2130Ids.IsDeviceSupported(vid, pid))
            {
                return new CP2130SpiDevice(device, connection, iface);
            }
            else
            {
                return null;
            }
        }

        public abstract override void closeSPI();

        public abstract override bool connectSPI();

        public abstract override int getClockDivider();

        public abstract override int getSelectedSlave();

        public abstract override void readMISO(int lengthBuffer);

        public abstract override void selectSlave(int nSlave);

        public abstract override void setClock(int clockDivider);

        public override void setMISOCallback(UsbMISOCallback misoCallback)
        {
            readThread.SetCallback(misoCallback);
        }

        public abstract override void writeMOSI(byte[] buffer);

        public abstract override void writeRead(byte[] buffer, int lengthRead);

        /*
         * Kill workingThread; This must be called when closing a device
         */
        /*
         * Restart workingThread if it has been killed before
         */

        protected class ReadThread : AbstractWorkerThread
        {
            private UsbSpiDevice device;
            private UsbEndpoint inEndpoint;
            private UsbMISOCallback misoCallback;

            public ReadThread(UsbSpiDevice device)
            {
                this.device = device;
            }

            private void OnReceivedData(byte[] data)
            {
                misoCallback?.onReceivedData(data);
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
                    OnReceivedData(dataReceived);
                }
            }

            public void SetCallback(UsbMISOCallback misoCallback)
            {
                this.misoCallback = misoCallback;
            }

            public void SetUsbEndpoint(UsbEndpoint inEndpoint)
            {
                this.inEndpoint = inEndpoint;
            }
        }

        protected class WriteThread : AbstractWorkerThread
        {
            private UsbSpiDevice device;
            private UsbEndpoint outEndpoint;

            public WriteThread(UsbSpiDevice device)
            {
                this.device = device;
            }

            public override void DoRun()
            {
                byte[] data = device.serialBuffer.GetWriteBuffer();
                if (data.Length > 0)
                {
                    device.connection.BulkTransfer(outEndpoint, data, data.Length, USB_TIMEOUT);
                }
            }

            public void SetUsbEndpoint(UsbEndpoint outEndpoint)
            {
                this.outEndpoint = outEndpoint;
            }
        }
    }
}