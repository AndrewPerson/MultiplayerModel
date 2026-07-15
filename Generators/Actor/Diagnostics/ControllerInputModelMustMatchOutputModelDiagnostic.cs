// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
// using SourceGenUtils;
//
// namespace MultiplayerModel.Generators.Controller.Diagnostics;
//
// public static class ControllerInputModelMustMatchAttributeModelDiagnostic
// {
//     public const string DiagnosticId = "ControllerInputModelMustMatchAttributeModel";
//     private const string Title = "The type of the model used as input must match the type of the model in the ControllerAttribute";
//
//     private const string MessageFormat =
//         "This parameter accepts {0}, but the ControllerAttribute has {1} as the model. This parameter will be treated as a regular parameter, not a model parameter.";
//
//     private const string Category = "Usage";
//
//     public static readonly DiagnosticDescriptor Diagnostic = new
//     (
//         DiagnosticId,
//         Title,
//         MessageFormat,
//         Category,
//         DiagnosticSeverity.Warning,
//         isEnabledByDefault: true,
//         description: null
//     );
//     
//     public static void RegisterForAnalysis(AnalysisContext context)
//     {
//         context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
//     }
//     
//     public static void Analyze(SyntaxNodeAnalysisContext context)
//     {
//         var method = (IMethodSymbol)context.ContainingSymbol!;
//         var methodSyntax = (MethodDeclarationSyntax)context.Node;
//
//         var modelType = method.GetAttributes<MessageHandlerAttribute>().FirstOrDefault()?.ConstructorArguments[0].Type;
//         if (modelType == null) return;
//         
//         foreach (var (parameter, parameterSyntax) in
//                  method.Parameters.Zip(methodSyntax.ParameterList.Parameters, (symbol, syntax) => (symbol, syntax)))
//         {
//             if (parameter.HasAttribute<ModelAttribute>() &&
//                 !parameter.Type.Equals(modelType, SymbolEqualityComparer.Default))
//             {
//                 context.ReportDiagnostic
//                 (
//                     Microsoft.CodeAnalysis.Diagnostic.Create
//                     (
//                         Diagnostic,
//                         parameterSyntax.GetLocation(),
//                         parameter.Type, modelType 
//                     )
//                 );
//             }
//         }
//     }
// }