using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
// using MultiplayerModel.Generators.Controller.Diagnostics;

namespace MultiplayerModel.Generators.Actor;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class Analyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        // UseControllerMethodDiagnostic.Diagnostic,
        // ControllerInputModelMustMatchAttributeModelDiagnostic.Diagnostic,
        // ControllerMethodMustBePublicDiagnostic.NonPublicMethodDiagnostic,
        // ControllerMethodMustBePublicDiagnostic.NonPublicTypeDiagnostic,
        // ControllerShouldReturnVoidDiagnostic.Diagnostic
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        // UseControllerMethodDiagnostic.RegisterForAnalysis(context);
        // ControllerInputModelMustMatchAttributeModelDiagnostic.RegisterForAnalysis(context);
        // ControllerMethodMustBePublicDiagnostic.RegisterForAnalysis(context);
        // ControllerShouldReturnVoidDiagnostic.RegisterForAnalysis(context);
    }
}