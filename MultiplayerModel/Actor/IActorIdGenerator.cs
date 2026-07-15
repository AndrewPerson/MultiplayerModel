namespace MultiplayerModel.Actor;

public interface IActorIdGenerator
{
    ActorId<T> Generate<T>(uint containerId) where T : IActor;

    /**
     * The simple generator should be linked to this one, i.e.
     * generating an id using it should advance the internal
     * generator as if an id was generated using this generator.
     */
    ISimpleActorIdGenerator MakeSimple(uint defaultContainerId);
    
    IActorIdGenerator Clone();
}

public interface ISimpleActorIdGenerator : IActorIdGenerator
{
    public uint DefaultContainerId { get; }

    /**
     * Generate an id using the <see cref="DefaultContainerId"/>
     */
    ActorId<T> Generate<T>() where T : IActor => Generate<T>(DefaultContainerId);

    IActorIdGenerator IActorIdGenerator.Clone() => Clone(); 
    public new ISimpleActorIdGenerator Clone();
}