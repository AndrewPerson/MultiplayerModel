using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using MultiplayerModel.Actor;
using MultiplayerModel.Rpc;
using MultiplayerModel.Serialisation;
using MultiplayerModel.Serialisation.Json;
using MultiplayerModel.Transport.Default;
// using MultiplayerModel.Transport.InMemory;
using Playground;

// var guid = Guid.CreateVersion8(100000000000, 10);
// Console.WriteLine(guid);
//
// var decoded = Guid.Parse(guid.ToString()).ParseAsVersion8();
// Console.WriteLine(decoded);

// using var networkEventListener = new NetworkEventListener();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
var serverLogger = loggerFactory.CreateLogger("Server");
var clientLogger = loggerFactory.CreateLogger("Client");

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

var canceller = new CancellationTokenSource();

// var network = new InMemoryNetwork();

var serverTransport = new DefaultServerTransport(
    ["http://localhost:8000/"],
    actorJsonSerialisationOpts,
    messageJsonSerialisationOpts,
    serverLogger
); // network.Server;

IRpcActorContainer serverContainer = new RpcServerActorContainer(serverTransport);

var actor = new Person(serverContainer.ActorId<Person>(), "Andrew");
serverContainer.AddActor(actor);

var serverRunTask = serverContainer.Run(canceller.Token).ContinueWith(t =>
{
    if (t.IsFaulted) Console.WriteLine(t.Exception);
});

actor = actor
    .Greet(serverContainer)
    .Greet2(serverContainer)
    .ChangeName(serverContainer, new("Andrew 2"))
    .Greet(serverContainer);

Console.WriteLine(serverContainer.GetActor(actor.Id) == actor);

Console.ReadKey();
Console.WriteLine("-------");

var clientTransport = await DefaultClientTransport.Create(
    new("http://localhost:8000/"),
    actorJsonSerialisationOpts,
    messageJsonSerialisationOpts,
    clientLogger
); // network.AddClient();

IRpcActorContainer clientContainer = new RpcClientActorContainer(clientTransport);

var clientRunTask = clientContainer.Run(canceller.Token).ContinueWith(t =>
{
    if (t.IsFaulted) Console.WriteLine(t.Exception);
});

Console.ReadKey();
Console.WriteLine("-------");

Console.WriteLine(string.Join(", ", serverContainer.ListActors().Select(x => x.ToString())));
Console.WriteLine(string.Join(", ", clientContainer.ListActors().Select(x => x.ToString())));

Console.ReadKey();
Console.WriteLine("-------");

actor = actor.ChangeName(clientContainer, new("Andrew 3"));

Console.ReadKey();
Console.WriteLine("-------");

Console.WriteLine(serverContainer.GetActor(actor.Id));
Console.WriteLine(clientContainer.GetActor(actor.Id));

Console.ReadKey();
Console.WriteLine("-------");

actor = actor.ChangeName(serverContainer, new("Andrew 4"));

Console.ReadKey();
Console.WriteLine("-------");

Console.WriteLine(serverContainer.GetActor(actor.Id));
Console.WriteLine(clientContainer.GetActor(actor.Id));

await canceller.CancelAsync();
await serverRunTask;
await clientRunTask;
