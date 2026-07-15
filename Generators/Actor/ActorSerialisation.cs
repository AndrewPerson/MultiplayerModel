using SourceGenUtils;

namespace MultiplayerModel.Generators.Actor;

public readonly record struct ActorSerialisation(StringyType ActorType, bool IsRecordType, string SerialisationKey);
