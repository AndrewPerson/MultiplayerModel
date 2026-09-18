using MultiplayerModel.Actor;
using MultiplayerModel.Extension;

namespace MultiplayerModel.Rpc.Observables;

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
 * batch, and for passing a meaningful snapshot when forwarding <see cref="IRpcActorContainer.Watch()"/> calls to
 * <see cref="Subscribe"/>.
 * </remarks>
 */
public sealed partial class ActorWatchRegistry : IDisposable
{
    private sealed class TypeWatchEntry
    {
        public readonly List<TypeWatchSubscription> Subscriptions = [];
    }
    
    private sealed class WatchEntry
    {
        public readonly List<WatchSubscription> Subscriptions = [];
    }

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly Dictionary<Type, TypeWatchEntry> typeEntries = [];
    private readonly Dictionary<TypelessActorId, WatchEntry> entries = [];
    
    private readonly Dictionary<TypelessActorId, (Type, IActor?)> pendingChanges = [];

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

    public IDisposable Subscribe<T>(IObserver<(TypelessActorId, IActor?)> observer) where T : struct, ITypedActor<T>
    {
        var type = typeof(T);
        TypeWatchSubscription subscription;

        // The gate must be released before the snapshot is delivered: the delivery is a callback into
        // user code, which may unsubscribe (re-taking the non-reentrant gate).
        using (gate.EnterWaitScope())
        {
            ThrowIfDisposed();

            if (!typeEntries.TryGetValue(type, out var entry))
            {
                entry = new TypeWatchEntry();
                typeEntries[type] = entry;
            }

            subscription = new TypeWatchSubscription(this, type, entry, observer);
            entry.Subscriptions.Add(subscription);
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
    public void RecordChange(TypelessActorId id, Type type, IActor? actor)
    {
        using (gate.EnterWaitScope())
        {
            pendingChanges[id] = (type, actor);
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
        List<(TypelessActorId Id, Type Type, IActor? Actor)> batch;

        using (gate.EnterWaitScope())
        {
            if (pendingChanges.Count == 0)
            {
                return;
            }

            batch = pendingChanges
                .Select(kv => (Id: kv.Key, Type: kv.Value.Item1, Actor: kv.Value.Item2))
                .ToList();

            pendingChanges.Clear();
        }

        foreach (var (id, type, actor) in batch)
        {
            List<WatchSubscription> subscriptions = [];
            List<TypeWatchSubscription> typeSubscriptions = [];

            using (gate.EnterWaitScope())
            {
                if (entries.TryGetValue(id, out var entry))
                {
                    subscriptions = entry.Subscriptions.ToList();
                }
                
                if (typeEntries.TryGetValue(type, out var typeEntry))
                {
                    typeSubscriptions = typeEntry.Subscriptions.ToList();
                }
            }

            foreach (var subscription in typeSubscriptions)
            {
                subscription.Deliver(id, actor);
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
        List<TypeWatchSubscription> allTypeSubscriptions;
        List<WatchSubscription> allSubscriptions;

        using (gate.EnterWaitScope())
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            allTypeSubscriptions = typeEntries.Values.SelectMany(entry => entry.Subscriptions).ToList();
            allSubscriptions = entries.Values.SelectMany(entry => entry.Subscriptions).ToList();
            
            entries.Clear();
            typeEntries.Clear();
            
            pendingChanges.Clear();
        }
        
        foreach (var subscription in allTypeSubscriptions)
        {
            subscription.Complete();
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
}
