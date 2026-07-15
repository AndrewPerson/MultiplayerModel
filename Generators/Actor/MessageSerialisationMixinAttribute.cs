namespace MultiplayerModel.Generators.Actor;

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
public class MessageSerialisationMixinAttribute(string baseKey) : Attribute
{
    public string BaseKey { get; } = baseKey;
}