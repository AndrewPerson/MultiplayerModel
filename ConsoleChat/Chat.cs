using System.Collections.Immutable;
using MultiplayerModel.Actor;
using MultiplayerModel.Generators.Actor;

namespace ConsoleChat;

public readonly record struct Message(string Username, string Text);

[ActorSerialisationMixin("chat")]
[MessageSerialisationMixin("chat")]
public readonly partial record struct Chat(
    ActorId<Chat> Id,
    ImmutableList<Message> Messages,
    ImmutableDictionary<string, uint> ClientUsernames
)
{
    [MessageHandler]
    public Chat Join([MessageId] MessageId messageId, string username)
    {
        if (ClientUsernames.ContainsValue(messageId.ContainerId))
        {
            throw new InvalidOperationException();
        }

        return this with { ClientUsernames = ClientUsernames.Add(username, messageId.ContainerId) };
    }
    
    [MessageHandler]
    public Chat SendMessage([MessageId] MessageId messageId, Message message)
    {
        if (messageId.ContainerId != ClientUsernames[message.Username])
        {
            throw new InvalidOperationException();
        }

        return this with { Messages = Messages.Add(message) };
    }

    public override string ToString()
    {
        return $"Chat {{ Id = {Id}, Messages = [{string.Join(", ", Messages.Select(m => m.ToString()))}], ClientUsernames = {ClientUsernames} }}";
    }

    public int StableHash()
    {
        return 0; // TODO
    }
}