using Android.App;
using Android.OS;

namespace LNG.CMRI
{
    /// <summary>
    /// Defines the <see cref="UsbBinder" />
    /// </summary>
    public class SerialBinder : Binder
    {
        private Service service;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbBinder"/> class.
        /// </summary>
        /// <param name="usbService">The usbService<see cref="LGUsbService"/></param>
        public SerialBinder(Service service)
        {
            this.service = service;
        }

        /// <summary>
        /// The GetService
        /// </summary>
        /// <returns>The <see cref="LGUsbService"/></returns>
        public Service GetService()
        {
            return service;
        }
    }
}