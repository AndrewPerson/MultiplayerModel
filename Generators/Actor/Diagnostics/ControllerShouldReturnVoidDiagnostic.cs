// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Diagnostics;
// using SourceGenUtils;
//
// namespace MultiplayerModel.Generators.Controller.Diagnostics;
//
// public static class ControllerShouldReturnVoidDiagnostic
// {
//     public const string DiagnosticId = "ControllerShouldReturnVoid";
//     private const string Title = "The controller method should return void";
//
//     private const string MessageFormat =
//         "Controller methods will not have their return value used. To avoid confusion, they should return void.";
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
//
//         if (method.HasAttribute<MessageHandlerAttribute>() && method.ReturnType.SpecialType != SpecialType.System_Void)
//         {
//             context.ReportDiagnostic
//             (
//                 Microsoft.CodeAnalysis.Diagnostic.Create
//                 (
//                     Diagnostic,
//                     ((MethodDeclarationSyntax)context.Node).ReturnType.GetLocation()
//                 )
//             );
//         }
//     }
// }