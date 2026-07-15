namespace MultiplayerModel.Generators.Actor;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
public class ActorSerialisationMixinAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}