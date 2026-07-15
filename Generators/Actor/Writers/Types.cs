using SourceGenUtils;
using SourceGenUtils.Collections;

namespace MultiplayerModel.Generators.Actor.Writers;

public static class Types
{
    public static readonly StringyType IMessageType =
        new("MultiplayerModel.Actor", "IMessage", ImmutableEquatableArray<StringyType>.Empty);

    public static readonly StringyType MessageIdType =
        new("MultiplayerModel.Actor", "MessageId", ImmutableEquatableArray<StringyType>.Empty);

    public static readonly StringyType IActorType =
        new("MultiplayerModel.Actor", "IActor", ImmutableEquatableArray<StringyType>.Empty);
    
    public static readonly StringyType IActorContainerType = new(
        "MultiplayerModel",
        "IActorContainer",
        ImmutableEquatableArray<StringyType>.Empty
    );

    public static readonly StringyType ModuleInitializerAttributeType = new(
        "System.Runtime.CompilerServices",
        "ModuleInitializer",
        ImmutableEquatableArray<StringyType>.Empty
    );

    public static readonly StringyType ActorSerialisationRegistryType = new(
        "MultiplayerModel.Serialisation",
        "ActorSerialisationRegistry",
        ImmutableEquatableArray<StringyType>.Empty
    );
    
    public static readonly StringyType MessageSerialisationRegistryType = new(
        "MultiplayerModel.Serialisation",
        "MessageSerialisationRegistry",
        ImmutableEquatableArray<StringyType>.Empty
    );
    
    public static string MethodMessageName(MessageHandlerMethod method) => $"{method.MethodName}Message";

    public static StringyType MethodMessage(MessageHandlerMethod method)
    {
        return new
        (
            method.ContainingType.Namespace,
            $"{method.ContainingType.Type}.{MethodMessageName(method)}",
            ImmutableEquatableArray<StringyType>.Empty
        );
    }

    public static StringyType MethodIActor(MessageHandlerMethod method) => new
    (
        "MultiplayerModel.Actor",
        "ITypedActor",
        new[] { method.ContainingType, MethodMessage(method) }.ToImmutableEquatableArray()
    );
}