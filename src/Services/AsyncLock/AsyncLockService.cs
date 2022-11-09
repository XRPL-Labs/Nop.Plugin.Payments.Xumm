using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Xumm.Services.AsyncLock;

internal sealed class AsyncLockService
{
    private static readonly Dictionary<object, AsyncLockRefCount<SemaphoreSlim>> SemaphoreSlims = new();

    internal async Task<IDisposable> LockAsync(object key)
    {
        await GetOrCreate(key).WaitAsync().ConfigureAwait(false);
        return new AsyncLockReleaser { Key = key };
    }

    private static SemaphoreSlim GetOrCreate(object key)
    {
        AsyncLockRefCount<SemaphoreSlim> item;
        lock (SemaphoreSlims)
        {
            if (SemaphoreSlims.TryGetValue(key, out item))
            {
                ++item.RefCount;
            }
            else
            {
                item = new AsyncLockRefCount<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                SemaphoreSlims[key] = item;
            }
        }

        return item.Value;
    }

    private sealed class AsyncLockReleaser : IDisposable
    {
        public object Key { get; init; }

        public void Dispose()
        {
            AsyncLockRefCount<SemaphoreSlim> item;
            lock (SemaphoreSlims)
            {
                item = SemaphoreSlims[Key];
                --item.RefCount;
                if (item.RefCount == 0)
                {
                    SemaphoreSlims.Remove(Key);
                }
            }
            item.Value.Release();
        }
    }
}
