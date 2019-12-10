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
using Java.Lang;

namespace com.felhr.usbserial
{
    public abstract class AbstractWorkerThread:Thread
    {
        protected bool firstTime = true;
        private volatile bool keep = true;
        private volatile Thread workingThread;

        public void StopThread()
        {
            keep = false;
            this.workingThread?.Interrupt();
        }

        public override void Run()
        {
            if (!this.keep)
            {
                return;
            }
            this.workingThread = Thread.CurrentThread();
            while (this.keep && (!this.workingThread.IsInterrupted))
            {
                DoRun();
            }
            Android.Util.Log.Info("UsbSerialDevice", this.Class.Name + " Thread has Died.");
        }

        public abstract void DoRun();
    }
}