using MultiplayerModel.Actor;

namespace MultiplayerModel.Rpc;

public class RpcMessageIdGenerator : ISimpleMessageIdGenerator
{
    public uint DefaultContainerId { get; set; }

    private readonly RpcGuidGenerator generator = new();

    public RpcMessageIdGenerator(uint defaultContainerId)
    {
        DefaultContainerId = defaultContainerId;
    }
    
    private RpcMessageIdGenerator(uint defaultContainerId, RpcGuidGenerator generator) : this(defaultContainerId)
    {
        this.generator = generator;
    }

    public MessageId Generate(uint containerId)
    {
        return new(containerId, generator.Next(containerId));
    }

    ISimpleMessageIdGenerator IMessageIdGenerator.MakeSimple(uint defaultContainerId) => MakeSimple(defaultContainerId);
    public RpcMessageIdGenerator MakeSimple(uint defaultContainerId)
    {
        return new(defaultContainerId, generator);
    }
    
    ISimpleMessageIdGenerator ISimpleMessageIdGenerator.Clone() => Clone();
    public RpcMessageIdGenerator Clone()
    {
        return new(DefaultContainerId, generator.Clone());
    }
}
