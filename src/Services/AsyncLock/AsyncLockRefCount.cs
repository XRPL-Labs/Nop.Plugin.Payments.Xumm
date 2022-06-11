namespace Nop.Plugin.Payments.Xumm.Services.AsyncLock
{
    internal sealed class AsyncLockRefCount<T>
    {
        internal AsyncLockRefCount(T value)
        {
            RefCount = 1;
            Value = value;
        }

        public int RefCount { get; set; }
        public T Value { get; private set; }
    }
}
