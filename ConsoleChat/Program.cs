using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ConsoleChat;
using MultiplayerModel.Actor;
using MultiplayerModel.Rpc;
using MultiplayerModel.Serialisation;
using MultiplayerModel.Serialisation.Json;
using MultiplayerModel.Transport.Default;

Console.Write("Host session? (y/n) ");
var host = Console.ReadKey().Key == ConsoleKey.Y;
Console.WriteLine();

var actorJsonSerialisationOpts = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine
    (
        new PolymorphicTypeResolver(
            typeof(IActor),
            ActorSerialisationRegistry.GetActorKeys()
        ),
        new PolymorphicTypeResolver(
            typeof(IActorId),
            [new(typeof(TypelessActorId), "typeless"), ..ActorSerialisationRegistry.GetActorIdKeys()]
        ),
        new DefaultJsonTypeInfoResolver()
    )
};

var messageJsonSerialisationOpts = new JsonSerializerOptions(JsonSerializerOptions.Default)
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine
    (
        new PolymorphicTypeResolver(
            typeof(IMessage),
            MessageSerialisationRegistry.GetMessageKeys()
        ),
        new PolymorphicTypeResolver(
            typeof(IActorId),
            [new(typeof(TypelessActorId), "typeless"), ..ActorSerialisationRegistry.GetActorIdKeys()]
        ),
        new DefaultJsonTypeInfoResolver()
    )
};

IRpcActorContainer actorContainer;
if (host)
{
    var serverTransport = new DefaultServerTransport(
        ["http://localhost:8000/"],
        actorJsonSerialisationOpts,
        messageJsonSerialisationOpts
    );

    actorContainer = new RpcServerActorContainer(serverTransport);
    
    actorContainer.AddActor(new Chat(
        actorContainer.ActorId<Chat>(),
        ImmutableList<Message>.Empty, 
        ImmutableDictionary<string, uint>.Empty
    ));
}
else
{
    var clientTransport = await DefaultClientTransport.Create(
        new("http://localhost:8000/"),
        actorJsonSerialisationOpts,
        messageJsonSerialisationOpts
    );

    actorContainer = new RpcClientActorContainer(clientTransport);
}

_ = actorContainer.Run();

await Task.Delay(1000);

var chat = actorContainer.GetActor(actorContainer.ListActors<Chat>().First());

Console.Write("Username: ");
var username = Console.ReadLine()!;
chat = chat.Join(actorContainer, username);

while (true)
{
    Console.Write("> ");
    var message = Console.ReadLine();
    if (!string.IsNullOrEmpty(message))
    {
        chat = chat.SendMessage(actorContainer, new Message(username, message));
    }
    
    Console.Clear();
    foreach (var chatMessage in chat.Messages.TakeLast(10))
    {
        Console.WriteLine($"{chatMessage.Username}: {chatMessage.Text}");
    }
}