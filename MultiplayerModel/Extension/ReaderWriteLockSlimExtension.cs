namespace MultiplayerModel.Extension;

public static class ReaderWriteLockSlimExtension
{
    extension(ReaderWriterLockSlim readerWriterLock)
    {
        public ReadScope EnterReadScope()
        {
            readerWriterLock.EnterReadLock();
            return new(readerWriterLock);
        }
        
        public UpgradeableReadScope EnterUpgradeableReadScope()
        {
            readerWriterLock.EnterUpgradeableReadLock();
            return new(readerWriterLock);
        }
        
        public WriteScope EnterWriteScope()
        {
            readerWriterLock.EnterWriteLock();
            return new(readerWriterLock);
        }
    }

    public ref struct ReadScope(ReaderWriterLockSlim readerWriterLock) : IDisposable
    {
        private bool exited;

        public void Exit()
        {
            if (Interlocked.Exchange(ref exited, true) == false) readerWriterLock.ExitReadLock();
        }

        public void Dispose() => Exit();
    }
    
    public ref struct UpgradeableReadScope(ReaderWriterLockSlim readerWriterLock) : IDisposable
    {
        private bool exited;

        public void Exit()
        {
            if (Interlocked.Exchange(ref exited, true) == false) readerWriterLock.ExitUpgradeableReadLock();
        }

        public void Dispose() => Exit();
    }
    
    public ref struct WriteScope(ReaderWriterLockSlim readerWriterLock) : IDisposable
    {
        private bool exited;

        public void Exit()
        {
            if (Interlocked.Exchange(ref exited, true) == false) readerWriterLock.ExitWriteLock();
        }

        public void Dispose() => Exit();
    }
}