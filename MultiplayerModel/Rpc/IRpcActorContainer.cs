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
}