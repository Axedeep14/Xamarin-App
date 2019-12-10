namespace com.felhr.deviceids
{
    internal static class CP2130Ids
    {
        private static long[] cp2130Devices = Helpers.CreateTable(new long[] {
            Helpers.CreateDevice(0x10C4, 0x87a0) }
    );

        public static bool IsDeviceSupported(int vendorId, int productId)
        {
            return Helpers.Exists(cp2130Devices, vendorId, productId);
        }
    }
}