using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MultiplayerModel.Actor;

namespace MultiplayerModel.Serialisation.Json;

public class PolymorphicTypeResolver(Type baseType, IList<KeyValuePair<Type, string>> typeDiscriminators)
    : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);
        
        // The following IsAssignableTo check seems backwards, but isn't.
        // The idea is that we want the type resolver to be as broad as possible, i.e. if it encounters a subclass, then
        // it will automatically pick the subset of types that inherit from that subclass (see the filter below). This
        // helps remove the need for many instances of this class, especially for generics.
        if ((jsonTypeInfo.Type.IsInterface || jsonTypeInfo.Type.IsAbstract)
            && jsonTypeInfo.Type.IsAssignableTo(baseType))
        {
            jsonTypeInfo.PolymorphismOptions = new()
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };

            foreach (var jsonDerivedType in typeDiscriminators
                         .Where(kv => kv.Key.IsAssignableTo(jsonTypeInfo.Type))
                         .Select(kv => new JsonDerivedType(kv.Key, kv.Value)))
            {
                jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(jsonDerivedType);
            }

            return jsonTypeInfo;
        }

        return null;
    }
}