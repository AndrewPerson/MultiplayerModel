namespace MultiplayerModel.Actor;

/**
 * <remarks>
 * Regular actors should inherit from <see cref="ITypedActor{TSelf}"/> instead of this. This interface exists mostly
 * for being able to store heterogeneous actors in collections.
 * </remarks>
 */
public interface IActor
{
    public TypelessActorId Id { get; }
    
    public int StableHash();
}

public interface IActor<TMessage> : IActor where TMessage : IMessage
{
    /**
     * Perform operations based off of the message.
     *
     * <remarks>
     * Does not need to be thread-safe. Message processing will be serialised by <see cref="IActorContainer"/>.
     * </remarks>
     */
    public IActor ProcessMessage(IActorContainer actorContainer, ref TMessage message);
}

public interface ITypedActor<TSelf> : IActor where TSelf : struct, ITypedActor<TSelf>
{
    public new ActorId<TSelf> Id { get; }
    TypelessActorId IActor.Id => Id;
}

public interface ITypedActor<TSelf, TMessage> : ITypedActor<TSelf>, IActor<TMessage>
    where TSelf : struct, ITypedActor<TSelf> where TMessage : IMessage
{
    IActor IActor<TMessage>.ProcessMessage(IActorContainer actorContainer, ref TMessage message)
        => ProcessMessage(actorContainer, ref message);
    
    /**
     * Perform operations based off of the message.
     *
     * <remarks>
     * Does not need to be thread-safe. Message processing will be serialised by <see cref="IActorContainer"/>.
     * </remarks>
     */
    public new TSelf ProcessMessage(IActorContainer actorContainer, ref TMessage message);
}
