namespace MultiplayerModel.Extension;

public static class SemaphoreSlimExtension
{
    extension(SemaphoreSlim semaphore)
    {
        public WaitScope EnterWaitScope()
        {
            semaphore.Wait();
            return new(semaphore);
        }
    }

    public ref struct WaitScope(SemaphoreSlim semaphore) : IDisposable
    {
        private bool exited;

        public void Exit()
        {
            if (!Interlocked.Exchange(ref exited, true)) semaphore.Release();
        }

        public void Dispose() => Exit();
    }
}
