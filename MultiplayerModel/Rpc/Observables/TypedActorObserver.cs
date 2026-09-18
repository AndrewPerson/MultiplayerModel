using MultiplayerModel.Actor;

namespace MultiplayerModel.Rpc.Observables;

/**
 * Adapts an untyped <see cref="IActor"/> watch to a typed <see cref="T"/> observer.
 */
internal sealed class TypedActorObserver<T>(IObserver<T?> observer) : IObserver<IActor?>
    where T : struct, ITypedActor<T>
{
    public void OnCompleted() => observer.OnCompleted();

    public void OnError(Exception error) => observer.OnError(error);

    public void OnNext(IActor? value)
    {
        switch (value)
        {
            case null:
                observer.OnNext(null);
                break;
            case T typed:
                observer.OnNext(typed);
                break;
            default:
                throw new InvalidOperationException(
                    $"Actor {value.Id} was expected to be of type {typeof(T).Name}, but was {value.GetType().Name}.");
        }
    }
}