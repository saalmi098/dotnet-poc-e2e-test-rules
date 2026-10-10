using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET002 - CSS/XPath selectors passed to Playwright <c>Locator(...)</c>.
/// <para><b>Finds:</b> <c>Locator("...")</c> on <c>IPage</c>/<c>IFrame</c>/<c>ILocator</c>/<c>IFrameLocator</c>
/// with a constant selector starting with <c>css=</c>, <c>xpath=</c>, <c>//</c>, containing <c>#</c>, <c>.</c>, <c>[</c>,
/// <c>&gt;</c>, or using positional/structural parts (<c>:nth-child(</c>, <c>:nth-of-type(</c>, <c>:first-child</c>,
/// <c>:last-child</c>, <c>nth=</c>).</para>
/// <para><b>Why:</b> CSS/XPath selectors are brittle; prefer GetByRole, GetByTestId or GetByLabel.</para>
/// <para><b>Ignores:</b> <c>GetBy*</c> calls, plain tag selectors like <c>"button"</c>, non-constant selectors,
/// user-facing engines (<c>text=</c>, <c>role=</c>, <c>data-testid=</c>) and test-id attribute selectors such as
/// <c>[data-testid="save"]</c>, unless they are chained (<c>&gt;&gt;</c>) or positional.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferSemanticLocatorsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET002";

    private static readonly ImmutableHashSet<string> LocatorHosts =
        ["ILocator", "IPage", "IFrame", "IFrameLocator"];

    private static readonly char[] CssChars = ['#', '.', '[', '>'];

    private static readonly string[] StructuralParts =
        [":nth-child(", ":nth-last-child(", ":nth-of-type(", ":nth-last-of-type(", ":first-child", ":last-child", "nth="];

    private static readonly string[] UserFacingEngines =
        ["text=", "role=", "data-testid=", "data-test-id=", "data-test="];

    // [data-testid="save"], [data-test-id=save], [data-test='x'], [data-qa="x"]: an explicit test hook.
    private static readonly Regex TestIdAttributeSelector =
        new(@"^\[data-(testid|test-id|test|qa)=([""'][^""']*[""']|[\w-]+)\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Prefer semantic locators",
        messageFormat: "Prefer GetByRole, GetByTestId, or GetByLabel over brittle CSS/XPath selectors.",
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
            && method.ContainingNamespace.ToDisplayString() == SyntaxHelpers.PlaywrightNamespace)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation()));
        }
    }

    private static bool IsBrittle(string selector)
    {
        var trimmed = selector.Trim();
        if (StructuralParts.Any(part => trimmed.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return true;
        }

        if (trimmed.IndexOf(">>", StringComparison.Ordinal) < 0
            && (UserFacingEngines.Any(e => trimmed.StartsWith(e, StringComparison.OrdinalIgnoreCase))
                || TestIdAttributeSelector.IsMatch(trimmed)))
        {
            return false;
        }

        return trimmed.StartsWith("css=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("xpath=", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.IndexOfAny(CssChars) >= 0;
    }
}
