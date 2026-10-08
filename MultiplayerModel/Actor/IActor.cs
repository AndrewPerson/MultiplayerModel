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

    public IActor Clone();

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
    public void ProcessMessage(IActorContainer actorContainer, ref TMessage message);
}

public interface ITypedActor<TSelf> : IActor where TSelf : ITypedActor<TSelf>
{
    public new ActorId<TSelf> Id { get; }
    TypelessActorId IActor.Id => Id;

    IActor IActor.Clone() => Clone();
    public new TSelf Clone();
}

public interface ITypedActor<TSelf, TMessage> : ITypedActor<TSelf>, IActor<TMessage>
    where TSelf : ITypedActor<TSelf> where TMessage : IMessage;