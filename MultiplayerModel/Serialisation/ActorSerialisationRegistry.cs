using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;

namespace MultiplayerModel.Serialisation;

public static class ActorSerialisationRegistry
{
    private static readonly Dictionary<Type, string> ActorKeys = [];
    private static readonly Dictionary<Type, string> ActorIdKeys = [];
    
    public static void Register<T>(string key) where T : IActor
    {
        ActorKeys[typeof(T)] = key;
        ActorIdKeys[typeof(ActorId<T>)] = key;
    }

    public static bool TryGetActorKey<T>([MaybeNullWhen(false)] out string key) where T : IActor
    {
        return ActorKeys.TryGetValue(typeof(T), out key);
    }

    public static bool TryGetActorIdKey<T>([MaybeNullWhen(false)] out string key) where T : IActor
    {
        return ActorIdKeys.TryGetValue(typeof(ActorId<T>), out key);
    }

    public static List<KeyValuePair<Type, string>> GetActorKeys()
    {
        return ActorKeys.ToList();
    }
    
    public static List<KeyValuePair<Type, string>> GetActorIdKeys()
    {
        return ActorIdKeys.ToList();
    }
}
