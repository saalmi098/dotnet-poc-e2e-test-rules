using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET001 - fixed delays in test code.
/// <para><b>Finds:</b> calls to <c>System.Threading.Tasks.Task.Delay</c>, <c>System.Threading.Thread.Sleep</c> and
/// Playwright's <c>WaitForTimeoutAsync</c> in test code (path contains <c>/e2e/</c> or <c>/tests/</c>, or assembly
/// name ends with <c>.Tests</c>).</para>
/// <para><b>Why:</b> fixed sleeps make tests slow and flaky; use Playwright auto-waiting or web-first assertions.</para>
/// <para><b>Ignores:</b> <c>Delay</c>/<c>Sleep</c> methods on other types; condition-based waits
/// (<c>WaitForURLAsync</c>, <c>WaitForResponseAsync</c>, ...); non-test code.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidDelayInE2ETestsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Avoid fixed delays in E2E tests",
        messageFormat: "Avoid {0} in Playwright E2E tests. Use Playwright auto-waiting or web-first assertions instead.",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var taskType = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var threadType = start.Compilation.GetTypeByMetadataName("System.Threading.Thread");
            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, taskType, threadType), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? taskType, INamedTypeSymbol? threadType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var name = SyntaxHelpers.GetInvokedName(invocation);
        if (name is not ("Delay" or "Sleep" or "WaitForTimeoutAsync"))
        {
            return;
        }

        if (!TestCodeDetector.IsTestCode(invocation.SyntaxTree, context.Compilation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var isFixedDelay = name switch
        {
            "Delay" => SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType),
            "Sleep" => SymbolEqualityComparer.Default.Equals(method.ContainingType, threadType),
            _ => SyntaxHelpers.IsInPlaywrightNamespace(method.ContainingType),
        };

        if (isFixedDelay)
        {
            var display = name == "WaitForTimeoutAsync" ? name : method.ContainingType.Name + "." + name;
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), display));
        }
    }
}
