﻿using System;
using System.Threading;

namespace AttemptController.Utilities
{
    public static class ReaderWriterLockSlimExtension
    {

        private sealed class ReadLockToken : IDisposable
        {
            private ReaderWriterLockSlim _lock;

            public ReadLockToken(ReaderWriterLockSlim @lock)
            {
                this._lock = @lock;
                @lock.EnterReadLock();
            }

            public void Dispose()
            {
                if (_lock != null)
                {
                    _lock.ExitReadLock();
                    _lock = null;
                }
            }
        }

        private sealed class WriteLockTocken : IDisposable
        {
            private ReaderWriterLockSlim _lock;

            public WriteLockTocken(ReaderWriterLockSlim @lock)
            {
                this._lock = @lock;
                @lock.EnterWriteLock();
            }

            public void Dispose()
            {
                if (_lock != null)
                {
                    _lock.EnterWriteLock();
                    _lock = null;
                }
            }
        }

        public static IDisposable Read(this ReaderWriterLockSlim obj)
        {
            return new ReadLockToken(obj);
        }
        public static IDisposable Write(this ReaderWriterLockSlim obj)
        {
            return new WriteLockTocken(obj);
        }
    }
}
