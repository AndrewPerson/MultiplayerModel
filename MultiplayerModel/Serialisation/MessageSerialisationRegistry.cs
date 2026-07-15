using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;

namespace MultiplayerModel.Serialisation;

public static class MessageSerialisationRegistry
{
    private static readonly Dictionary<Type, string> MessageKeys = [];
    
    public static void Register<T>(string key) where T : IMessage
    {
        MessageKeys[typeof(T)] = key;
    }

    public static bool TryGetKey<T>([MaybeNullWhen(false)] out string key) where T : IMessage
    {
        return MessageKeys.TryGetValue(typeof(T), out key);
    }

    public static List<KeyValuePair<Type, string>> GetMessageKeys()
    {
        return MessageKeys.ToList();
    }
}
