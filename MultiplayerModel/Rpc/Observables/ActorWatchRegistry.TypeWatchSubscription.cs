using MultiplayerModel.Actor;
using MultiplayerModel.Extension;

namespace MultiplayerModel.Rpc.Observables;

public sealed partial class ActorWatchRegistry
{
    /**
     * A single watcher on an actor. Detaches itself if its observer throws.
     */
    private sealed class TypeWatchSubscription(
        ActorWatchRegistry registry,
        Type type,
        TypeWatchEntry entry,
        IObserver<(TypelessActorId, IActor?)> observer
    ) : IDisposable
    {
        private bool finished;

        public void Deliver(TypelessActorId id, IActor? actor)
        {
            if (Volatile.Read(ref finished))
            {
                return;
            }

            try
            {
                observer.OnNext((id, actor));
            }
            catch
            {
                // Detach a misbehaving observer so it doesn't affect other watchers of the same actor.
                Dispose();
            }
        }

        public void Complete()
        {
            if (Interlocked.Exchange(ref finished, true))
            {
                return;
            }

            RemoveFromEntry();

            try
            {
                observer.OnCompleted();
            }
            catch
            {
                // Nothing sensible to do.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref finished, true))
            {
                return;
            }

            RemoveFromEntry();
        }

        private void RemoveFromEntry()
        {
            SemaphoreSlimExtension.WaitScope scope;

            try
            {
                scope = registry.gate.EnterWaitScope();
            }
            catch (ObjectDisposedException)
            {
                // The registry has been disposed; there is nothing left to clean up.
                return;
            }

            using (scope)
            {
                entry.Subscriptions.Remove(this);

                if (entry.Subscriptions.Count == 0)
                {
                    registry.typeEntries.Remove(type);
                }
            }
        }
    }
}