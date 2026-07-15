using SourceGenUtils;

namespace MultiplayerModel.Generators.Actor.Writers;

public static class ActorSerialisationWriter
{
    public static void Write(IndentedTextWriter writer, ActorSerialisation actor)
    {
        if (actor.ActorType.Namespace != null)
        {
            writer.WriteLine($"namespace {actor.ActorType.Namespace};");
            writer.WriteLine();
        }

        writer.WriteLine(actor.IsRecordType
            ? $"public partial record struct {actor.ActorType.Type}"
            : $"public partial struct {actor.ActorType.Type}");
        using (writer.WriteBlock("{", "}"))
        {
            writer.WriteLine($"[{Types.ModuleInitializerAttributeType}]");
            writer.WriteLine("public static void RegisterForSerialisation()");
            using (writer.WriteBlock("{", "}"))
            {
                writer.WriteLine($"{Types.ActorSerialisationRegistryType}.Register<{actor.ActorType}>(\"{actor.SerialisationKey}\");");
            }
        }
    }
}