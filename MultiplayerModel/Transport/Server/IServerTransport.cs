using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.Server;

/**
 * <remarks>Implementations should be thread-safe</remarks>
 *
 * <seealso cref="Client.IClientTransport"/>
 */
public interface IServerTransport : IDisposable
{
    /**
     * <remarks>Should be set by the <see cref="Rpc.RpcServerActorContainer"/> when passed in to the constructor.</remarks>
     */
    public IListActorsHandler? ListActorsHandler { get; set; }
    
    /**
     * <remarks>Should be set by the <see cref="Rpc.RpcServerActorContainer"/> when passed in to the constructor.</remarks>
     */
    public IActorDownloadHandler? DownloadHandler { get; set; }

    /**
     * Starts any long-running processes the transport may need, and returns a task that doesn't complete until they
     * stop running (either because the <paramref name="cancellationToken"/> was cancelled or an exception occured).
     */
    public Task Run(CancellationToken cancellationToken = default);
    
    /**
     * <remarks>
     * Implementations should ensure that the <see cref="IMessage"/>'s <see cref="MessageId.ContainerId"/> parameter
     * matches the sender's ClientId
     * </remarks>
     */
    public Task<(IActorId, IMessage)> ReceiveMessage(CancellationToken cancellationToken = default);

    public Task SendMessageOrdering
    (
        IReadOnlyList<AddressedMessage> messages,
        IReadOnlyDictionary<TypelessActorId, int?> actorHashes,
        long version,
        CancellationToken cancellationToken = default
    );
}