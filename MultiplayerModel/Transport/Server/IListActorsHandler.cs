using MultiplayerModel.Actor;

namespace MultiplayerModel.Transport.Server;

public interface IListActorsHandler
{
    /**
     * Lists all actors on the server
     */
    public IReadOnlySet<TypelessActorId> ListActors();
}