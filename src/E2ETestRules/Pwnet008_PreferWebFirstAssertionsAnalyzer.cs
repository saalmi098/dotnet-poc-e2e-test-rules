using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET008 - one-shot browser-state reads checked with a non-retrying assertion.
/// <para><b>Finds:</b> xUnit <c>Assert.*</c> calls whose arguments read DOM/page state through Playwright: <c>IsVisibleAsync</c>,
/// <c>TextContentAsync</c>, <c>InputValueAsync</c>, <c>GetAttributeAsync</c>, <c>CountAsync</c>, <c>TitleAsync</c>,
/// <c>IPage.Url</c>, ... Directly, or through a local variable initialized from such a read (see <see cref="BrowserStateReads"/>).</para>
/// <para><b>Why:</b> the value is read once; if the page has not settled yet the test fails (or passes) by timing.
/// <c>Expect(locator).ToBeVisibleAsync()</c> etc. retry until the condition holds or time out.</para>
/// <para><b>Ignores:</b> assertions over non-browser data; values that reached the local through reassignment or
/// more than one hop; reads wrapped in page-object methods.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet008_PreferWebFirstAssertionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET008";

    // Only xUnit v3 is supported.
    private const string XunitAssert = "Xunit.Assert";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Prefer web-first assertions over one-shot browser-state reads",
        messageFormat: "Assertion checks browser state read once via {0}, which does not retry. Use Expect(...).{1} instead.",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var token = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
            || method.ContainingType?.ToDisplayString() != XunitAssert)
        {
            return;
        }

        foreach (var input in invocation.ArgumentList.Arguments.Select(a => a.Expression))
        {
            var read = BrowserStateReads.Find(input, model, token);
            if (read != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), read, BrowserStateReads.Replacements[read]));
                return;
            }
        }
    }
}
