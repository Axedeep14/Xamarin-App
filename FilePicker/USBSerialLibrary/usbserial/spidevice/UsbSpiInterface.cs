namespace com.felhr.usbserial
{
    public abstract class UsbSpiInterface
    {
        // Clock dividers;
        public const int DIVIDER_2 = 2;

        public const int DIVIDER_4 = 4;
        public const int DIVIDER_8 = 8;
        public const int DIVIDER_16 = 16;
        public const int DIVIDER_32 = 32;
        public const int DIVIDER_64 = 64;
        public const int DIVIDER_128 = 128;

        // Common SPI operations
        public abstract bool connectSPI();

        public abstract void writeMOSI(byte[] buffer);

        public abstract void readMISO(int lengthBuffer);

        public abstract void writeRead(byte[] buffer, int lenghtRead);

        public abstract void setClock(int clockDivider);

        public abstract void selectSlave(int nSlave);

        public abstract void setMISOCallback(UsbMISOCallback misoCallback);

        public abstract void closeSPI();

        // Status information
        public abstract int getClockDivider();

        public abstract int getSelectedSlave();
    }

    public interface UsbMISOCallback
    {
        int onReceivedData(byte[] data);
    }
}