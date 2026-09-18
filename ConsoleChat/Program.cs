using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ConsoleChat;
using Microsoft.Extensions.Logging;
using MultiplayerModel.Actor;
using MultiplayerModel.Extension;
using MultiplayerModel.Rpc;
using MultiplayerModel.Serialisation;
using MultiplayerModel.Serialisation.Json;
using MultiplayerModel.Transport.Default;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Log");

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
        messageJsonSerialisationOpts,
        logger
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
        messageJsonSerialisationOpts,
        logger
    );

    actorContainer = new RpcClientActorContainer(clientTransport);
}

_ = actorContainer.Run().ContinueWith(t =>
{
    if (t.IsFaulted)
    {
        logger.LogCritical(t.Exception, "Stopped running with exception");
    }
    else
    {
        logger.LogCritical("Stopped running cleanly");
    }
});

Chat chat;
if (host)
{
    chat = actorContainer.GetActor(actorContainer.ListActors<Chat>().First());
}
else
{
    Console.WriteLine("Starting watch");
    chat = (await actorContainer.Watch<Chat>().FirstAsync()).Item2!.Value;
}

// await Task.Delay(1000);
// var chat = actorContainer.GetActor(actorContainer.ListActors<Chat>().First());

Console.Write("Username: ");
var username = Console.ReadLine()!;
chat = chat.Join(actorContainer, username);

var consoleLock = new SemaphoreSlim(1);
actorContainer.Watch(chat.Id).Subscribe(c =>
{
    if (c is null)
    {
        return;
    }

    using (consoleLock.EnterWaitScope())
    {
        Console.Clear();
        foreach (var chatMessage in c.Value.Messages.TakeLast(10))
        {
            Console.WriteLine($"{chatMessage.Username}: {chatMessage.Text}");
        }

        Console.Write("> ");
    }
});

while (true)
{
    var message = Console.ReadLine();
    if (!string.IsNullOrEmpty(message))
    {
        chat.SendMessage(actorContainer, new Message(username, message));
    }
}