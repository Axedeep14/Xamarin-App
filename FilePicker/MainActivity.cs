using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using System.IO;
using Java.Lang;
using AlertDialog = Android.App.AlertDialog;
using Android.Content;

namespace FilePicker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        string _filePath="";
        TextView filepath;
        Button button;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            filepath = FindViewById<TextView>(Resource.Id.filePath);
            // Set our view from the "main" layout resource

            button = FindViewById<Button>(Resource.Id.filebtn);

            button.Click += (e , o) => {
                OnFileSelect();
            };
            
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected async void OnFileSelect()
        {
            try
            {
                    var crossFilePicker = Plugin.FilePicker.CrossFilePicker.Current;

                    var myResult = await crossFilePicker.PickFile();
                    System.Console.WriteLine("deepak 1                   " + myResult);
                    _filePath = myResult.FilePath;
                    System.Console.WriteLine("deepak 1                   " + _filePath);
                    if (!string.IsNullOrEmpty(myResult.FileName))//Just the file name, it doesn't has the path
                    {
                        _filePath = myResult.FilePath;
                        if (File.Exists(_filePath))
                        {
                            filepath.Text = File.ReadAllText(_filePath); ;
                        }
                    }
                else
                {
                    System.Console.WriteLine("deepak                   " + _filePath);
                    AlertDialog.Builder dialog = new AlertDialog.Builder(this);
                    AlertDialog alert = dialog.Create();
                    alert.SetTitle("Title");
                    alert.SetMessage("Simple Alert");
                    alert.SetButton("OK", (c, ev) =>
                    {
                        Intent refresh = new Intent(this, typeof(MainActivity));
                        refresh.AddFlags(ActivityFlags.NoAnimation);
                        Finish();
                        StartActivity(refresh);
                    });
                    alert.Show();
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("deepak       " + e);
            }
        }
    }
}