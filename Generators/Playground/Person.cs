using MultiplayerModel.Actor;
using MultiplayerModel.Generators.Actor;

namespace Playground;

[ActorSerialisationMixin("person")]
[MessageSerialisationMixin("person")]
public partial class Person(ActorId<Person> id, string name) : ITypedActor<Person>
{
    public ActorId<Person> Id { get; } = id;
    public string Name { get; private set; } = name;

    [MessageHandler]
    public void Greet([MessageId] MessageId messageId)
    {
        Console.WriteLine($"{messageId}: I am {Name}!");
    }

    [MessageHandler]
    public void ChangeName([MessageId] MessageId messageId, Foo<string> newName)
    {
        Console.WriteLine($"{messageId}: I have changed my name from {Name} to {newName}!");
        Name = newName.Value;
    }

    public Person Clone()
    {
        return new Person(Id, Name);
    }

    public override string ToString()
    {
        return $"Person {{ Id = {Id}, Name = {Name} }}";
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
    public static void Greet2(this Person person, [MessageId] MessageId messageId)
    {
        Console.WriteLine($"{messageId}: I am {person.Name}! (From extension!)");
    }
}
