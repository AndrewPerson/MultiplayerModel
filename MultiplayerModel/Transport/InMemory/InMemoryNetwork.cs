namespace MultiplayerModel.Transport.InMemory;

public class InMemoryNetwork
{
    public InMemoryServerTransport Server { get; }
    public List<InMemoryClientTransport> Clients { get; } = [];

    private uint nextClientId = 1;
    
    public InMemoryNetwork()
    {
        Server = new(this);
    }

    public InMemoryClientTransport AddClient()
    {
        var client = new InMemoryClientTransport(this, nextClientId++);
        Clients.Add(client);

        return client;
    }
}