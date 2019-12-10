using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;

using Java.Util.Concurrent.Atomic;

namespace com.felhr.usbserial
{
    public class SerialPortBuilder
    {
        internal const string ACTION_USB_PERMISSION = "com.felhr.usbserial.USB_PERMISSION";
        internal const int MODE_START = 0;
        internal const int MODE_OPEN = 1;

        internal static SerialPortBuilder serialPortBuilder;

        public List<UsbDeviceStatus> devices;
        public List<UsbSerialDevice> serialDevices = new List<UsbSerialDevice>();

        internal System.Collections.Concurrent.ConcurrentQueue<PendingUsbPermission> queuedPermissions = new System.Collections.Concurrent.ConcurrentQueue<PendingUsbPermission>();
        internal volatile bool processingPermission = false;

        internal PendingUsbPermission currentPendingPermission;

        internal UsbManager usbManager;
        internal SerialPortCallback serialPortCallback;

        internal int baudRate, dataBits, stopBits, parity, flowControl;
        internal int mode = 0;

        internal bool broadcastRegistered = false;

        internal static SerialPortBuilderBroadcastReceiver usbReceiver;

        private SerialPortBuilder(SerialPortCallback serialPortCallback)
        {
            this.serialPortCallback = serialPortCallback;
        }

        public static SerialPortBuilder CreateSerialPortBuilder(SerialPortCallback serialPortCallback)
        {
            if (serialPortBuilder == null)
            {
                serialPortBuilder = new SerialPortBuilder(serialPortCallback);
            }
            usbReceiver = new SerialPortBuilderBroadcastReceiver(serialPortBuilder);
            return serialPortBuilder;
        }

        public List<UsbDevice> GetPossibleSerialPorts(Context context)
        {
            usbManager = (UsbManager)context.GetSystemService(Context.UsbService);

            IDictionary<string, UsbDevice> allDevices = usbManager.DeviceList;
            List<UsbDevice> devices = allDevices.Values.Where(s => UsbSerialDevice.IsSupported(s)).ToList();

            return devices;
        }

        public bool GetSerialPorts(Context context)
        {
            InitReceiver(context);

            if (devices == null || devices.Count == 0)
            { // Not previous devices detected
                devices = new List<UsbDeviceStatus>();
                foreach (UsbDevice device in GetPossibleSerialPorts(context))
                {
                    devices.Add(new UsbDeviceStatus(device));
                }

                if (devices.Count == 0)
                {
                    return false;
                }

                foreach (UsbDeviceStatus deviceStatus in devices)
                {
                    queuedPermissions.Enqueue(CreateUsbPermission(context, deviceStatus));
                }

                if (!processingPermission)
                {
                    LaunchPermission();
                }
            }
            else
            { // Previous devices detected and maybe pending permissions intent launched
                List<UsbDeviceStatus> newDevices = new List<UsbDeviceStatus>();

                foreach (UsbDevice device in GetPossibleSerialPorts(context))
                {
                    if (!devices.Contains(new UsbDeviceStatus(device)))
                    {
                        newDevices.Add(new UsbDeviceStatus(device));
                    }
                }

                if (newDevices.Count == 0)
                {
                    return false;
                }

                foreach (UsbDeviceStatus deviceStatus in newDevices)
                {
                    queuedPermissions.Enqueue(CreateUsbPermission(context, deviceStatus));
                }

                devices.AddRange(newDevices);

                if (!processingPermission)
                {
                    LaunchPermission();
                }
            }

            return true;
        }

        public bool OpenSerialPorts(Context context, int baudRate, int dataBits,
            int stopBits, int parity, int flowControl)
        {
            this.baudRate = baudRate;
            this.dataBits = dataBits;
            this.stopBits = stopBits;
            this.parity = parity;
            this.flowControl = flowControl;
            this.mode = MODE_OPEN;
            return GetSerialPorts(context);
        }

        public bool DisconnectDevice(UsbSerialDevice usbSerialDevice)
        {
            usbSerialDevice.SyncClose();
            serialDevices.Remove(usbSerialDevice);
            return true;
        }

        public bool DisconnectDevice(UsbDevice usbDevice)
        {
            UsbSerialDevice optionalDevice = serialDevices.First(p => usbDevice.DeviceId == p.GetDeviceId());

            if (optionalDevice != null)
            {
                UsbSerialDevice disconnectedDevice = optionalDevice;
                disconnectedDevice.SyncClose();
                serialDevices.Remove(optionalDevice);
                serialPortBuilder.serialPortCallback.OnSerialPortsDetected(serialPortBuilder.serialDevices);

                return true;
            }
            return false;
        }

        public void UnregisterListeners(Context context)
        {
            if (broadcastRegistered)
            {
                context.UnregisterReceiver(usbReceiver);
                broadcastRegistered = false;
            }
        }

        private PendingUsbPermission CreateUsbPermission(Context context, UsbDeviceStatus usbDeviceStatus)
        {
            PendingIntent mPendingIntent = PendingIntent.GetBroadcast(context, 0, new Intent(ACTION_USB_PERMISSION), 0);
            PendingUsbPermission pendingUsbPermission = new PendingUsbPermission
            {
                pendingIntent = mPendingIntent,
                usbDeviceStatus = usbDeviceStatus
            };
            return pendingUsbPermission;
        }

        internal void LaunchPermission()
        {
            try
            {
                processingPermission=true;
                currentPendingPermission = queuedPermissions.Take(1).First();
                usbManager.RequestPermission(currentPendingPermission.usbDeviceStatus.usbDevice,
                        currentPendingPermission.pendingIntent);
            }
            catch (Java.Lang.InterruptedException)
            {
                //e.printStackTrace();
                processingPermission = false;
            }
        }

        private void InitReceiver(Context context)
        {
            if (!broadcastRegistered)
            {
                IntentFilter filter = new IntentFilter();
                filter.AddAction(ACTION_USB_PERMISSION);
                context.RegisterReceiver(usbReceiver, filter);
                broadcastRegistered = true;
            }
        }

        internal void CreateAllPorts(UsbDeviceStatus usbDeviceStatus)
        {
            int interfaceCount = usbDeviceStatus.usbDevice.InterfaceCount;
            for (int i = 0; i <= interfaceCount - 1; i++)
            {
                if (usbDeviceStatus.usbDeviceConnection == null)
                {
                    usbDeviceStatus.usbDeviceConnection = usbManager.OpenDevice(usbDeviceStatus.usbDevice);
                }

                UsbSerialDevice usbSerialDevice = UsbSerialDevice.CreateUsbSerialDevice(
                        usbDeviceStatus.usbDevice,
                        usbDeviceStatus.usbDeviceConnection,
                        i);

                serialDevices.Add(usbSerialDevice);
            }
        }
    }

    public class UsbDeviceStatus
    {
        public UsbDevice usbDevice;
        public UsbDeviceConnection usbDeviceConnection;
        public bool open;

        public UsbDeviceStatus(UsbDevice usbDevice)
        {
            this.usbDevice = usbDevice;
        }

        public override bool Equals(object obj)
        {
            UsbDeviceStatus usbDeviceStatus = (UsbDeviceStatus)obj;
            return usbDeviceStatus.usbDevice.DeviceId == usbDevice.DeviceId;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class PendingUsbPermission
    {
        public PendingIntent pendingIntent;
        public UsbDeviceStatus usbDeviceStatus;
    }

    public class InitSerialPortThread : Java.Lang.Thread
    {
        private SerialPortBuilder serialPortBuilder;
        private List<UsbSerialDevice> usbSerialDevices;

        public InitSerialPortThread(List<UsbSerialDevice> usbSerialDevices)
        {
            this.usbSerialDevices = usbSerialDevices;
        }

        public InitSerialPortThread(SerialPortBuilder serialPortBuilder)
        {
            this.serialPortBuilder = serialPortBuilder;
            usbSerialDevices = serialPortBuilder.serialDevices;
        }

        public override void Run()
        {
            int n = 1;
            foreach (UsbSerialDevice usbSerialDevice in usbSerialDevices)
            {
                if (!usbSerialDevice.IsOpen)
                {
                    if (usbSerialDevice.SyncOpen())
                    {
                        usbSerialDevice.SetBaudRate(serialPortBuilder.baudRate);
                        usbSerialDevice.SetDataBits(serialPortBuilder.dataBits);
                        usbSerialDevice.SetStopBits(serialPortBuilder.stopBits);
                        usbSerialDevice.SetParity(serialPortBuilder.parity);
                        usbSerialDevice.SetFlowControl(serialPortBuilder.flowControl);
                        usbSerialDevice.SetPortName(UsbSerialDevice.COM_PORT + n.ToString());
                        n++;
                    }
                }
            }
            serialPortBuilder.serialPortCallback.OnSerialPortsDetected(serialPortBuilder.serialDevices);
        }
    }

    public class SerialPortBuilderBroadcastReceiver : BroadcastReceiver
    {
        private SerialPortBuilder serialPortBuilder;

        public SerialPortBuilderBroadcastReceiver(SerialPortBuilder serialPortBuilder)
        {
            this.serialPortBuilder = serialPortBuilder;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action.Equals(SerialPortBuilder.ACTION_USB_PERMISSION))
            {
                bool granted = intent.Extras.GetBoolean(UsbManager.ExtraPermissionGranted);
                InitSerialPortThread initSerialPortThread;
                if (granted)
                {
                    serialPortBuilder.CreateAllPorts(serialPortBuilder.currentPendingPermission.usbDeviceStatus);
                    if (serialPortBuilder.queuedPermissions.Count > 0)
                    {
                        serialPortBuilder.LaunchPermission();
                    }
                    else
                    {
                        serialPortBuilder.processingPermission = false;
                        if (serialPortBuilder.mode == SerialPortBuilder.MODE_START)
                        {
                            serialPortBuilder.serialPortCallback.OnSerialPortsDetected(serialPortBuilder.serialDevices);
                        }
                        else
                        {
                            initSerialPortThread = new InitSerialPortThread(serialPortBuilder);
                            initSerialPortThread.Start();
                        }
                    }
                }
                else if (serialPortBuilder.queuedPermissions.Count > 0)
                {
                    serialPortBuilder.LaunchPermission();
                }
                else
                {
                    serialPortBuilder.processingPermission = false;
                    if (serialPortBuilder.mode == SerialPortBuilder.MODE_START)
                    {
                        serialPortBuilder.serialPortCallback.OnSerialPortsDetected(serialPortBuilder.serialDevices);
                    }
                    else
                    {
                        initSerialPortThread = new InitSerialPortThread(serialPortBuilder.serialDevices);
                        initSerialPortThread.Start();
                    }
                }
            }
        }
    }
}