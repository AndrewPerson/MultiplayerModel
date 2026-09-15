using MultiplayerModel.Actor;
using MultiplayerModel.Extension;

namespace MultiplayerModel.Rpc;

/**
 * Tracks per-actor watches and batches change notifications.
 *
 * <remarks>
 * Changes are recorded via <see cref="RecordChange"/> as they happen and are aggregated per actor (last write wins)
 * until <see cref="ReleaseChanges"/> is called, at which point at most one notification is delivered per actor. This
 * allows a container to apply an entire batch of changes (e.g. a server message ordering on a client) and release a
 * single aggregated change per actor.
 *
 * The owning container is responsible for calling <see cref="ReleaseChanges"/> at the end of each change
 * batch, and for passing a meaningful snapshot when forwarding <see cref="IRpcActorContainer.Watch"/> calls to
 * <see cref="Subscribe"/>.
 * </remarks>
 */
internal sealed class ActorWatchRegistry : IDisposable
{
    private sealed class WatchEntry
    {
        public readonly List<WatchSubscription> Subscriptions = [];
    }

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<TypelessActorId, WatchEntry> entries = [];
    private readonly Dictionary<TypelessActorId, IActor?> pendingChanges = [];

    private bool disposed;

    /**
     * Subscribes <paramref name="observer"/> to changes of the actor with <paramref name="id"/>.
     *
     * <remarks>
     * If <paramref name="snapshot"/> is not null, it is delivered to the observer immediately (before this
     * method returns). The owning container should pass the actor's current value, computed while holding a
     * lock that also serialises <see cref="RecordChange"/> calls in order to order the snapshot before
     * subsequently recorded changes without having to compare values.
     * </remarks>
     */
    public IDisposable Subscribe(TypelessActorId id, IObserver<IActor?> observer, IActor? snapshot)
    {
        WatchSubscription subscription;

        // The gate must be released before the snapshot is delivered: the delivery is a callback into
        // user code, which may unsubscribe (re-taking the non-reentrant gate).
        using (gate.EnterWaitScope())
        {
            ThrowIfDisposed();

            if (!entries.TryGetValue(id, out var entry))
            {
                entry = new WatchEntry();
                entries[id] = entry;
            }

            subscription = new WatchSubscription(this, id, entry, observer);
            entry.Subscriptions.Add(subscription);
        }

        if (snapshot is not null)
        {
            subscription.Deliver(snapshot);
        }

        return subscription;
    }

    /**
     * Records a change of the actor with <paramref name="id"/>. <c>null</c> means the actor was removed.
     *
     * <remarks>
     * Changes are aggregated per actor until <see cref="ReleaseChanges"/> is called; only the last recorded
     * value per actor is delivered.
     * </remarks>
     */
    public void RecordChange(TypelessActorId id, IActor? actor)
    {
        using (gate.EnterWaitScope())
        {
            pendingChanges[id] = actor;
        }
    }

    /**
     * Delivers all recorded changes (at most one per actor) to their watchers, then clears them.
     *
     * <remarks>
     * Delivery happens on the calling thread, outside of this registry's own gate, in no particular order.
     * </remarks>
     */
    public void ReleaseChanges()
    {
        List<(TypelessActorId Id, IActor? Actor)> batch;

        using (gate.EnterWaitScope())
        {
            if (pendingChanges.Count == 0)
            {
                return;
            }

            batch = pendingChanges
                .Select(kv => (Id: kv.Key, Actor: kv.Value))
                .ToList();

            pendingChanges.Clear();
        }

        foreach (var (id, actor) in batch)
        {
            List<WatchSubscription> subscriptions;

            using (gate.EnterWaitScope())
            {
                if (!entries.TryGetValue(id, out var entry))
                {
                    continue;
                }

                subscriptions = entry.Subscriptions.ToList();
            }

            foreach (var subscription in subscriptions)
            {
                subscription.Deliver(actor);
            }
        }
    }

    /**
     * Completes all subscriptions and drops any pending changes.
     */
    public void Dispose()
    {
        List<WatchSubscription> allSubscriptions;

        using (gate.EnterWaitScope())
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            allSubscriptions = entries.Values.SelectMany(entry => entry.Subscriptions).ToList();
            entries.Clear();
            pendingChanges.Clear();
        }

        foreach (var subscription in allSubscriptions)
        {
            subscription.Complete();
        }

        // subscription.Complete uses gate, so we can only dispose it after all subscriptions have completed
        gate.Dispose();
    }

    /// <remarks>Needs to be called after <see cref="gate"/> has been locked</remarks>
    /// <exception cref="ObjectDisposedException"></exception>
    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ActorWatchRegistry));
        }
    }

    /**
     * A single watcher on an actor. Detaches itself if its observer throws.
     */
    private sealed class WatchSubscription(
        ActorWatchRegistry registry,
        TypelessActorId id,
        WatchEntry entry,
        IObserver<IActor?> observer
    ) : IDisposable
    {
        private bool finished;

        public void Deliver(IActor? actor)
        {
            if (Volatile.Read(ref finished))
            {
                return;
            }

            try
            {
                observer.OnNext(actor);
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
                    registry.entries.Remove(id);
                }
            }
        }
    }
}

/**
 * Adapts an untyped <see cref="IActor"/> watch to a typed <see cref="T"/> observer.
 */
internal sealed class TypedActorObserver<T>(IObserver<T?> observer) : IObserver<IActor?>
    where T : struct
{
    public void OnCompleted() => observer.OnCompleted();

    public void OnError(Exception error) => observer.OnError(error);

    public void OnNext(IActor? value)
    {
        switch (value)
        {
            case null:
                observer.OnNext(null);
                break;
            case T typed:
                observer.OnNext(typed);
                break;
            default:
                throw new InvalidOperationException(
                    $"Actor {value.Id} was expected to be of type {typeof(T).Name}, but was {value.GetType().Name}.");
        }
    }
}

/**
 * A minimal <see cref="IObservable{T}"/> backed by a subscription factory.
 */
internal sealed class DelegateObservable<T>(Func<IObserver<T>, IDisposable> onSubscribe) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer) => onSubscribe(observer);
}
