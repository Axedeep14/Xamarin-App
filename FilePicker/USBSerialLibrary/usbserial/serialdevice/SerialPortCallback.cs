using System.Collections.Generic;

namespace com.felhr.usbserial
{
    public interface SerialPortCallback
    {
        void OnSerialPortsDetected(List<UsbSerialDevice> serialPorts);
    }
}