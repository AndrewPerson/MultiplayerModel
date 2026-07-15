using MultiplayerModel.Actor;

namespace MultiplayerModel.Transport.Shared;

public readonly record struct AddressedMessage(TypelessActorId ActorId, IMessage Message);