using SourceGenUtils;

namespace MultiplayerModel.Generators.Actor.Writers;

public static class WrappedMethodWriter
{
    public static void Write(IndentedTextWriter writer, MessageHandlerMethod method)
    {
        var methodMessageType = Types.MethodMessage(method);

        if (method.ContainingType.Namespace != null)
        {
            writer.WriteLine($"namespace {method.ContainingType.Namespace};");
            writer.WriteLine();
        }

        writer.WriteLine($"public static class {method.ContainingType.Type}{method.MethodName}WrappedExtension");
        using (writer.WriteBlock("{", "}"))
        {
            if (method.ExtensionType == null)
            {
                var parameterList = method.Parameters
                    .Where(p => p.Component == MessageHandlerParameter.MessageComponent.None)
                    .Prepend(new(Types.IActorContainerType, "actorContainer",
                        MessageHandlerParameter.MessageComponent.None))
                    .Select(p => $"{p.Type} {p.Name}")
                    .Prepend($"this {method.ContainingType} self")
                    .ToList();

                writer.WriteLine($"public static {method.ContainingType} {method.MethodName}({string.Join(", ", parameterList)})");
                using (writer.WriteBlock("{", "}"))
                {
                    var messageParameterList = method.Parameters
                        .Where(p => p.Component == MessageHandlerParameter.MessageComponent.None)
                        .Select(p => $"{p.Name}: {p.Name}")
                        .Prepend("Id: actorContainer.MessageId()")
                        .ToList();

                    writer.WriteLine($"return actorContainer.SendMessage(self.Id, new {methodMessageType}({string.Join(", ", messageParameterList)})) ?? self;");
                }
            }
            else
            {
                var actorParameter = method.Parameters[0];
                
                var parameterList = method.Parameters
                    .Where(p => p.Component == MessageHandlerParameter.MessageComponent.None)
                    .Select(p => $"{p.Type} {p.Name}")
                    .ToList();

                parameterList.Insert(1, $"{Types.IActorContainerType} actorContainer");

                writer.WriteLine($"public static {method.ContainingType} {method.MethodName}(this {string.Join(", ", parameterList)})");
                using (writer.WriteBlock("{", "}"))
                {
                    var messageParameterList = method.Parameters
                        .Skip(1)
                        .Where(p => p.Component == MessageHandlerParameter.MessageComponent.None)
                        .Select(p => $"{p.Name}: {p.Name}")
                        .Prepend("Id: actorContainer.MessageId()")
                        .ToList();

                    writer.WriteLine(
                        $"return actorContainer.SendMessage({actorParameter.Name}.Id, new {methodMessageType}({string.Join(", ", messageParameterList)})) ?? {actorParameter.Name};"
                    );
                }
            }
        }
    }
}