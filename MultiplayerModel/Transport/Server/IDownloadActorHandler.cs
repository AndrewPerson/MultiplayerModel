using MultiplayerModel.Actor;
using MultiplayerModel.Transport.Client;
using MultiplayerModel.Transport.Shared;

namespace MultiplayerModel.Transport.Server;

public interface IActorDownloadHandler
{
    /**
     * <returns>null if the actor isn't found, otherwise it returns the actor</returns>
     */
    public IActor? GetActorForDownload(TypelessActorId actorId);
}