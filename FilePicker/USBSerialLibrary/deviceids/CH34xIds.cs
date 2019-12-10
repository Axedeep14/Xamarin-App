
namespace com.felhr.deviceids
{
    internal class CH34xIds
    {
        private CH34xIds()
        {
        }

        private static long[] ch34xDevices = Helpers.CreateTable(new long[] {
            Helpers.CreateDevice(0x4348, 0x5523),
            Helpers.CreateDevice(0x1a86, 0x7523),
            Helpers.CreateDevice(0x1a86, 0x5523),
            Helpers.CreateDevice(0x1a86, 0x0445) }
        );

        public static bool IsDeviceSupported(int vendorId, int productId)
        {
            return Helpers.Exists(ch34xDevices, vendorId, productId);
        }
    }
}