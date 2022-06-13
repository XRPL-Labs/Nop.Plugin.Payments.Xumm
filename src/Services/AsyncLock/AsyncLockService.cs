using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Xumm.Services.AsyncLock
{
    internal sealed class AsyncLockService
    {
        private static readonly Dictionary<object, AsyncLockRefCount<SemaphoreSlim>> _semaphoreSlims = new();

        private static SemaphoreSlim GetOrCreate(object key)
        {
            AsyncLockRefCount<SemaphoreSlim> item;
            lock (_semaphoreSlims)
            {
                if (_semaphoreSlims.TryGetValue(key, out item))
                    ++item.RefCount;
                else
                {
                    item = new AsyncLockRefCount<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                    _semaphoreSlims[key] = item;
                }
            }

            return item.Value;
        }

        public async Task<IDisposable> LockAsync(object key)
        {
            await GetOrCreate(key).WaitAsync().ConfigureAwait(false);
            return new AsyncLockReleaser { Key = key };
        }

        private sealed class AsyncLockReleaser : IDisposable
        {
            public object Key { get; set; }

            public void Dispose()
            {
                AsyncLockRefCount<SemaphoreSlim> item;
                lock (_semaphoreSlims)
                {
                    item = _semaphoreSlims[Key];
                    --item.RefCount;
                    if (item.RefCount == 0)
                    {
                        _semaphoreSlims.Remove(Key);
                    }
                }
                item.Value.Release();
            }
        }
    }
}
