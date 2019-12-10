using Android;
using Android.Content;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;

using System.Linq;

using AndroidResource = Android.Resource;

namespace LNG.CMRI.Utility
{
    public static class PermissionUtils
    {
        public const int RC_LOCATION_PERMISSIONS = 1000;

        public const int RC_WIFI_PERMISSIONS = 1001;

        public static readonly string[] LOCATION_PERMISSIONS = { Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation };

        public static readonly string[] WIFI_PERMISSIONS = { Manifest.Permission.ChangeWifiState, Manifest.Permission.AccessWifiState, Manifest.Permission.ChangeNetworkState, Manifest.Permission.AccessNetworkState, Manifest.Permission.Internet };

        public static bool AllPermissionsGranted(this Android.Content.PM.Permission[] grantResults)
        {
            if (grantResults.Length < 1)
            {
                return false;
            }

            return !grantResults.Any(result => result == Android.Content.PM.Permission.Denied);
        }

        public static bool HasLocationPermissions(this Context context)
        {
            foreach (var perm in LOCATION_PERMISSIONS)
            {
                if (ContextCompat.CheckSelfPermission(context, perm) != Android.Content.PM.Permission.Granted)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool HasWIFIPermissions(this Context context)
        {
            foreach (var perm in WIFI_PERMISSIONS)
            {
                if (ContextCompat.CheckSelfPermission(context, perm) != Android.Content.PM.Permission.Granted)
                {
                    return false;
                }
            }
            return true;
        }

        public static void RequestPermissionsForApp(this Android.Support.V4.App.Fragment frag)
        {
            var showRequestRationale = ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, Manifest.Permission.AccessFineLocation) ||
                                       ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, Manifest.Permission.AccessCoarseLocation);

            if (showRequestRationale)
            {
                var rootView = frag.Activity.FindViewById(AndroidResource.Id.Content);
                Snackbar.Make(rootView, "Location Permissions Needed.", Snackbar.LengthIndefinite)
                        .SetAction("OK", v =>
                        {
                            frag.RequestPermissions(LOCATION_PERMISSIONS, RC_LOCATION_PERMISSIONS);
                        })
                        .Show();
            }
            else
            {
                frag.RequestPermissions(LOCATION_PERMISSIONS, RC_LOCATION_PERMISSIONS);
            }
        }

        public static void RequestPermissionsForApp(this Android.Support.V4.App.Fragment frag, string[] permissions)
        {
            //var showRequestRationale = ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, Manifest.Permission.AccessFineLocation) ||
            //                           ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, Manifest.Permission.AccessCoarseLocation);

            bool showRequestRationale = false;

            for (int i = 0; i < permissions.Length; i++)
            {
                if (i == 0)
                {
                    showRequestRationale = ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, permissions[i]);
                }
                else
                {
                    showRequestRationale = showRequestRationale || ActivityCompat.ShouldShowRequestPermissionRationale(frag.Activity, permissions[i]);
                }
            }

            if (showRequestRationale)
            {
                var rootView = frag.Activity.FindViewById(AndroidResource.Id.Content);
                Snackbar.Make(rootView, "Location Permissions Needed.", Snackbar.LengthIndefinite)
                        .SetAction("OK", v =>
                        {
                            frag.RequestPermissions(permissions, RC_WIFI_PERMISSIONS);
                        })
                        .Show();
            }
            else
            {
                frag.RequestPermissions(permissions, RC_WIFI_PERMISSIONS);
            }
        }
    }
}