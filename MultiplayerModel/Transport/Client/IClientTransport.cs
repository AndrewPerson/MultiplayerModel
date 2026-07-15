using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Default;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.Client;

/**
 * <remarks>Implementations should be thread-safe</remarks>
 * 
 * <seealso cref="Server.IServerTransport"/>
 */
public interface IClientTransport : IDisposable
{
    public uint ClientId { get; }

    /**
     * Starts any long-running processes the transport may need, and returns a task that doesn't complete until they
     * stop running (either because the <paramref name="cancellationToken"/> was cancelled or an exception occured).
     */
    public Task Run(CancellationToken cancellationToken = default);

    /**
     * List all ids of actors on the server.
     */
    public Task<IReadOnlySet<TypelessActorId>> ListActors(CancellationToken cancellationToken = default);

    /**
     * Asynchronously fetch the latest version of an actor from the server.
     */
    public Task<IActor> DownloadActor(IActorId actorId, CancellationToken cancellationToken = default);

    /**
     * Send a message to the server. Each message is received by the server EXACTLY once.
     *
     * <exception cref="FailedToSendMessageException">The transport couldn't send the message</exception>
     */
    public Task SendMessage(IActorId actorId, IMessage message, CancellationToken cancellationToken = default);

    /**
     * Receive the latest message ordering from the server. Each ordering is received EXACTLY once.
     *
     * <remarks>
     * Will (asynchronously) wait until an ordering is available if none are available at
     * the time this is called.
     * </remarks>
     */
    public Task<MessageOrdering> ReceiveMessageOrdering(CancellationToken cancellationToken = default);
}