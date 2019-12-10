using System;

using Android.Hardware.Usb;
using Android.Util;

namespace com.felhr.usbserial
{
    public class CP2130SpiDevice : UsbSpiDevice
    {
        private const string CLASS_ID = nameof(CP2130SpiDevice);

        public const int CLOCK_12MHz = 0;
        public const int CLOCK_6MHz = 1;
        public const int CLOCK_3MHz = 2;
        public const int CLOCK_1_5MHz = 3;
        public const int CLOCK_750KHz = 4;
        public const int CLOCK_375KHz = 5;
        public const int CLOCK_187_5KHz = 6;
        public const int CLOCK_93_75KHz = 7;

        private const int BM_REQ_DEVICE_2_HOST = 0xc0;
        private const int BM_REQ_HOST_2_DEVICE = 0x40;

        private const int SET_SPI_WORD = 0x31;
        private const int SET_GPIO_CHIP_SELECT = 0x25;
        private const int GET_SPI_WORD = 0x30;

        private UsbInterface mInterface;
        private UsbEndpoint inEndpoint;
        private UsbEndpoint outEndpoint;
        private UsbRequest requestIN;

        private int currentChannel;

        public CP2130SpiDevice(UsbDevice device, UsbDeviceConnection connection) : this(device, connection, -1)
        {
        }

        public CP2130SpiDevice(UsbDevice device, UsbDeviceConnection connection, int iface) : base(device, connection)
        {
            mInterface = device.GetInterface(iface >= 0 ? iface : 0);
            currentChannel = 0;
        }

        public override bool connectSPI()
        {
            bool ret = openCP2130();

            if (!ret)
            {
                return false;
            }

            // Restart the working thread if it has been killed before and  get and claim interface
            RestartWorkingThread();
            RestartWriteThread();

            // Pass references to the threads
            SetThreadsParams(inEndpoint, outEndpoint);

            return true;
        }

        public override int getSelectedSlave()
        {
            return currentChannel;
        }

        public override void writeMOSI(byte[] buffer)
        {
            byte[] buffCommand = new byte[buffer.Length + 8];
            buffCommand[0] = 0x00;
            buffCommand[1] = 0x00;
            buffCommand[2] = 0x01;
            buffCommand[3] = (byte)0x80;
            buffCommand[4] = (byte)(buffer.Length & 0xff);
            buffCommand[5] = (byte)((buffer.Length >> 8) & 0xff);
            buffCommand[6] = (byte)((buffer.Length >> 16) & 0xff);
            buffCommand[7] = (byte)((buffer.Length >> 24) & 0xff);

            Array.Copy(buffer, 0, buffCommand, 8, buffer.Length);

            serialBuffer.PutWriteBuffer(buffCommand);
        }

        public override void setClock(int clockDivider)
        {
            switch (clockDivider)
            {
                case CLOCK_12MHz:
                    setSetSpiWord(currentChannel, CLOCK_12MHz);
                    break;

                case CLOCK_6MHz:
                    setSetSpiWord(currentChannel, CLOCK_6MHz);
                    break;

                case CLOCK_3MHz:
                    setSetSpiWord(currentChannel, CLOCK_3MHz);
                    break;

                case CLOCK_1_5MHz:
                    setSetSpiWord(currentChannel, CLOCK_1_5MHz);
                    break;

                case CLOCK_750KHz:
                    setSetSpiWord(currentChannel, CLOCK_750KHz);
                    break;

                case CLOCK_375KHz:
                    setSetSpiWord(currentChannel, CLOCK_375KHz);
                    break;

                case CLOCK_187_5KHz:
                    setSetSpiWord(currentChannel, CLOCK_187_5KHz);
                    break;

                case CLOCK_93_75KHz:
                    setSetSpiWord(currentChannel, CLOCK_93_75KHz);
                    break;
            }
        }

        public override void readMISO(int lengthBuffer)
        {
            byte[] buffCommand = new byte[8];
            buffCommand[0] = 0x00;
            buffCommand[1] = 0x00;
            buffCommand[2] = 0x00;
            buffCommand[3] = (byte)0x80;
            buffCommand[4] = (byte)(lengthBuffer & 0xff);
            buffCommand[5] = (byte)((lengthBuffer >> 8) & 0xff);
            buffCommand[6] = (byte)((lengthBuffer >> 16) & 0xff);
            buffCommand[7] = (byte)((lengthBuffer >> 24) & 0xff);

            serialBuffer.PutWriteBuffer(buffCommand);
        }

        public override void writeRead(byte[] buffer, int lengthRead)
        {
            byte[] buffCommand = new byte[8 + buffer.Length];
            buffCommand[0] = 0x00;
            buffCommand[1] = 0x00;
            buffCommand[2] = 0x02;
            buffCommand[3] = (byte)0x80;
            buffCommand[4] = (byte)(lengthRead & 0xff);
            buffCommand[5] = (byte)((lengthRead >> 8) & 0xff);
            buffCommand[6] = (byte)((lengthRead >> 16) & 0xff);
            buffCommand[7] = (byte)((lengthRead >> 24) & 0xff);

            Array.Copy(buffer, 0, buffCommand, 8, buffer.Length);

            serialBuffer.PutWriteBuffer(buffCommand);
        }

        public override void selectSlave(int nSlave)
        {
            if (nSlave > 10 || nSlave < 0)
            {
                Log.Info(CLASS_ID, "selected slave must be in 0-10 range");
                return;
            }

            setGpioChipSelect(nSlave, true);
        }

        public override int getClockDivider()
        {
            byte[] data = getSpiWord();
            return data[currentChannel] & 0x07;
        }

        public override void closeSPI()
        {
            KillWorkingThread();
            KillWriteThread();
            connection.ReleaseInterface(mInterface);
        }

        private bool openCP2130()
        {
            if (connection.ClaimInterface(mInterface, true))
            {
                Log.Info(CLASS_ID, "Interface succesfully claimed");
            }
            else
            {
                Log.Info(CLASS_ID, "Interface could not be claimed");
                return false;
            }

            // Assign endpoints
            int numberEndpoints = mInterface.EndpointCount;
            for (int i = 0; i <= numberEndpoints - 1; i++)
            {
                UsbEndpoint endpoint = mInterface.GetEndpoint(i);
                if (endpoint.Type == UsbAddressing.XferBulk
                        && endpoint.Direction == UsbAddressing.In)
                {
                    inEndpoint = endpoint;
                }
                else
                {
                    outEndpoint = endpoint;
                }
            }

            return true;
        }

        private void setSetSpiWord(int channel, int freq)
        {
            byte[] payload = new byte[2];

            if (channel >= 0 && channel <= 10)
            {
                payload[0] = (byte)channel;
            }
            else
            {
                Log.Info(CLASS_ID, "Channel not valid");
                return;
            }
            payload[1] = (byte)freq;
            payload[1] = (byte)(payload[1] | (1 << 3)); // Push pull chip select pin mode

            setControlCommandOut(SET_SPI_WORD, 0, 0, payload);
        }

        private void setGpioChipSelect(int channel, bool othersDisabled)
        {
            byte[] payload = new byte[2];

            if (channel >= 0 && channel <= 10)
            {
                payload[0] = (byte)channel;
            }
            else
            {
                Log.Info(CLASS_ID, "Channel not valid");
                return;
            }

            byte control;
            if (othersDisabled)
            {
                control = 0x02;
            }
            else
            {
                control = 0x01;
            }

            payload[1] = control;

            int ret = setControlCommandOut(SET_GPIO_CHIP_SELECT, 0x00, 0x00, payload);

            if (ret != -1)
            {
                currentChannel = channel;
            }
        }

        private byte[] getSpiWord()
        {
            return setControlCommandIn(GET_SPI_WORD, 0x00, 0x00, 2);
        }

        private int setControlCommandOut(int request, int value, int index, byte[] data)
        {
            int dataLength = 0;
            if (data != null)
            {
                dataLength = data.Length;
            }
            int response = connection.ControlTransfer((UsbAddressing)BM_REQ_HOST_2_DEVICE, request, value, mInterface.Id, data, dataLength, USB_TIMEOUT);
            Log.Info(CLASS_ID, "Control Transfer Response: " + response.ToString());
            return response;
        }

        private byte[] setControlCommandIn(int request, int value, int index, int length)
        {
            byte[] data = new byte[length];
            int response = connection.ControlTransfer((UsbAddressing)BM_REQ_DEVICE_2_HOST, request, value, mInterface.Id, data, length, USB_TIMEOUT);
            Log.Info(CLASS_ID, "Control Transfer Response: " + response.ToString());
            return data;
        }
    }
}