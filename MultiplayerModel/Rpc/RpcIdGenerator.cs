using MultiplayerModel.Extension;

namespace MultiplayerModel.Rpc;

public class RpcIdGenerator
{
    private readonly Dictionary<uint, ulong> clientCounters = [];

    public RpcIdGenerator()
    {
    }

    private RpcIdGenerator(Dictionary<uint, ulong> clientCounters)
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
    
    public ulong NextULong(uint clientId)
    {
        if (!clientCounters.TryGetValue(clientId, out var counter))
        {
            counter = 0;
        }

        return clientCounters[clientId] = ++counter;
    }

    public RpcIdGenerator Clone()
    {
        return new(new(clientCounters));
    }
}