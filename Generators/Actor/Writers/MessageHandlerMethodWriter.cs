using SourceGenUtils;

namespace MultiplayerModel.Generators.Actor.Writers;

public static class MessageHandlerMethodWriter
{
    private static List<MessageHandlerParameter> GetMessageParameters(MessageHandlerMethod method)
    {
        var messageParameters = method.Parameters.ToList();

        if (method.ExtensionType != null)
        {
            messageParameters.RemoveAt(0);
        }

        messageParameters.Insert(0, new(
            Types.MessageIdType,
            "Id",
            MessageHandlerParameter.MessageComponent.None
        ));

        return messageParameters;
    }

    public static void Write(IndentedTextWriter writer, MessageHandlerMethod method)
    {
        var messageTypeName = Types.MethodMessageName(method);
        var messageType = Types.MethodMessage(method);

        var methodIActorType = Types.MethodIActor(method);

        if (method.ContainingType.Namespace != null)
        {
            writer.WriteLine($"namespace {method.ContainingType.Namespace};");
            writer.WriteLine();
        }

        writer.WriteLine(method.IsRecordType
            ? $"public partial record struct {method.ContainingType.Type} : {methodIActorType}"
            : $"public partial struct {method.ContainingType.Type} : {methodIActorType}");

        using (writer.WriteBlock("{", "}"))
        {
            writer.WriteLine($"public record struct {messageTypeName}");
            using (writer.WriteBlock("(", $") : {Types.IMessageType}"))
            {
                var messageParameters =
                    GetMessageParameters(method)
                        .Where(p => p.Component == MessageHandlerParameter.MessageComponent.None)
                        .ToList();

                writer.WriteLine(string.Join(
                    $",{writer.NewLine}",
                    messageParameters.Select(p => $"{p.Type} {p.Name}")
                ));
            }

            using (writer.WriteBlock("{", "}"))
            {
                writer.WriteLine($"public {Types.IActorType} TryDispatch({Types.IActorContainerType} actorContainer, {Types.IActorType} actor)");
                using (writer.WriteBlock("{", "}"))
                {
                    writer.WriteLine($"if (actor is {methodIActorType} castActor)");
                    using (writer.WriteBlock("{", "}"))
                    {
                        writer.WriteLine("return castActor.ProcessMessage(actorContainer, ref this);");
                    }
                    writer.WriteLine("else");
                    using (writer.WriteBlock("{", "}"))
                    {
                        writer.WriteLine("return actor;");
                    }
                }

                if (method.MessageSerialisationBaseKey is not null)
                {
                    writer.WriteLine($"[{Types.ModuleInitializerAttributeType}]");
                    writer.WriteLine("public static void RegisterForSerialisation()");
                    using (writer.WriteBlock("{", "}"))
                    {
                        var serialisationKey = method.MessageSerialisationBaseKey + '.' + messageTypeName;
                        writer.WriteLine($"{Types.MessageSerialisationRegistryType}.Register<{messageTypeName}>(\"{serialisationKey}\");");
                    }
                }
            }

            writer.WriteLine();

            writer.WriteLine($"{method.ContainingType} {methodIActorType}.ProcessMessage({Types.IActorContainerType} actorContainer, ref {messageType} message)");
            using (writer.WriteBlock("{", "}"))
            {
                writer.WriteLine(method.ExtensionType switch
                {
                    null => $"return this.{method.MethodName}",
                    var extensionType => $"return {extensionType}.{method.MethodName}",
                });

                using (writer.WriteBlock("(", ");"))
                {
                    if (method.ExtensionType != null)
                    {
                        writer.Write("this");
                        writer.WriteLine(method.Parameters.Length > 0 ? "," : "");
                    }

                    writer.WriteLine
                    (
                        string.Join
                        (
                            $",{writer.NewLine}",
                            method.Parameters
                                .Skip(method.ExtensionType == null ? 0 : 1)
                                .Select(p => p.Component switch
                                {
                                    MessageHandlerParameter.MessageComponent.MessageId => $"{p.Name}: message.Id",
                                    MessageHandlerParameter.MessageComponent.ActorContainer => $"{p.Name}: actorContainer",
                                    MessageHandlerParameter.MessageComponent.None => $"{p.Name}: message.{p.Name}",
                                    _ => throw new ArgumentOutOfRangeException()
                                })
                        )
                    );
                }
            }
        }
    }
}