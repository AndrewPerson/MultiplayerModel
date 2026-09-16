using System.Text.Json.Serialization;
using MultiplayerModel.Actor.Json;

namespace MultiplayerModel.Actor;

public interface IActorId
{
    public uint ContainerId { get; }
    public ulong LocalId { get; }
}

public interface IActorId<out T> : IActorId where T : IActor;

public readonly record struct ActorId<T>(uint ContainerId, ulong LocalId) : IActorId<T> where T : IActor
{
    public static implicit operator TypelessActorId(ActorId<T> self)
    {
        return new TypelessActorId(self.ContainerId, self.LocalId);
    }
}

[JsonConverter(typeof(TypelessActorIdConverter))]
public readonly record struct TypelessActorId(uint ContainerId, ulong LocalId) : IActorId;