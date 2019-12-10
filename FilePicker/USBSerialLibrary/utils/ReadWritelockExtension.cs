using System;
using System.Threading;

namespace USBSerialLibrary.utils
{
    /// <summary>
    /// Defines the <see cref="RWLSExtension" />
    /// </summary>
    public static class RWLSExtension
    {
        /// <summary>
        /// The ReadLock
        /// </summary>
        /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
        /// <returns>The <see cref="ReadLockHelper"/></returns>
        public static ReadLockHelper ReadLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new ReadLockHelper(readerWriterLock);
        }

        /// <summary>
        /// The UpgradableReadLock
        /// </summary>
        /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
        /// <returns>The <see cref="UpgradeableReadLockHelper"/></returns>
        public static UpgradeableReadLockHelper UpgradableReadLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new UpgradeableReadLockHelper(readerWriterLock);
        }

        /// <summary>
        /// The WriteLock
        /// </summary>
        /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
        /// <returns>The <see cref="WriteLockHelper"/></returns>
        public static WriteLockHelper WriteLock(this ReaderWriterLockSlim readerWriterLock)
        {
            return new WriteLockHelper(readerWriterLock);
        }

        /// <summary>
        /// Defines the <see cref="ReadLockHelper" />
        /// </summary>
        public struct ReadLockHelper : IDisposable
        {
            private readonly ReaderWriterLockSlim readerWriterLock;

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            public ReadLockHelper(ReaderWriterLockSlim readerWriterLock)
            {
                readerWriterLock.EnterReadLock();
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            /// <param name="timeout">The timeout<see cref="int"/></param>
            public ReadLockHelper(ReaderWriterLockSlim readerWriterLock, int timeout)
            {
                if (!readerWriterLock.TryEnterReadLock(timeout))
                {
                    throw new TimeoutException("LockTimeoutException");
                }
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// The Dispose
            /// </summary>
            public void Dispose()
            {
                this.readerWriterLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Defines the <see cref="UpgradeableReadLockHelper" />
        /// </summary>
        public struct UpgradeableReadLockHelper : IDisposable
        {
            private readonly ReaderWriterLockSlim readerWriterLock;

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            public UpgradeableReadLockHelper(ReaderWriterLockSlim readerWriterLock)
            {
                readerWriterLock.EnterUpgradeableReadLock();
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            /// <param name="timeout">The timeout<see cref="int"/></param>
            public UpgradeableReadLockHelper(ReaderWriterLockSlim readerWriterLock, int timeout)
            {
                if (!readerWriterLock.TryEnterUpgradeableReadLock(timeout))
                {
                    throw new TimeoutException("LockTimeoutException");
                }
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// The Dispose
            /// </summary>
            public void Dispose()
            {
                this.readerWriterLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Defines the <see cref="WriteLockHelper" />
        /// </summary>
        public struct WriteLockHelper : IDisposable
        {
            private readonly ReaderWriterLockSlim readerWriterLock;

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            public WriteLockHelper(ReaderWriterLockSlim readerWriterLock)
            {
                readerWriterLock.EnterWriteLock();
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref=""/> class.
            /// </summary>
            /// <param name="readerWriterLock">The readerWriterLock<see cref="ReaderWriterLockSlim"/></param>
            /// <param name="timeout">The timeout<see cref="int"/></param>
            public WriteLockHelper(ReaderWriterLockSlim readerWriterLock, int timeout)
            {
                if (readerWriterLock.TryEnterWriteLock(timeout))
                {
                    throw new TimeoutException("LockTimeoutException");
                }
                this.readerWriterLock = readerWriterLock;
            }

            /// <summary>
            /// The Dispose
            /// </summary>
            public void Dispose()
            {
                this.readerWriterLock.ExitWriteLock();
            }
        }
    }
}