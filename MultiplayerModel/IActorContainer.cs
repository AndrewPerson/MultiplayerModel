using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;
using MultiplayerModel.Rpc;

namespace MultiplayerModel;

public interface IActorContainer
{
    public uint Id { get; }
    
    public IActorIdGenerator ActorIdGenerator { get; }
    public ISimpleActorIdGenerator SelfActorIdGenerator => ActorIdGenerator.MakeSimple(Id); 
    
    public IMessageIdGenerator MessageIdGenerator { get; }
    public ISimpleMessageIdGenerator SelfMessageIdGenerator => MessageIdGenerator.MakeSimple(Id);

    public ActorId<T> ActorId<T>() where T : IActor => SelfActorIdGenerator.Generate<T>();
    public MessageId MessageId() => SelfMessageIdGenerator.Generate();
    
    /**
     * <summary>Sends a message to the actor with the corresponding <paramref name="actorId"/>.</summary>
     *
     * <remarks>
     * If no such actor exists, then the actor is treated as if it received the message, but made
     * no action based off of it. This does <i>NOT</i> mean this function is itself a no-op, as
     * other misc. processing may still occur, i.e. replicating the message in the case of
     * <see cref="RpcClientActorContainer"/>
     * </remarks>
     */
    public T? SendMessage<T, TMessage>(IActorId<T> actorId, TMessage message)
        where T : struct, ITypedActor<T, TMessage> where TMessage : IMessage;

    /**
     * <summary>Sends a message to the actor with the corresponding <paramref name="actorId"/>.</summary>
     *
     * <remarks>
     * If no such actor exists, then the actor is treated as if it received the message, but made
     * no action based off of it. This does <i>NOT</i> mean this function is itself a no-op, as
     * other misc. processing may still occur, i.e. replicating the message in the case of
     * <see cref="RpcClientActorContainer"/>
     * </remarks>
     */
    public IActor? SendMessage(IActorId actorId, IMessage message);

    public IReadOnlySet<TypelessActorId> ListActors();
    public IReadOnlySet<ActorId<T>> ListActors<T>() where T : struct, ITypedActor<T>;

    public bool ContainsActor<T>(IActorId<T> id) where T : struct, ITypedActor<T>;
    public bool ContainsActor(IActorId id);
    
    /**
     * Gets the actor with the corresponding <paramref name="id"/>.
     * 
     * <exception cref="KeyNotFoundException">The <paramref name="id"/> was not found.</exception>
     */
    public T GetActor<T>(IActorId<T> id) where T : struct, ITypedActor<T>;
    
    /**
     * Gets the actor with the corresponding <paramref name="id"/>.
     *
     * <returns>true if the actor with the corresponding <paramref name="id"/> is found, false otherwise.</returns>
     */
    public bool TryGetActor<T>(IActorId<T> id, out T actor) where T : struct, ITypedActor<T>;

    /**
     * <remarks>Less stringently typed version of <see cref="TryGetActor{T}"/></remarks>
     */
    public bool TryGetActor(IActorId id, [MaybeNullWhen(false)] out IActor actor);
    
    public void AddActor<T>(ITypedActor<T> actor) where T : struct, ITypedActor<T>;
    
    public T RemoveActor<T>(IActorId<T> id) where T : struct, ITypedActor<T>;
    public bool TryRemoveActor<T>(IActorId<T> id, out T actor) where T : struct, ITypedActor<T>;
}
