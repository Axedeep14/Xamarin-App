using System;
using System.Text;

namespace com.felhr.utils
{
    public class HexData
    {
        private const string HEXES = "0123456789ABCDEF";
        private const string HEX_INDICATOR = "0x";
        private const string SPACE = " ";
        private static StringBuilder hex = new StringBuilder();
        private static object lockHexToString = new object();
        private static object lockBytesToString = new object();

        public HexData()
        {
        }

        public string HexToString(byte[] data)
        {
            lock (lockHexToString)
            {
                if (data != null)
                {
                    //StringBuilder hex = new StringBuilder(2 * data.Length);
                    hex.Clear();
                    for (int i = 0; i <= data.Length - 1; i++)
                    {
                        byte dataAtIndex = data[i];
                        //hex.Append(HEX_INDICATOR);
                        hex.Append(HEXES[(dataAtIndex & 0xF0) >> 4]).Append(HEXES[dataAtIndex & 0x0F]);
                        hex.Append(SPACE);
                    }
                    return hex.ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        public string BytesToString(byte[] array)
        {
            lock (lockBytesToString)
            {
                StringBuilder builder = new StringBuilder();
                if (array != null)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        builder.Append(" " + array[i].ToString());
                    }
                }
                return builder.ToString();
            }
        }

        public byte[] StringTobytes(string hexString)
        {
            string stringProcessed = hexString.Trim().Replace("0x", "");
            stringProcessed = stringProcessed.Replace("\\s+", "");
            byte[] data = new byte[stringProcessed.Length / 2];
            int i = 0;
            int j = 0;
            while (i <= stringProcessed.Length - 1)
            {
                byte character = (byte)int.Parse(stringProcessed.Substring(i, i + 2), System.Globalization.NumberStyles.HexNumber);
                data[j] = character;
                j++;
                i += 2;
            }
            return data;
        }

        public string HexToString(string id)
        {
            if (id.Length == 1)
            {
                return "000" + id;
            }

            if (id.Length == 2)
            {
                return "00" + id;
            }

            if (id.Length == 3)
            {
                return "0" + id;
            }
            else
            {
                return id;
            }
        }
    }

    public static class HexDump
    {
        private static char[] HEX_DIGITS = {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

        public static string DumpHexString(byte[] array)
        {
            return DumpHexString(array, 0, array.Length);
        }

        public static string DumpHexString(byte[] array, int offset, int length)
        {
            StringBuilder result = new StringBuilder();

            byte[] line = new byte[16];
            int lineIndex = 0;

            result.Append("\n0x");
            result.Append(ToHexString(offset));

            for (int i = offset; i < offset + length; i++)
            {
                if (lineIndex == 16)
                {
                    result.Append(" ");

                    for (int j = 0; j < 16; j++)
                    {
                        if (line[j] > ' ' && line[j] < '~')
                        {
                            result.Append(System.Text.Encoding.Default.GetString(line), j, 1);
                        }
                        else
                        {
                            result.Append(".");
                        }
                    }

                    result.Append("\n0x");
                    result.Append(ToHexString(i));
                    lineIndex = 0;
                }

                byte b = array[i];
                result.Append(" ");
                result.Append(HEX_DIGITS[(b >> 4) & 0x0F]);
                result.Append(HEX_DIGITS[b & 0x0F]);

                line[lineIndex++] = b;
            }

            if (lineIndex != 16)
            {
                int count = (16 - lineIndex) * 3;
                count++;
                for (int i = 0; i < count; i++)
                {
                    result.Append(" ");
                }

                for (int i = 0; i < lineIndex; i++)
                {
                    if (line[i] > ' ' && line[i] < '~')
                    {
                        result.Append(System.Text.Encoding.Default.GetString(line), i, 1);
                    }
                    else
                    {
                        result.Append(".");
                    }
                }
            }

            return result.ToString();
        }

        public static string ToHexString(byte[] byteArray)
        {
            return BitConverter.ToString(byteArray).Replace("-", "");
        }

        public static string ToHexString(byte[] byteArray, int offset, int length)
        {
            StringBuilder hex = new StringBuilder(length * 2);

            while ((offset < byteArray.Length) && (length > 0))
            {
                hex.AppendFormat("{0:x2}", byteArray[offset]);

                offset++;
                length--;
            }
            return hex.ToString();
        }

        public static string ToHexString(int i)
        {
            return ToHexString(ToByteArray(i));
        }

        public static string ToHexString(short i)
        {
            return ToHexString(ToByteArray(i));
        }

        public static byte[] ToByteArray(byte b)
        {
            return new byte[] { b };
        }

        public static byte[] ToByteArray(int i)
        {
            byte[] array = new byte[4];

            array[3] = (byte)(i & 0xFF);
            array[2] = (byte)((i >> 8) & 0xFF);
            array[1] = (byte)((i >> 16) & 0xFF);
            array[0] = (byte)((i >> 24) & 0xFF);

            return array;
        }

        public static byte[] ToByteArray(short i)
        {
            byte[] array = new byte[2];

            array[1] = (byte)(i & 0xFF);
            array[0] = (byte)((i >> 8) & 0xFF);

            return array;
        }
    }
}