using System.Threading.Tasks.Dataflow;
using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Client;
using MultiplayerModel.Transport.Server;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.InMemory;

public class InMemoryServerTransport(InMemoryNetwork network) : IServerTransport
{
    public IListActorsHandler? ListActorsHandler { get; set; }
    public IActorDownloadHandler? DownloadHandler { get; set; }

    private readonly BufferBlock<(TypelessActorId, IMessage)> messages = new();
    
    public Task Run(CancellationToken cancellationToken = default)
    {
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public void QueueMessage(TypelessActorId actorId, IMessage message)
    {
        messages.Post((actorId, message));
    }

    public async Task<(TypelessActorId, IMessage)> ReceiveMessage(CancellationToken cancellationToken = default)
    {
        var message = await messages.ReceiveAsync(cancellationToken);
        return message;
    }

    public Task SendMessageOrdering(
        IReadOnlyList<AddressedMessage> messages,
        IReadOnlyDictionary<TypelessActorId, int?> actorHashes,
        long version,
        CancellationToken cancellationToken = default
    )
    {
        var ordering = new MessageOrdering(messages, actorHashes, version);
        foreach (var client in network.Clients)
        {
            client.QueueMessageOrdering(ordering);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}