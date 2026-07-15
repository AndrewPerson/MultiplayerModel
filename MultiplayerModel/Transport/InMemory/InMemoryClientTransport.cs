using System.Threading.Tasks.Dataflow;
using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Client;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.InMemory;

public class InMemoryClientTransport(InMemoryNetwork network, uint clientId) : IClientTransport
{
    public uint ClientId { get; } = clientId;

    private readonly BufferBlock<MessageOrdering> orderings = new();

    public Task Run(CancellationToken cancellationToken = default)
    {
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public Task<IReadOnlySet<TypelessActorId>> ListActors(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(network.Server.ListActorsHandler!.ListActors());
    }

    public Task<IActor> DownloadActor(IActorId actorId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(network.Server.DownloadHandler!.GetActorForDownload(new(actorId))!);
    }

    public Task SendMessage(IActorId actorId, IMessage message, CancellationToken cancellationToken = default)
    {
        network.Server.QueueMessage(actorId, message);
        return Task.CompletedTask;
    }

    public void QueueMessageOrdering(MessageOrdering messageOrdering)
    {
        orderings.Post(messageOrdering);
    }

    public async Task<MessageOrdering> ReceiveMessageOrdering(CancellationToken cancellationToken = default)
    {
        return await orderings.ReceiveAsync(cancellationToken);
    }

    public void Dispose()
    {
    }
}