namespace com.felhr.deviceids
{
    internal class PL2303Ids
    {
        private PL2303Ids()
        {
        }

        private static long[] pl2303Devices = Helpers.CreateTable(new long[] {
                Helpers.CreateDevice(0x04a5, 0x4027),
                Helpers.CreateDevice(0x067b, 0x2303),
                Helpers.CreateDevice(0x067b, 0x04bb),
                Helpers.CreateDevice(0x067b, 0x1234),
                Helpers.CreateDevice(0x067b, 0xaaa0),
                Helpers.CreateDevice(0x067b, 0xaaa2),
                Helpers.CreateDevice(0x067b, 0x0611),
                Helpers.CreateDevice(0x067b, 0x0612),
                Helpers.CreateDevice(0x067b, 0x0609),
                Helpers.CreateDevice(0x067b, 0x331a),
                Helpers.CreateDevice(0x067b, 0x0307),
                Helpers.CreateDevice(0x067b, 0x0463),
                Helpers.CreateDevice(0x0557, 0x2008),
                Helpers.CreateDevice(0x0547, 0x2008),
                Helpers.CreateDevice(0x04bb, 0x0a03),
                Helpers.CreateDevice(0x04bb, 0x0a0e),
                Helpers.CreateDevice(0x056e, 0x5003),
                Helpers.CreateDevice(0x056e, 0x5004),
                Helpers.CreateDevice(0x0eba, 0x1080),
                Helpers.CreateDevice(0x0eba, 0x2080),
                Helpers.CreateDevice(0x0df7, 0x0620),
                Helpers.CreateDevice(0x0584, 0xb000),
                Helpers.CreateDevice(0x2478, 0x2008),
                Helpers.CreateDevice(0x1453, 0x4026),
                Helpers.CreateDevice(0x0731, 0x0528),
                Helpers.CreateDevice(0x6189, 0x2068),
                Helpers.CreateDevice(0x11f7, 0x02df),
                Helpers.CreateDevice(0x04e8, 0x8001),
                Helpers.CreateDevice(0x11f5, 0x0001),
                Helpers.CreateDevice(0x11f5, 0x0003),
                Helpers.CreateDevice(0x11f5, 0x0004),
                Helpers.CreateDevice(0x11f5, 0x0005),
                Helpers.CreateDevice(0x0745, 0x0001),
                Helpers.CreateDevice(0x078b, 0x1234),
                Helpers.CreateDevice(0x10b5, 0xac70),
                Helpers.CreateDevice(0x079b, 0x0027),
                Helpers.CreateDevice(0x0413, 0x2101),
                Helpers.CreateDevice(0x0e55, 0x110b),
                Helpers.CreateDevice(0x0731, 0x2003),
                Helpers.CreateDevice(0x050d, 0x0257),
                Helpers.CreateDevice(0x058f, 0x9720),
                Helpers.CreateDevice(0x11f6, 0x2001),
                Helpers.CreateDevice(0x07aa, 0x002a),
                Helpers.CreateDevice(0x05ad, 0x0fba),
                Helpers.CreateDevice(0x5372, 0x2303),
                Helpers.CreateDevice(0x03f0, 0x0b39),
                Helpers.CreateDevice(0x03f0, 0x3139),
                Helpers.CreateDevice(0x03f0, 0x3239),
                Helpers.CreateDevice(0x03f0, 0x3524),
                Helpers.CreateDevice(0x04b8, 0x0521),
                Helpers.CreateDevice(0x04b8, 0x0522),
                Helpers.CreateDevice(0x054c, 0x0437),
                Helpers.CreateDevice(0x11ad, 0x0001),
                Helpers.CreateDevice(0x0b63, 0x6530),
                Helpers.CreateDevice(0x0b8c, 0x2303),
                Helpers.CreateDevice(0x110a, 0x1150),
                Helpers.CreateDevice(0x0557, 0x2008)}
        );

        public static bool IsDeviceSupported(int vendorId, int productId)
        {
            return Helpers.Exists(pl2303Devices, vendorId, productId);
        }
    }
}