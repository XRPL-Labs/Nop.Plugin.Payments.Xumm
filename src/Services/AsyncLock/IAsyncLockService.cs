using System;
using System.Threading.Tasks;

namespace Nop.Plugin.Payments.Xumm.Services.AsyncLock
{
    public interface IAsyncLockService
    {
        Task<IDisposable> LockAsync(object key);
    }
}
