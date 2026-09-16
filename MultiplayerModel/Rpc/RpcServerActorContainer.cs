using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;
using MultiplayerModel.Extension;
using MultiplayerModel.Transport.Server;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Rpc;

/**
 * <remarks>
 * All methods on this class are thread-safe, although they may not be very efficient in their thread-safety.
 * </remarks>
 */
public class RpcServerActorContainer : IRpcActorContainer, IListActorsHandler, IActorDownloadHandler, IDisposable
{
    public uint Id => 0;

    public RpcActorIdGenerator ActorIdGenerator { get; } = new(0);
    public RpcMessageIdGenerator MessageIdGenerator { get; } = new(0);

    public IServerTransport Transport { get; }

    private readonly ReaderWriterLockSlim actorsLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly Dictionary<TypelessActorId, IActor> actors = [];
    private readonly ActorWatchRegistry actorWatchRegistry = new();

    private readonly ChangeCalculationActorContainer nextMessageOrderingContainer;
    private long messageOrderingVersion;

    private CancellationTokenSource? backgroundTaskCanceller;
    private bool running;

    private bool disposedValue;

    public RpcServerActorContainer(IServerTransport transport)
    {
        Transport = transport;
        transport.ListActorsHandler = this;
        transport.DownloadHandler = this;

        nextMessageOrderingContainer = new(this);
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref running, true))
        {
            throw new InvalidOperationException($"{nameof(RpcServerActorContainer)} is already running.");
        }

        try
        {
            backgroundTaskCanceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var backgroundCancellationToken = backgroundTaskCanceller.Token;

            async Task MessageOrdering()
            {
                while (!backgroundCancellationToken.IsCancellationRequested)
                {
                    var sleepTask = Task.Delay(100, backgroundCancellationToken);

                    List<(TypelessActorId, IMessage)> appliedMessages;
                    Dictionary<TypelessActorId, int?> dirtyActorHashes;

                    using (actorsLock.EnterWriteScope())
                    {
                        appliedMessages = nextMessageOrderingContainer.AppliedMessages.ToList();
                        dirtyActorHashes = nextMessageOrderingContainer.DirtyActors.ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value?.StableHash()
                        );

                        nextMessageOrderingContainer.ApplyTo(actors);
                        nextMessageOrderingContainer.ReCreate();
                    }

                    if (appliedMessages.Count != 0 || dirtyActorHashes.Count != 0)
                    {
                        await Transport.SendMessageOrdering
                        (
                            appliedMessages.Select(tup => new AddressedMessage(tup.Item1, tup.Item2)).ToList(),
                            dirtyActorHashes,
                            Interlocked.Increment(ref messageOrderingVersion) - 1, // We want the value pre-increment
                            backgroundCancellationToken
                        );
                    }

                    await sleepTask;
                }

                backgroundCancellationToken.ThrowIfCancellationRequested();
            }

            async Task ReceiveMessages()
            {
                while (!backgroundCancellationToken.IsCancellationRequested)
                {
                    var (actorId, message) = await Transport.ReceiveMessage(backgroundCancellationToken);
                    SendMessage(actorId, message);
                }

                backgroundCancellationToken.ThrowIfCancellationRequested();
            }

            await await Task.WhenAny(Transport.Run(backgroundCancellationToken), MessageOrdering(), ReceiveMessages());
        }
        finally
        {
            backgroundTaskCanceller?.Cancel();
            Interlocked.Exchange(ref running, false);
        }
    }

    public T? SendMessage<T, TMessage>(ActorId<T> actorId, TMessage message)
        where T : struct, ITypedActor<T, TMessage> where TMessage : IMessage
    {
        T? newActor;

        using (actorsLock.EnterWriteScope())
        {
            newActor = nextMessageOrderingContainer.SendMessage(actorId, message);

            if (newActor is not null)
            {
                actorWatchRegistry.RecordChange(actorId, newActor);
            }
        }

        actorWatchRegistry.ReleaseChanges();

        return newActor;
    }

    public IActor? SendMessage(TypelessActorId actorId, IMessage message)
    {
        IActor? newActor;

        using (actorsLock.EnterWriteScope())
        {
            newActor = nextMessageOrderingContainer.SendMessage(actorId, message);

            if (newActor is not null)
            {
                actorWatchRegistry.RecordChange(actorId, newActor);
            }
        }

        actorWatchRegistry.ReleaseChanges();

        return newActor;
    }

    public IReadOnlySet<TypelessActorId> ListActors()
    {
        using (actorsLock.EnterReadScope())
        {
            var result = actors.Keys.ToHashSet();
            result.UnionWith(nextMessageOrderingContainer.DirtyActors.Keys);

            return result;
        }
    }

    public IReadOnlySet<ActorId<T>> ListActors<T>() where T : struct, ITypedActor<T>
    {
        using (actorsLock.EnterReadScope())
        {
            var result = actors
                .Where(kv => kv.Value is T)
                .Select(kv => new ActorId<T>(kv.Key.ContainerId, kv.Key.LocalId))
                .ToHashSet();

            result.UnionWith(
                nextMessageOrderingContainer.DirtyActors
                    .Where(kv => kv.Value is T)
                    .Select(kv => new ActorId<T>(kv.Key.ContainerId, kv.Key.LocalId))
            );

            return result;
        }
    }

    public IObservable<T?> Watch<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        TypelessActorId typelessId = id;

        return new DelegateObservable<T?>(observer =>
        {
            // Holding the read lock while subscribing orders the initial snapshot before any subsequently
            // recorded changes, without comparing values.
            using (actorsLock.EnterReadScope())
            {
                return actorWatchRegistry.Subscribe(typelessId, new TypedActorObserver<T>(observer), GetActorForWatch(typelessId));
            }
        });
    }

    /**
     * Gets the effective value of an actor (pending changes preferred over committed ones) for use as a watch
     * snapshot.
     *
     * <remarks>Must be called with the read lock held. In contrast to <see cref="TryGetActor"/>, this does not
     * copy the actor into the pending container.</remarks>
     */
    private IActor? GetActorForWatch(TypelessActorId id)
    {
        if (nextMessageOrderingContainer.DirtyActors.TryGetValue(id, out var pendingActor))
        {
            // A null value means the actor has been removed.
            return pendingActor;
        }

        return actors.GetValueOrDefault(id);
    }

    public bool ContainsActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        using (actorsLock.EnterReadScope())
        {
            if (nextMessageOrderingContainer.DirtyActors.TryGetValue(id, out var unackedActor))
            {
                if (unackedActor is T) return true;
                if (unackedActor is null) return false; // A null value means the actor has been removed
            }

            return actors.TryGetValue(id, out var actor) && actor is T;
        }
    }

    public bool ContainsActor(TypelessActorId id)
    {
        using (actorsLock.EnterReadScope())
        {
            if (nextMessageOrderingContainer.DirtyActors.TryGetValue(id, out var actor))
            {
                return actor is not null;
            }

            return actors.ContainsKey(id);
        }
    }

    public T GetActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        if (TryGetActor(id, out var actor))
        {
            return actor;
        }

        throw new KeyNotFoundException();
    }

    public bool TryGetActor<T>(ActorId<T> id, out T actor) where T : struct, ITypedActor<T>
    {
        using (actorsLock.EnterReadScope())
        {
            if (nextMessageOrderingContainer.DirtyActors.TryGetValue(id, out var untypedUnackedActor))
            {
                // A null value means the actor has been removed
                if (untypedUnackedActor is null)
                {
                    actor = default;
                    return false;
                }

                if (untypedUnackedActor is T typedUnackedActor)
                {
                    actor = typedUnackedActor;
                    return true;
                }
            }

            if (actors.TryGetValue(id, out var untypedActor) && untypedActor is T typedActor)
            {
                actor = typedActor;
                return true;
            }
        }

        actor = default;
        return false;
    }

    public bool TryGetActor(TypelessActorId id, [MaybeNullWhen(false)] out IActor actor)
    {
        using (actorsLock.EnterReadScope())
        {
            if (nextMessageOrderingContainer.DirtyActors.TryGetValue(id, out actor))
            {
                // A null value means the actor has been removed
                return actor is not null;
            }

            if (actors.TryGetValue(id, out actor))
            {
                return true;
            }
        }

        actor = null;
        return false;
    }

    public void AddActor<T>(in T actor) where T : struct, ITypedActor<T>
    {
        using (actorsLock.EnterWriteScope())
        {
            actors.Add(actor.Id, actor);
            actorWatchRegistry.RecordChange(actor.Id, actor);
        }

        actorWatchRegistry.ReleaseChanges();
    }

    public T RemoveActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        if (TryRemoveActor(id, out var actor))
        {
            return actor;
        }

        throw new KeyNotFoundException();
    }

    public bool TryRemoveActor<T>(ActorId<T> id, out T actor) where T : struct, ITypedActor<T>
    {
        bool removed = false;

        using (actorsLock.EnterWriteScope())
        {
            if (nextMessageOrderingContainer.TryRemoveActor(id, out actor))
            {
                actors.Remove(id);
                removed = true;
            }
            else if (TryGetActor(id, out actor))
            {
                actors.Remove(actor.Id);
                removed = true;
            }

            if (removed)
            {
                actorWatchRegistry.RecordChange(id, null);
            }
        }

        if (removed)
        {
            actorWatchRegistry.ReleaseChanges();
        }

        return removed;
    }

    IActor? IActorDownloadHandler.GetActorForDownload(TypelessActorId actorId)
    {
        using (actorsLock.EnterReadScope())
        {
            if (actors.TryGetValue(actorId, out var actor))
            {
                return actor;
            }
        }

        return null;
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                backgroundTaskCanceller?.Cancel();
                actorsLock.Dispose();
                actorWatchRegistry.Dispose();
            }

            Transport.Dispose();

            disposedValue = true;
        }
    }

    ~RpcServerActorContainer()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}