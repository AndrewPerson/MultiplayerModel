using SourceGenUtils;
using SourceGenUtils.Collections;

namespace MultiplayerModel.Generators.Actor;

public readonly record struct MessageHandlerMethod(
    StringyType ContainingType,
    StringyType? ExtensionType,
    bool IsRecordType,
    string MethodName,
    string? MessageSerialisationBaseKey,
    ImmutableEquatableArray<MessageHandlerParameter> Parameters
);

public readonly record struct MessageHandlerParameter
(
    StringyType Type,
    string Name,
    MessageHandlerParameter.MessageComponent Component
)
{
    public enum MessageComponent
    {
        MessageId,
        ActorContainer,
        None
    }
}