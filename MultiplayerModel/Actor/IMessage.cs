namespace MultiplayerModel.Actor;

public interface IMessage
{
    public MessageId Id { get; }

    /**
     * Attempt to send the message to the actor by picking the right <see cref="IActor{TMessage}"/>
     * implementation. If the actor does not implement the correct <see cref="IActor{TMessage}"/>
     * interface, do nothing.
     */
    public void TryDispatch(IActorContainer container, IActor actor);
}
