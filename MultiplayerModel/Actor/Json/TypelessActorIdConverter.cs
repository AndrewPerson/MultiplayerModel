using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiplayerModel.Actor.Json;

public class TypelessActorIdConverter : JsonConverter<TypelessActorId>
{
    public override TypelessActorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        uint? containerId = null;
        Guid? localId = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();

                if (!reader.Read()) throw new JsonException();

                if (propertyName == "ContainerId")
                {
                    if (containerId is not null) throw new JsonException();
                    containerId = reader.GetUInt32();
                }
                else if (propertyName == "LocalId")
                {
                    if (localId is not null) throw new JsonException();
                    localId = ((JsonConverter<Guid>)options.GetConverter(typeof(Guid)))
                        .Read(ref reader, typeof(Guid), options);
                }
                else
                {
                    throw new JsonException();
                }
            }
            else
            {
                throw new JsonException();
            }
        }

        if (containerId is null || localId is null)
        {
            throw new JsonException();
        }

        return new(containerId.Value, localId.Value);
    }

    public override void Write(Utf8JsonWriter writer, TypelessActorId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("ContainerId", value.ContainerId);

        writer.WritePropertyName("LocalId");
        ((JsonConverter<Guid>)options.GetConverter(typeof(Guid))).Write(writer, value.LocalId, options);

        writer.WriteEndObject();
    }

    public override TypelessActorId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var propertyName = reader.GetString()!;
        if (propertyName.Split(':') is [var containerIdString, var localIdString])
        {
            if (uint.TryParse(containerIdString, out var containerId)
                && Guid.TryParse(localIdString, out var localid))
            {
                return new(containerId, localid);
            }
        }

        throw new JsonException();
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TypelessActorId value,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.ContainerId}:{value.LocalId}");
    }
}