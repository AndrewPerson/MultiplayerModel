using System.Text.Json.Serialization;
using MultiplayerModel.Actor.Json;

namespace MultiplayerModel.Actor;

public interface IActorId
{
    public uint ContainerId { get; }
    public ulong LocalId { get; }
}

public interface IActorId<out T> : IActorId where T : IActor;

public readonly record struct ActorId<T>(uint ContainerId, ulong LocalId) : IActorId<T> where T : IActor;

[JsonConverter(typeof(TypelessActorIdConverter))]
public readonly record struct TypelessActorId(uint ContainerId, ulong LocalId) : IActorId
{
    public TypelessActorId(IActorId other) : this(other.ContainerId, other.LocalId)
    {
    }
}
