using MultiplayerModel.Actor;

namespace MultiplayerModel.Rpc.Observables;

/**
 * Adapts an untyped <see cref="TypelessActorId"/> and <see cref="IActor"/> watch to a typed <see cref="ActorId{T}"/>
 * and <see cref="T"/> observer.
 */
internal sealed class IdAndTypedActorObserver<T>(IObserver<(ActorId<T>, T?)> observer)
    : IObserver<(TypelessActorId, IActor?)> where T : struct, ITypedActor<T>
{
    public void OnCompleted() => observer.OnCompleted();

    public void OnError(Exception error) => observer.OnError(error);

    public void OnNext((TypelessActorId, IActor?) value)
    {
        switch (value)
        {
            case (var id, null):
                observer.OnNext((new ActorId<T>(id.ContainerId, id.LocalId), null));
                break;
            case (var id, T typed):
                observer.OnNext((new ActorId<T>(id.ContainerId, id.LocalId), typed));
                break;
            default:
                throw new InvalidOperationException(
                    $"Actor {value.Item2.Id} was expected to be of type {typeof(T).Name}, but was {value.GetType().Name}.");
        }
    }
}