namespace Shared
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static Disposable EnterRead(this ReaderWriterLockSlim lockManager)
        {
            lockManager.EnterReadLock();
            return Disposable.Create(() => lockManager.ExitReadLock());
        }

        public static Disposable EnterWrite(this ReaderWriterLockSlim lockManager)
        {
            lockManager.EnterWriteLock();
            return Disposable.Create(() => lockManager.ExitWriteLock());
        }

        public static Disposable EnterUpgradeableRead(this ReaderWriterLockSlim lockManager)
        {
            lockManager.EnterUpgradeableReadLock();
            return Disposable.Create(() => lockManager.ExitUpgradeableReadLock());
        }
    }
}