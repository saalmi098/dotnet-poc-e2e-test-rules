using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules
{
    /// <summary>PWNET005: Playwright <c>*Async</c> task-returning calls whose result is discarded.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AwaitPlaywrightAsyncCallsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PWNET005";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Await Playwright async calls",
            messageFormat: "Await Playwright asynchronous operations to avoid race conditions and unobserved tasks.",
            category: "Playwright E2E",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
        }

        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var name = SyntaxHelpers.GetInvokedName(invocation);
            if (name is null || !name.EndsWith("Async", StringComparison.Ordinal) || !IsDiscarded(invocation))
            {
                return;
            }

            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
                && IsTask(method.ReturnType)
                && method.ContainingType.ToDisplayString().IndexOf("Playwright", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
            }
        }

        // Bare "page.ClickAsync();" or explicit discard "_ = page.ClickAsync();".
        private static bool IsDiscarded(InvocationExpressionSyntax invocation) =>
            invocation.Parent switch
            {
                ExpressionStatementSyntax => true,
                AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" } } assignment =>
                    assignment.Parent is ExpressionStatementSyntax,
                _ => false,
            };

        private static bool IsTask(ITypeSymbol type) =>
            type is INamedTypeSymbol { Name: "Task" } named
            && named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
    }
}
