using System;

namespace com.felhr.deviceids
{
    static class Helpers
    {
        /**
         * Create a device id, since they are 4 bytes each, we can pack the pair in an long.
         */
        public static long CreateDevice(int vendorId, int productId)
        {
            //return (((long)vendorId) << 32) | (productId & 0xFFFF_FFFFL);
            long HI_WORD = vendorId << 32;
            long LO_WORD = productId & 0xFFFFFFFFL;
            long RESULT = HI_WORD | LO_WORD;
            return RESULT;
        }

        /**
     * Creates a sorted table.
     * This way, we can use binarySearch to find whether the entry exists.
     */
        public static long[] CreateTable(long[] entries)
        {
            Array.Sort(entries);
            return entries;
        }

        public static bool Exists(long[] devices, int vendorId, int productId)
        {
            return Array.BinarySearch(devices, CreateDevice(vendorId, productId)) >= 0;
        }
    }
}