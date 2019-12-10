using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace USBSerialLibrary.utils
{
    public class UsbSupport : Java.Lang.Object
    {
        public const int UsbClassAppSpec = 254;
        public const int UsbClassAudio = 1;
        public const int UsbClassCdcData = 10;
        public const int UsbClassComm = 2;
        public const int UsbClassContentSec = 13;
        public const int UsbClassCscid = 11;
        public const int UsbClassHid = 3;
        public const int UsbClassHub = 9;
        public const int UsbClassMassStorage = 8;
        public const int UsbClassMisc = 239;
        public const int UsbClassPerInterface = 0;
        public const int UsbClassPhysica = 5;
        public const int UsbClassPrinter = 7;
        public const int UsbClassStillImage = 6;
        public const int UsbClassVendorSpec = 255;
        public const int UsbClassVideo = 14;
        public const int UsbClassWirelessController = 234;
        public const int UsbDirOut = 0;
        public const int UsbDirIn = 128;
        public const int UsbEndpointDirMask = 128;
        public const int UsbEndpointNumberMask = 15;
        public const int UsbEndpointXferBulk = 2;
        public const int UsbEndpointXferControl = 0;
        public const int UsbEndpointXferInt = 3;
        public const int UsbEndpointXferIsoc = 1;
    }

    public enum Parity
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4,
        NotSet = -1
    }

    public enum StopBits
    {
        One = 1,
        OnePointFive = 3,
        Two = 2,
        NotSet = -1
    }
}