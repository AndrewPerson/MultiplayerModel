using MultiplayerModel.Actor;
using MultiplayerModel.Generators.Actor;

namespace Playground;

[ActorSerialisationMixin("person")]
[MessageSerialisationMixin("person")]
public readonly partial record struct Person(ActorId<Person> Id, string Name) : ITypedActor<Person>
{
    [MessageHandler]
    public Person Greet([MessageId] MessageId messageId)
    {
        Console.WriteLine($"{messageId}: I am {Name}!");
        return this;
    }

    [MessageHandler]
    public Person ChangeName([MessageId] MessageId messageId, Foo<string> newName)
    {
        Console.WriteLine($"{messageId}: I have changed my name from {Name} to {newName}!");
        return this with { Name = newName.Value };
    }

    public int StableHash()
    {
        return 0; // TODO
    }
}

public record struct Foo<T>(T Value);

public static class PersonExtensions
{
    [MessageHandler]
    public static Person Greet2(this Person person, [MessageId] MessageId messageId)
    {
        Console.WriteLine($"{messageId}: I am {person.Name}! (From extension!)");
        return person;
    }
}
