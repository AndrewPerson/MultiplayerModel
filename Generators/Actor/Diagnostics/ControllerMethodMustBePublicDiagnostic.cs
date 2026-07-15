// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
// using SourceGenUtils;
//
// namespace MultiplayerModel.Generators.Controller.Diagnostics;
//
// public static class ControllerMethodMustBePublicDiagnostic
// {
//     public const string DiagnosticId = "ControllerMethodMustBePublic";
//     private const string Title = "Controller methods must be public and within public types";
//
//     private const string NonPublicMethodMessageFormat =
//         "Controller methods must be public, but this method is {0}. A controller will not be generated for this method.";
//
//     private const string NonPublicTypeMessage =
//         "Controller methods must be public, but this method's containing type {0} is {1}. A controller will not be generated for this method.";
//
//     private const string Category = "Usage";
//
//     public static readonly DiagnosticDescriptor NonPublicMethodDiagnostic = new
//     (
//         DiagnosticId,
//         Title,
//         NonPublicMethodMessageFormat,
//         Category,
//         DiagnosticSeverity.Warning,
//         isEnabledByDefault: true,
//         description: null
//     );
//
//     public static readonly DiagnosticDescriptor NonPublicTypeDiagnostic = new
//     (
//         DiagnosticId,
//         Title,
//         NonPublicTypeMessage,
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
//         
//         if (!method.HasAttribute<MessageHandlerAttribute>()) return;
//
//         if (method.DeclaredAccessibility != Accessibility.Public)
//         {
//             var declarationNode = (MethodDeclarationSyntax)context.Node;
//             var diagnosticLocation = declarationNode.Modifiers.Count == 0
//                 ? declarationNode.Identifier.GetLocation()
//                 : declarationNode.Modifiers[0].GetLocation();
//             
//             context.ReportDiagnostic
//             (
//                 Diagnostic.Create
//                 (
//                     NonPublicMethodDiagnostic,
//                     diagnosticLocation,
//                     method.DeclaredAccessibility
//                 )
//             );
//
//             return;
//         }
//
//         var symbol = method.ContainingSymbol;
//         while (symbol is not null)
//         {
//             if (symbol.DeclaredAccessibility != Accessibility.NotApplicable &&
//                 symbol.DeclaredAccessibility != Accessibility.Public)
//             {
//                 context.ReportDiagnostic
//                 (
//                     Diagnostic.Create
//                     (
//                         NonPublicTypeDiagnostic,
//                         ((MethodDeclarationSyntax)context.Node).Identifier.GetLocation(),
//                         symbol.Name,
//                         symbol.DeclaredAccessibility
//                     )
//                 );
//
//                 return;
//             }
//
//             symbol = symbol.ContainingSymbol;
//         }
//     }
// }