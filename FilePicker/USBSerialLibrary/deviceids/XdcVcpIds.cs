namespace com.felhr.deviceids
{
    internal static class XdcVcpIds
    {
        /*
	 * Werner Wolfrum (w.wolfrum@wolfrum-elektronik.de)
	 */

        /* Different products and vendors of XdcVcp family
        */
        private static  long[] xdcvcpDevices = Helpers.CreateTable(new long[] {
                Helpers.CreateDevice(0x264D, 0x0232), // VCP (Virtual Com Port)
                Helpers.CreateDevice(0x264D, 0x0120),  // USI (Universal Sensor Interface)
                Helpers.CreateDevice(0x0483, 0x5740) }//CC3D (STM)
        );

        public static bool IsDeviceSupported(int vendorId, int productId)
        {
            return Helpers.Exists(xdcvcpDevices, vendorId, productId);
        }
    }
}