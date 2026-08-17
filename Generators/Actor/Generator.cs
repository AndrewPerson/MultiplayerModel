using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using MultiplayerModel.Generators.Actor.Writers;
using SourceGenUtils;
using SourceGenUtils.Collections;
using IndentedTextWriter = SourceGenUtils.IndentedTextWriter;

namespace MultiplayerModel.Generators.Actor;

[Generator(LanguageNames.CSharp)]
public class ActorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var messageHandlerMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName<MessageHandlerMethod?>
            (
                typeof(MessageHandlerAttribute).FullName!,
                static (node, _) => node is BaseMethodDeclarationSyntax,
                static (context, _) =>
                {
                    var methodSymbol = (IMethodSymbol)context.TargetSymbol;

                    ISymbol? symbol = methodSymbol;
                    while (symbol is not null)
                    {
                        if (symbol.DeclaredAccessibility != Accessibility.Public &&
                            symbol.DeclaredAccessibility != Accessibility.NotApplicable) return null;

                        symbol = symbol.ContainingSymbol;
                    }

                    var containingType = methodSymbol.ContainingType;
                    if (containingType.ContainingType != null)
                    {
                        // TODO: Report that nested types aren't supported
                    }

                    var messageSerialisationBaseKey = (string?)containingType
                        .GetAttributes<MessageSerialisationMixinAttribute>()
                        .FirstOrDefault()?
                        .ConstructorArguments[0]
                        .Value;

                    var parameters = methodSymbol
                        .Parameters
                        .Select
                        (p => new MessageHandlerParameter
                            (
                                new(p.Type),
                                p.Name,
                                p.HasAttribute<MessageIdAttribute>()
                                && new StringyType(p.Type) == Types.MessageIdType
                                    ? MessageHandlerParameter.MessageComponent.MessageId
                                    : p.HasAttribute<ActorContainerAttribute>()
                                      && new StringyType(p.Type) == Types.IActorContainerType
                                        ? MessageHandlerParameter.MessageComponent.ActorContainer
                                        : MessageHandlerParameter.MessageComponent.None
                            )
                        )
                        .ToImmutableEquatableArray();

                    if (methodSymbol.IsExtensionMethod)
                    {
                        var realContainingType = methodSymbol.Parameters[0].Type;
                        return new MessageHandlerMethod
                        (
                            new(realContainingType),
                            new(containingType),
                            realContainingType.IsRecord,
                            methodSymbol.Name,
                            messageSerialisationBaseKey,
                            parameters
                        );
                    }
                    else
                    {
                        return new MessageHandlerMethod
                        (
                            new(containingType),
                            null,
                            containingType.IsRecord,
                            methodSymbol.Name,
                            messageSerialisationBaseKey,
                            parameters
                        );
                    }
                }
            )
            .Where(static m => m is not null)
            .Select(static (m, _) => m!.Value);

        var actorsWithSerialisationMixin = context.SyntaxProvider
            .ForAttributeWithMetadataName<ActorSerialisation>
            (
                typeof(ActorSerialisationMixinAttribute).FullName!,
                static (node, _) => node is StructDeclarationSyntax || node is RecordDeclarationSyntax,
                static (context, _) => new(
                    new((ITypeSymbol)context.TargetSymbol),
                    ((ITypeSymbol)context.TargetSymbol).IsRecord,
                    (string)context.TargetSymbol
                        .GetAttributes<ActorSerialisationMixinAttribute>()
                        .First()
                        .ConstructorArguments[0]
                        .Value!
                )
            );

        context.RegisterSourceOutput
        (
            messageHandlerMethods,
            static (context, method) =>
            {
                var hintName = $"{method.ContainingType}.{method.MethodName}.MessageHandler.g.cs"
                    .Replace(':', '_')
                    .Replace('<', '_')
                    .Replace('>', '_');

                using var stream = new MemoryStream();
                using var innerWriter = new StreamWriter(stream);
                using var writer = new IndentedTextWriter(innerWriter);

                MessageHandlerMethodWriter.Write(writer, method);

                writer.Flush();

                using var reader = new StreamReader(stream);

                stream.Seek(0, SeekOrigin.Begin);
                context.AddSource(hintName, SourceText.From(reader.ReadToEnd(), encoding: Encoding.UTF8));
            }
        );

        context.RegisterSourceOutput
        (
            messageHandlerMethods,
            static (context, method) =>
            {
                var hintName = $"{method.ContainingType}.{method.MethodName}.WrappedMethod.g.cs"
                    .Replace(':', '_')
                    .Replace('<', '_')
                    .Replace('>', '_');

                using var stream = new MemoryStream();
                using var innerWriter = new StreamWriter(stream);
                using var writer = new IndentedTextWriter(innerWriter);

                WrappedMethodWriter.Write(writer, method);

                writer.Flush();

                using var reader = new StreamReader(stream);

                stream.Seek(0, SeekOrigin.Begin);
                context.AddSource(hintName, SourceText.From(reader.ReadToEnd(), encoding: Encoding.UTF8));
            }
        );
        
        context.RegisterSourceOutput
        (
            actorsWithSerialisationMixin,
            static (context, actor) =>
            {
                var hintName = $"{actor.ActorType}.Serialisation.g.cs"
                    .Replace(':', '_')
                    .Replace('<', '_')
                    .Replace('>', '_');

                using var stream = new MemoryStream();
                using var innerWriter = new StreamWriter(stream);
                using var writer = new IndentedTextWriter(innerWriter);

                ActorSerialisationWriter.Write(writer, actor);

                writer.Flush();

                using var reader = new StreamReader(stream);

                stream.Seek(0, SeekOrigin.Begin);
                context.AddSource(hintName, SourceText.From(reader.ReadToEnd(), encoding: Encoding.UTF8));
            }
        );
    }
}