using MultiplayerModel.Actor;

namespace MultiplayerModel.Rpc;

public class RpcActorIdGenerator : ISimpleActorIdGenerator
{
    public uint DefaultContainerId { get; set; }

    private readonly RpcGuidGenerator generator = new();

    public RpcActorIdGenerator(uint defaultContainerId)
    {
        DefaultContainerId = defaultContainerId;
    }

    private RpcActorIdGenerator(uint defaultContainerId, RpcGuidGenerator generator) : this(defaultContainerId)
    {
        this.generator = generator;
    }
    
    public ActorId<T> Generate<T>(uint containerId) where T : IActor
    {
        return new(containerId, generator.Next(containerId));
    }

    ISimpleActorIdGenerator IActorIdGenerator.MakeSimple(uint defaultContainerId) => MakeSimple(defaultContainerId);
    public RpcActorIdGenerator MakeSimple(uint defaultContainerId)
    {
        return new(defaultContainerId, generator);
    }
    
    ISimpleActorIdGenerator ISimpleActorIdGenerator.Clone() => Clone();
    public RpcActorIdGenerator Clone()
    {
        return new(DefaultContainerId, generator.Clone());
    }
}