using SourceGenUtils;
using SourceGenUtils.Collections;

namespace MultiplayerModel.Generators.Actor.Writers;

public static class Types
{
    public static readonly StringyType IMessageType =
        new("MultiplayerModel.Actor", "IMessage", ObjectType.Class, ImmutableEquatableArray<StringyType>.Empty);

    public static readonly StringyType MessageIdType =
        new("MultiplayerModel.Actor", "MessageId", ObjectType.Struct, ImmutableEquatableArray<StringyType>.Empty);

    public static readonly StringyType IActorType =
        new("MultiplayerModel.Actor", "IActor", ObjectType.Class, ImmutableEquatableArray<StringyType>.Empty);
    
    public static readonly StringyType IActorContainerType = new(
        "MultiplayerModel",
        "IActorContainer",
        ObjectType.Class,
        ImmutableEquatableArray<StringyType>.Empty
    );

    public static readonly StringyType ModuleInitializerAttributeType = new(
        "System.Runtime.CompilerServices",
        "ModuleInitializer",
        ObjectType.Class,
        ImmutableEquatableArray<StringyType>.Empty
    );

    public static readonly StringyType ActorSerialisationRegistryType = new(
        "MultiplayerModel.Serialisation",
        "ActorSerialisationRegistry",
        ObjectType.Class,
        ImmutableEquatableArray<StringyType>.Empty
    );
    
    public static readonly StringyType MessageSerialisationRegistryType = new(
        "MultiplayerModel.Serialisation",
        "MessageSerialisationRegistry",
        ObjectType.Class,
        ImmutableEquatableArray<StringyType>.Empty
    );
    
    /**
     * Whether <paramref name="type"/> is the same type as one of the known types above.
     *
     * <remarks>
     * <see cref="StringyType"/> equality includes the <see cref="ObjectType"/>, which Roslyn cannot recover
     * from metadata (a record struct compiled into another assembly looks like a plain struct), so known
     * types have to be compared on their rendered name rather than with <c>==</c>.
     * </remarks>
     */
    public static bool IsKnownType(StringyType type, StringyType knownType) =>
        type.NamespaceQualifiedType == knownType.NamespaceQualifiedType;

    public static string MethodMessageName(MessageHandlerMethod method) => $"{method.MethodName}Message";

    public static StringyType MethodMessage(MessageHandlerMethod method)
    {
        return new
        (
            method.ContainingType.Namespace,
            $"{method.ContainingType.Type}.{MethodMessageName(method)}",
            ObjectType.RecordStruct,
            ImmutableEquatableArray<StringyType>.Empty
        );
    }

    /**
     * <see cref="IActorType"/> closed over the method's message type — the interface that actually declares
     * <c>ProcessMessage</c>, and therefore the one an explicit interface implementation must name.
     */
    public static StringyType MethodIActorMessage(MessageHandlerMethod method) => new
    (
        "MultiplayerModel.Actor",
        "IActor",
        ObjectType.Class,
        new[] { MethodMessage(method) }.ToImmutableEquatableArray()
    );

    public static StringyType MethodIActor(MessageHandlerMethod method) => new
    (
        "MultiplayerModel.Actor",
        "ITypedActor",
        ObjectType.Class,
        new[] { method.ContainingType, MethodMessage(method) }.ToImmutableEquatableArray()
    );
}