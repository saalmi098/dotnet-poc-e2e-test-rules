using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules
{
    /// <summary>
    /// PWNET001 - PWNET001: Task.Delay in test code.
    /// <para><b>Finds:</b> calls to <c>System.Threading.Tasks.Task.Delay</c> in test code (path contains
    /// <c>/e2e/</c> or <c>/tests/</c>, or assembly name ends with <c>.Tests</c>).</para>
    /// <para><b>Why:</b> fixed sleeps make tests slow and flaky; use Playwright auto-waiting or web-first assertions.</para>
    /// <para><b>Ignores:</b> <c>Delay</c> methods on other types; non-test code.</para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AvoidTaskDelayInE2ETestsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PWNET001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Avoid Task.Delay in E2E tests",
            messageFormat: "Avoid Task.Delay in Playwright E2E tests. Use Playwright auto-waiting or web-first assertions instead.",
            category: "Playwright E2E",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var taskType = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                if (taskType is null)
                {
                    return;
                }

                start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, taskType), SyntaxKind.InvocationExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol taskType)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (SyntaxHelpers.GetInvokedName(invocation) != "Delay")
            {
                return;
            }

            if (!TestCodeDetector.IsTestCode(invocation.SyntaxTree, context.Compilation))
            {
                return;
            }

            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, taskType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }
    }
}
