using System.Collections.Immutable;
using MultiplayerModel.Actor;
using MultiplayerModel.Generators.Actor;

namespace ConsoleChat;

public readonly record struct Message(string Username, string Text);

[ActorSerialisationMixin("chat")]
[MessageSerialisationMixin("chat")]
public partial class Chat(ActorId<Chat> id, Chat.State_ state) : ITypedActor<Chat>
{
    public ActorId<Chat> Id { get; } = id;
    public State_ State { get; private set; } = state;
    
    public readonly record struct State_
    (
        ImmutableList<Message> Messages,
        ImmutableDictionary<string, uint> ClientUsernames
    );

    [MessageHandler]
    public void Join([MessageId] MessageId messageId, string username)
    {
        if (State.ClientUsernames.ContainsValue(messageId.ContainerId))
        {
            throw new InvalidOperationException();
        }

        State = State with { ClientUsernames = State.ClientUsernames.Add(username, messageId.ContainerId) };
    }

    [MessageHandler]
    public void SendMessage([MessageId] MessageId messageId, Message message)
    {
        if (messageId.ContainerId != State.ClientUsernames[message.Username])
        {
            throw new InvalidOperationException();
        }

        State = State with { Messages = State.Messages.Add(message) };
    }

    public Chat Clone()
    {
        return new Chat(Id, State);
    }

    public override string ToString()
    {
        return $"Chat {{ Id = {Id}, Messages = [{string.Join(", ", State.Messages.Select(m => m.ToString()))}], ClientUsernames = {State.ClientUsernames} }}";
    }

    public int StableHash()
    {
        return 0; // TODO
    }
}