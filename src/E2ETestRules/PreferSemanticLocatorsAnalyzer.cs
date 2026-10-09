using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules
{
    /// <summary>PWNET002: CSS/XPath selectors passed to Playwright <c>Locator(...)</c>.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PreferSemanticLocatorsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PWNET002";

        private const string PlaywrightNamespace = "Microsoft.Playwright";

        private static readonly ImmutableHashSet<string> LocatorHosts =
            ImmutableHashSet.Create("ILocator", "IPage", "IFrame", "IFrameLocator");

        private static readonly char[] CssChars = { '#', '.', '[', '>' };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Prefer semantic locators",
            messageFormat: "Prefer GetByRole, GetByTestId, or GetByLabel over brittle CSS/XPath selectors.",
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
            if (SyntaxHelpers.GetInvokedName(invocation) != "Locator" || invocation.ArgumentList.Arguments.Count == 0)
            {
                return;
            }

            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (context.SemanticModel.GetConstantValue(argument, context.CancellationToken).Value is not string selector
                || !IsBrittle(selector))
            {
                return;
            }

            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
                && LocatorHosts.Contains(method.ContainingType.Name)
                && method.ContainingNamespace.ToDisplayString() == PlaywrightNamespace)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
            }
        }

        private static bool IsBrittle(string selector)
        {
            var trimmed = selector.TrimStart();
            return trimmed.StartsWith("css=", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("xpath=", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.IndexOfAny(CssChars) >= 0;
        }
    }
}
