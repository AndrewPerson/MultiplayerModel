using System.Runtime.CompilerServices;
using DiffEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tests;

public class ControllerGeneratorTests
{
    [ModuleInitializer]
    internal static void Init()
    {
        DiffTools.UseOrder(DiffTool.Rider);
        VerifySourceGenerators.Initialize();
    }

    [Fact]
    public static async Task Test1()
    {
        // The source code to test
        var source = @"
using MultiplayerModel.Common;
using MultiplayerModel.Generators.Controller;

public class Test
{
    [Controller(typeof(Model))]
    public void TestMethod(string value, bool otherValue, [MessageID] MessageID messageId, [Model] Model model) { }

    [Controller(typeof(Model))]
    public static void TestMethod2() { }
}

public static class TestExtensions
{
    [Controller(typeof(Model))]
    public static void TestMethod(this Test test) { }
}

public class StaticTest
{
    [Controller(typeof(Model))]
    public static void StaticTest(string value, bool otherValue, [MessageID] MessageID messageId, [Model] Model model) { }
}

public class Model : IModel<Snapshot>
{
    public void Restore(Snapshot snapshot) { }
    public Snapshot Snapshot() => new();
}

public record struct Snapshot() : ISnapshot
{
    public int Hash() => 0;
}";

        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);

        var compilation = CSharpCompilation.Create
        (
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree],
            references:
            [
                ..Basic.Reference.Assemblies.Net100.References.All,
                MetadataReference.CreateFromFile(typeof(MessageHandlerAttribute).Assembly.Location)
            ]
        );

        var generator = new ControllerGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        foreach (var result in driver.GetRunResult().Results)
        {
            if (result.Exception != null)
            {
                throw new AggregateException(result.Exception);
            }
        }

        await Verify(driver);
    }
}
