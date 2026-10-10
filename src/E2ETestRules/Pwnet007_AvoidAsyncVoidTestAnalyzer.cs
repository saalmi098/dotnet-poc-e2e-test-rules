using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET007 - <c>async void</c> test methods.
/// <para><b>Finds:</b> xUnit <c>[Fact]</c>/<c>[Theory]</c> methods declared <c>async void</c>.</para>
/// <para><b>Why:</b> the runner cannot await the test, so failures after the first <c>await</c> are lost or crash
/// the run. xUnit v3 rejects such tests at runtime (its own analyzer reports xUnit1049).</para>
/// <para><b>Ignores:</b> <c>async void</c> methods without a test attribute (event handlers).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet007_AvoidAsyncVoidTestAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Avoid async void test methods",
        messageFormat: "Test method '{0}' is async void. Return Task so the test runner can await it and observe failures.",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword)
            || method.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }
            || !SyntaxHelpers.IsTestMethod(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.ValueText));
    }
}
