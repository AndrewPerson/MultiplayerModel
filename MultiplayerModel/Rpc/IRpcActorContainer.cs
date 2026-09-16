using MultiplayerModel.Actor;

namespace MultiplayerModel.Rpc;

public interface IRpcActorContainer : IActorContainer
{
    IActorIdGenerator IActorContainer.ActorIdGenerator => ActorIdGenerator;
    public new RpcActorIdGenerator ActorIdGenerator { get; }

    IMessageIdGenerator IActorContainer.MessageIdGenerator => MessageIdGenerator;
    public new RpcMessageIdGenerator MessageIdGenerator { get; }

    /**
     * Starts any long-running processes the container may need, and returns a task that doesn't complete/error until
     * they stop running (either because the <paramref name="cancellationToken"/> was cancelled or an exception occured).
     *
     * Also runs the underlying transport.
     */
    public Task Run(CancellationToken cancellationToken = default);

    /**
     * <summary>Watches the actor with the corresponding <paramref name="id"/> for changes.</summary>
     *
     * <remarks>
     * Emits the actor's current value upon subscription (if it exists), then emits the new value each time the actor
     * changes, and <c>null</c> when the actor is removed.
     * <br/>
     * The subscription stays alive after removal, so if the actor is re-added its value will be emitted again.
     * <br/>
     * Changes are batched: at most one value is emitted per actor per change batch (i.e. one applied message
     * ordering on a client, or one local change on a server), although the same value may be emitted more than once.
     * <br/>
     * Values are emitted on the thread that applied the change, so observers may be called from arbitrary threads and
     * are responsible for marshalling if needed.
     * </remarks>
     */
    public IObservable<T?> Watch<T>(ActorId<T> id) where T : struct, ITypedActor<T>;
}