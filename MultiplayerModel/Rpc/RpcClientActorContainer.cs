using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;
using MultiplayerModel.Extension;
using MultiplayerModel.Rpc.Observables;
using MultiplayerModel.Transport.Client;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Rpc;

/**
 * <remarks>
 * All methods on this class are thread-safe, although they may not be very efficient in their thread-safety.
 * </remarks>
 */
public class RpcClientActorContainer : IRpcActorContainer, IDisposable
{
    public RpcActorIdGenerator ActorIdGenerator { get; }
    public RpcMessageIdGenerator MessageIdGenerator { get; }

    public uint Id => Transport.ClientId;

    public IClientTransport Transport { get; }

    // Technically covers both actors and unackedChangesContainer
    private readonly ReaderWriterLockSlim actorsLock = new(LockRecursionPolicy.SupportsRecursion);

    private readonly Dictionary<TypelessActorId, TaskCompletionSource<IActor>> actorDownloads = [];
    private readonly ChangeCalculationActorContainer unackedChangesContainer;
    private readonly Dictionary<TypelessActorId, IActor> actors = [];
    private readonly ActorWatchRegistry actorWatchRegistry = new();

    private CancellationTokenSource? backgroundTaskCanceller;
    private bool running;

    private bool disposedValue;

    public RpcClientActorContainer(IClientTransport transport)
    {
        Transport = transport;

        ActorIdGenerator = new(Id);
        MessageIdGenerator = new(Id);

        unackedChangesContainer = new(this);
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref running, true))
        {
            throw new InvalidOperationException($"{nameof(RpcClientActorContainer)} is already running.");
        }

        try
        {
            backgroundTaskCanceller = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var backgroundCancellationToken = backgroundTaskCanceller.Token;

            var transportTask = Transport.Run(backgroundCancellationToken);

            var actorIds = await Transport.ListActors(backgroundCancellationToken);
            foreach (var id in actorIds)
            {
                DownloadActor(id);
            }

            async Task MessageOrdering()
            {
                while (!backgroundCancellationToken.IsCancellationRequested)
                {
                    var messageOrdering = await Transport.ReceiveMessageOrdering(backgroundCancellationToken);
                    await Task.Run(() => ApplyMessageOrdering(messageOrdering), backgroundCancellationToken);
                }

                backgroundCancellationToken.ThrowIfCancellationRequested();
            }

            await await Task.WhenAny(transportTask, MessageOrdering());
        }
        finally
        {
            backgroundTaskCanceller?.Cancel();
            Interlocked.Exchange(ref running, false);
        }
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * Will also send the message to the <see cref="RpcServerActorContainer"/> over the configured
     * <see cref="Transport"/>
     * </remarks>
     */
    public T? SendMessage<T, TMessage>(ActorId<T> actorId, TMessage message)
        where T : struct, ITypedActor<T, TMessage> where TMessage : IMessage
    {
        T? newActor;

        using (actorsLock.EnterWriteScope())
        {
            newActor = unackedChangesContainer.SendMessage(actorId, ref message);
            Transport.SendMessage(actorId, message).ConfigureAwait(true);

            if (newActor is not null)
            {
                actorWatchRegistry.RecordChange(actorId, typeof(T), newActor);
            }
        }

        actorWatchRegistry.ReleaseChanges();

        return newActor;
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * Will also send the message to the <see cref="RpcServerActorContainer"/> over the configured
     * <see cref="Transport"/>
     * </remarks>
     */
    public IActor? SendMessage(TypelessActorId actorId, IMessage message)
    {
        IActor? newActor;

        using (actorsLock.EnterWriteScope())
        {
            newActor = unackedChangesContainer.SendMessage(actorId, ref message);
            Transport.SendMessage(actorId, message).ConfigureAwait(true);

            if (newActor is not null)
            {
                actorWatchRegistry.RecordChange(actorId, newActor.GetType(), newActor);
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
            result.UnionWith(actorDownloads.Keys);
            result.UnionWith(unackedChangesContainer.DirtyActors.Keys);

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

            // TODO: Add the correct IDs from actorDownloads?

            result.UnionWith(
                unackedChangesContainer.DirtyActors
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

    public IObservable<(ActorId<T>, T?)> Watch<T>() where T : struct, ITypedActor<T>
    {
        return new DelegateObservable<(ActorId<T>, T?)>(observer =>
        {
            // Holding the read lock while subscribing orders the initial snapshot before any subsequently
            // recorded changes, without comparing values.
            using (actorsLock.EnterReadScope())
            {
                return actorWatchRegistry.Subscribe<T>(new IdAndTypedActorObserver<T>(observer));
            }
        });
    }

    /**
     * Gets the effective value of an actor (unacked changes preferred over committed ones) for use as a watch
     * snapshot.
     *
     * <remarks>Must be called with the read lock held. In-flight downloads are intentionally skipped; their
     * completion records a change that will be delivered to watchers. In contrast to <see cref="TryGetActor"/>,
     * this does not copy the actor into the unacked container nor block.</remarks>
     */
    private IActor? GetActorForWatch(TypelessActorId id)
    {
        if (unackedChangesContainer.DirtyActors.TryGetValue(id, out var unackedActor))
        {
            // A null value means the actor has been removed.
            return unackedActor;
        }

        return actors.GetValueOrDefault(id);
    }

    public bool ContainsActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        using (actorsLock.EnterReadScope())
        {
            if (unackedChangesContainer.DirtyActors.TryGetValue(id, out var unackedActor))
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
            if (unackedChangesContainer.DirtyActors.TryGetValue(id, out var actor))
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
        using (var scope = actorsLock.EnterReadScope())
        {
            if (actorDownloads.TryGetValue(id, out var actorDownload))
            {
                scope.Exit();

                if (actorDownload.Task.Result is T typedDownloadedActor)
                {
                    actor = typedDownloadedActor;
                    return true;
                }

                return TryGetActor(id, out actor);
            }

            if (unackedChangesContainer.DirtyActors.TryGetValue(id, out var untypedUnackedActor))
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
        using (var scope = actorsLock.EnterReadScope())
        {
            if (actorDownloads.TryGetValue(id, out var actorDownload))
            {
                scope.Exit();

                actor = actorDownload.Task.Result;
                return true;
            }

            if (unackedChangesContainer.DirtyActors.TryGetValue(id, out actor))
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
            actorWatchRegistry.RecordChange(actor.Id, typeof(T), actor);
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
            if (unackedChangesContainer.TryRemoveActor(id, out actor))
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
                actorWatchRegistry.RecordChange(id,  typeof(T), null);
            }
        }

        if (removed)
        {
            actorWatchRegistry.ReleaseChanges();
        }

        return removed;
    }

    private void ApplyMessageOrdering(MessageOrdering ordering)
    {
        // Dictionary<MessageId, (int Index, (TypelessActorId, IMessage) Item)> unackedMessagesDict;
        using (actorsLock.EnterWriteScope())
        {
            var ackedMessages = ordering.Ordering;
            
            var unackedMessagesDict = unackedChangesContainer.AppliedMessages
                .Index().ToDictionary(x => x.Item.Item2.Id);

            unackedChangesContainer.ReCreate();
            
            var ackedChangesContainer = new ChangeCalculationActorContainer(this);
            for (int i = 0; i < ackedMessages.Count; i++)
            {
                var (id, message) = ackedMessages[i];
                ackedChangesContainer.SendMessage(id, ref message);

                unackedMessagesDict.Remove(message.Id);
            }
            
            ackedChangesContainer.ApplyTo(actors, actorWatchRegistry);

            foreach (var (id, actor) in ackedChangesContainer.DirtyActors)
            {
                if (!ordering.ActorHashes.TryGetValue(id, out var correctHash))
                {
                    DownloadActor(id);
                    continue;
                }

                if (actor is null && correctHash is not null)
                {
                    DownloadActor(id);
                }
                else if (actor is not null && actor.StableHash() != correctHash)
                {
                    DownloadActor(id);
                }
            }
            
            foreach (var (id, correctHash) in ordering.ActorHashes)
            {
                if (correctHash is null)
                {
                    // A null hash means the actor was removed on the server; remove it locally (this also covers
                    // actors that the acked replay re-created, as the server's hash reflects its final state).
                    if (actors.Remove(id, out var actor))
                    {
                        actorWatchRegistry.RecordChange(id, actor.GetType(), null);
                    }

                    continue;
                }

                if (!ackedChangesContainer.DirtyActors.ContainsKey(id))
                {
                    // There must be some non-deterministic action happening, re-download the actor to remain in sync
                    DownloadActor(id);
                }
            }
            
            var unackedMessages = unackedMessagesDict.Values.OrderBy(x => x.Index).Select(x => x.Item).ToList();
            for (int i = 0; i < unackedMessages.Count; i++)
            {
                var (id, message) = unackedMessages[i];
                var newActor = unackedChangesContainer.SendMessage(id, ref message);

                if (newActor is not null)
                {
                    actorWatchRegistry.RecordChange(id, newActor.GetType(), newActor);
                }
            }
        }

        actorWatchRegistry.ReleaseChanges();
    }

    /**
     * <remarks>
     * Enters a write lock, so make sure to use an upgradeable read lock outside if you need to
     * read anything.
     * </remarks>
     */
    private void DownloadActor(TypelessActorId id)
    {
        using (actorsLock.EnterWriteScope())
        {
            if (!actorDownloads.ContainsKey(id))
            {
                var tcs = new TaskCompletionSource<IActor>();
                actorDownloads.Add(id, tcs);

                Transport.DownloadActor(id).ContinueWith(t =>
                {
                    tcs.SetFromTask(t);
                    using (actorsLock.EnterWriteScope())
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            actors[id] = t.Result;
                            actorWatchRegistry.RecordChange(id, t.Result.GetType(), t.Result);
                        }

                        actorDownloads.Remove(id);
                    }

                    actorWatchRegistry.ReleaseChanges();
                });
            }
        }
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

    ~RpcClientActorContainer()
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