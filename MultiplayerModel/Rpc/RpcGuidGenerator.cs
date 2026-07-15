using MultiplayerModel.Extension;

namespace MultiplayerModel.Rpc;

public class RpcGuidGenerator
{
    private readonly Dictionary<uint, ulong> clientCounters = [];

    public RpcGuidGenerator()
    {
    }

    private RpcGuidGenerator(Dictionary<uint, ulong> clientCounters)
    {
        this.clientCounters = clientCounters;
    }

    public Guid Next(uint clientId)
    {
        if (!clientCounters.TryGetValue(clientId, out var counter))
        {
            counter = 0;
        }

        var guid = Guid.CreateVersion8(counter++, clientId);

        clientCounters[clientId] = counter;

        return guid;
    }

    public RpcGuidGenerator Clone()
    {
        return new(new(clientCounters));
    }
}