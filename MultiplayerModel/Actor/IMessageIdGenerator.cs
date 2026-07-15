namespace MultiplayerModel.Actor;

public interface IMessageIdGenerator
{
    MessageId Generate(uint containerId);

    /**
     * The simple generator should be linked to this one, i.e.
     * generating an id using it should advance the internal
     * generator as if an id was generated using this generator.
     */
    public ISimpleMessageIdGenerator MakeSimple(uint defaultContainerId);
    
    public IMessageIdGenerator Clone();
}

public interface ISimpleMessageIdGenerator : IMessageIdGenerator
{
    public uint DefaultContainerId { get; }
    
    /**
     * Generate an id using the <see cref="DefaultContainerId"/>
     */
    public MessageId Generate() => Generate(DefaultContainerId);
    
    IMessageIdGenerator IMessageIdGenerator.Clone() => Clone(); 
    public new ISimpleMessageIdGenerator Clone();
}