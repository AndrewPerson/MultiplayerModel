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

        var declaration = actor.ActorType.ObjectType switch
        {
            ObjectType.Class => "class",
            ObjectType.Struct => "struct",
            ObjectType.RecordClass => "record class",
            ObjectType.RecordStruct => "record struct",
            _ => throw new ArgumentOutOfRangeException()
        };

        writer.WriteLine($"public partial {declaration} {actor.ActorType.Type}");
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