using MultiplayerModel.Actor;

namespace MultiplayerModel.Transport.Shared;

/**
 * ActorHashes is a mapping of Actor Ids to the hash of the final state of the actor. If an actor's ID isn't present,
 * that means its state didn't change. If the ID is present, but the hash is null, that means it got removed.
 */
public readonly record struct MessageOrdering(
    IReadOnlyList<AddressedMessage> Ordering,
    IReadOnlyDictionary<TypelessActorId, int?> ActorHashes,
    long Version
)
{
    public override string ToString()
    {
        return $"MessageOrdering {{ Ordering = [{string.Join(", ", Ordering.Select(o => o.ToString()))}], ActorHashes = {ActorHashes}, Version = {Version} }}";
    }
}
