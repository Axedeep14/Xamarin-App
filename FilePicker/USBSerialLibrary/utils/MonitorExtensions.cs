using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace USBSerialLibrary.utils
{
    public static class MonitorExtension
    {
        public static SyncLockHelper Lock(this object readerWriterLock)
        {
            return new SyncLockHelper(readerWriterLock);
        }

        public static SyncLockHelper Lock(this object readerWriterLock, int timeout)
        {
            return new SyncLockHelper(readerWriterLock, timeout);
        }

        public struct SyncLockHelper : IDisposable
        {
            public object MonitorObject { get; }

            public SyncLockHelper(object readerWriterLock)
            {
                Monitor.Enter(readerWriterLock);
                this.MonitorObject = readerWriterLock;
            }

            public SyncLockHelper(object readerWriterLock, int timeout)
            {
                if (!Monitor.TryEnter(readerWriterLock, timeout))
                {
                    throw new TimeoutException("LockTimeoutException");
                }
                this.MonitorObject = readerWriterLock;
            }

            public void Dispose()
            {
                Monitor.Exit(MonitorObject);
            }
        }
    }
}