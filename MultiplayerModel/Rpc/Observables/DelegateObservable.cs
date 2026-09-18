namespace MultiplayerModel.Rpc.Observables;

/**
 * A minimal <see cref="IObservable{T}"/> backed by a subscription factory.
 */
internal sealed class DelegateObservable<T>(Func<IObserver<T>, IDisposable> onSubscribe) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer) => onSubscribe(observer);
}
