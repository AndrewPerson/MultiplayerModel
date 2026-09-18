using System.Diagnostics.CodeAnalysis;
using MultiplayerModel.Actor;
using MultiplayerModel.Rpc.Observables;

namespace MultiplayerModel;

/**
 * Allows an <see cref="IActorContainer"/> to easily process messages, determine what actors have been mutated, and
 * revert those changes if necessary.
 *
 * <remarks>
 * This works by making copies and keeping track of all actors retrieved through it when processing messages.
 *
 * It also clones the <see cref="ActorIdGenerator"/> and <see cref="MessageIdGenerator"/> and can roll them back as
 * well.
 * </remarks>
 */
public class ChangeCalculationActorContainer(IActorContainer parent) : IActorContainer
{
    public uint Id => parent.Id;

    public IActorIdGenerator ActorIdGenerator { get; private set; } = parent.ActorIdGenerator.Clone();
    public IMessageIdGenerator MessageIdGenerator { get; private set; } = parent.MessageIdGenerator.Clone();

    /**
     * A collection of all actors that have changed after sending various messages with
     * <see cref="SendMessage{T,TMessage}(ActorId{T}, TMessage)"/> (or any other overload). These actors are NOT the
     * same objects as the actors with corresponding ids in <see cref="parent"/> (they are copies). To apply to a
     * container, copy all the dirty values into the container's internal actor storage. <see cref="ApplyTo"/> can
     * automate this for you.
     *
     * <remarks>A null value means the actor was removed.</remarks>
     *
     * <seealso cref="TryGetActor"/>
     */
    public IReadOnlyDictionary<TypelessActorId, IActor?> DirtyActors => dirtyActors;
    private readonly Dictionary<TypelessActorId, IActor?> dirtyActors = [];

    /**
     * A list of all messages that have been applied using <see cref="SendMessage{T,TMessage}(ActorId{T}, TMessage)"/>
     * (or any other overload).
     */
    public IReadOnlyList<(TypelessActorId, IMessage)> AppliedMessages => appliedMessages;
    private readonly List<(TypelessActorId, IMessage)> appliedMessages = [];
    
    /**
     * Resets the container to make it appear as if it was just constructed. This will NOT make it the same as how it
     * was when it was *originally* constructed; i.e. the <see cref="IActorContainer.ActorIdGenerator"/> and
     * <see cref="IActorContainer.MessageIdGenerator"/> on the parent may have already moved on.
     *
     * <remarks>
     * Clears all dirty actors and applied messages and re-clones the actor and message id generators from
     * <see cref="parent"/>.
     * </remarks>
     *
     * <seealso cref="DirtyActors"/>
     * <seealso cref="AppliedMessages"/>
     * <seealso cref="ActorIdGenerator"/>
     * <seealso cref="MessageIdGenerator"/>
     */
    public void ReCreate()
    {
        dirtyActors.Clear();
        appliedMessages.Clear();
        ActorIdGenerator = parent.ActorIdGenerator.Clone();
        MessageIdGenerator = parent.MessageIdGenerator.Clone();
    }

    /**
     * Applies the changes from <see cref="DirtyActors"/> to <paramref name="target"/>.
     */
    public void ApplyTo(IDictionary<TypelessActorId, IActor> target, ActorWatchRegistry? actorWatchRegistry)
    {
        foreach (var (id, actor) in DirtyActors)
        {
            if (actor is null)
            {
                var oldActor = target.Remove(id);
                actorWatchRegistry?.RecordChange(id, oldActor.GetType(), null);
            }
            else
            {
                target[id] = actor;
                actorWatchRegistry?.RecordChange(id, actor.GetType(), actor);
            }
        }
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * This will call <see cref="IActorContainer.ListActors"/> on <see cref="parent"/>, so make sure the implementation
     * on <see cref="parent"/> doesn't call this as well.
     * </remarks>
     */
    public IReadOnlySet<TypelessActorId> ListActors()
    {
        return parent.ListActors().Union(dirtyActors.Keys).ToHashSet();
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * This will call <see cref="IActorContainer.ListActors{T}"/> on <see cref="parent"/>, so make sure the
     * implementation on <see cref="parent"/> doesn't call this as well.
     * </remarks>
     */
    public IReadOnlySet<ActorId<T>> ListActors<T>() where T : struct, ITypedActor<T>
    {
        return parent
            .ListActors<T>()
            .Union(
                dirtyActors
                    .Where(kv => kv.Value is T)
                    .Select(kv => new ActorId<T>(kv.Key.ContainerId, kv.Key.LocalId))
            )
            .ToHashSet();
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * This will call <see cref="IActorContainer.ContainsActor{T}"/> on <see cref="parent"/>, so make sure the
     * implementation on <see cref="parent"/> doesn't call this as well.
     * </remarks>
     */
    public bool ContainsActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        if (dirtyActors.TryGetValue(id, out var uncastActor))
        {
            return uncastActor switch
            {
                null => false,
                T => true,
                _ => parent.ContainsActor(id)
            };
        }

        return parent.ContainsActor(id);
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * This will call <see cref="IActorContainer.ContainsActor"/> on <see cref="parent"/>, so make sure the
     * implementation on <see cref="parent"/> doesn't call this as well.
     * </remarks>
     */
    public bool ContainsActor(TypelessActorId id)
    {
        if (dirtyActors.TryGetValue(id, out var actor))
        {
            return actor is not null;
        }
        
        return parent.ContainsActor(id);
    }

    /**
     * <inheritdoc/>
     * <seealso cref="DirtyActors"/>
     * <seealso cref="AppliedMessages"/>
     */
    public T? SendMessage<T, TMessage>(ActorId<T> actorId, TMessage message)
        where T : struct, ITypedActor<T, TMessage> where TMessage : IMessage
    {
        return SendMessage(actorId, ref message);
    }

    /**
     * <seealso cref="DirtyActors"/>
     * <seealso cref="AppliedMessages"/>
     */
    public T? SendMessage<T, TMessage>(ActorId<T> actorId, ref TMessage message)
        where T : struct, ITypedActor<T, TMessage> where TMessage : IMessage
    {
        if (TryGetActor(actorId, out var actor))
        {
            var newActor = actor.ProcessMessage(this, ref message);
            dirtyActors[actorId] = newActor;

            appliedMessages.Add((actorId, message));
            
            return newActor;
        }
        else
        {
            appliedMessages.Add((actorId, message));
            return null;
        }
    }

    /**
     * <seealso cref="DirtyActors"/>
     * <seealso cref="AppliedMessages"/>
     */
    public IActor? SendMessage(TypelessActorId actorId, IMessage message) => SendMessage(actorId, ref message);

    /**
     * <seealso cref="DirtyActors"/>
     * <seealso cref="AppliedMessages"/>
     */
    public IActor? SendMessage(TypelessActorId actorId, ref IMessage message)
    {
        if (TryGetActor(actorId, out var actor))
        {
            var newActor = message.TryDispatch(this, actor);
            dirtyActors[actorId] = newActor;
            
            appliedMessages.Add((actorId, message));

            return newActor;
        }
        else
        {
            appliedMessages.Add((actorId, message));
            return null;
        }
    }

    /**
     * <inheritdoc/>
     * <remarks>Forwards to <see cref="TryGetActor"/> under the hood</remarks>
     */
    public T GetActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        if (TryGetActor(id, out var actor))
        {
            return actor;
        }

        throw new KeyNotFoundException();
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * Forwards to <see cref="IActorContainer.TryGetActor{T}"/> on <see cref="parent"/> if the id is not already present
     * in <see cref="DirtyActors"/>.
     * <br/>
     * Any new actors retrieved through this method are copied (due to being structs) and added to
     * <see cref="DirtyActors"/>.
     * <br/>
     * This allows changes to actors to be tracked and also applied in isolation to the actors in <see cref="parent"/>
     * </remarks>
     */
    public bool TryGetActor<T>(ActorId<T> id, out T actor) where T : struct, ITypedActor<T>
    {
        if (dirtyActors.TryGetValue(id, out var uncastActor))
        {
            // A null value means the actor has been removed.
            if (uncastActor is null)
            {
                actor = default;
                return false;
            }

            if (uncastActor is T castActor)
            {
                actor = castActor;
                return true;
            }
        }

        if (parent.TryGetActor(id, out actor))
        {
            dirtyActors[id] = actor;
            return true;
        }

        actor = default;
        return false;
    }

    public bool TryGetActor(TypelessActorId id, [MaybeNullWhen(false)] out IActor actor)
    {
        if (dirtyActors.TryGetValue(id, out actor))
        {
            // A null value means the actor has been removed.
            return actor is not null;
        }

        if (parent.TryGetActor(id, out actor))
        {
            dirtyActors[id] = actor;
            return true;
        }

        actor = null;
        return false;
    }

    /**
     * <inheritdoc/>
     *
     * <remarks>
     * Any actors added through this method are added to <see cref="DirtyActors"/>, but NOT cloned.
     * <br/>
     * This is because this method should only be called by actors processing messages from this container, meaning the
     * new actors added are wholly new and not used anywhere else, meaning they need to be cloned to be isolated.
     * </remarks>
     */
    public void AddActor<T>(in T actor) where T : struct, ITypedActor<T>
    {
        dirtyActors.Add(actor.Id, actor);
    }

    public T RemoveActor<T>(ActorId<T> id) where T : struct, ITypedActor<T>
    {
        if (TryRemoveActor(id, out var actor))
        {
            return actor;
        }

        throw new KeyNotFoundException();
    }

    public bool TryRemoveActor<T>(ActorId<T> id, out T actor) where T : struct, ITypedActor<T>
    {
        if (TryGetActor(id, out actor))
        {
            dirtyActors[id] = null;
            return true;
        }

        return false;
    }
}