using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET008 - one-shot browser-state reads checked with a non-retrying assertion.
/// <para><b>Finds:</b> xUnit <c>Assert.*</c> calls whose arguments read DOM/page state through Playwright: <c>IsVisibleAsync</c>,
/// <c>TextContentAsync</c>, <c>InputValueAsync</c>, <c>GetAttributeAsync</c>, <c>CountAsync</c>, <c>TitleAsync</c>,
/// <c>IPage.Url</c>, ... Directly, or through a local variable initialized from such a read.</para>
/// <para><b>Why:</b> the value is read once; if the page has not settled yet the test fails (or passes) by timing.
/// <c>Expect(locator).ToBeVisibleAsync()</c> etc. retry until the condition holds or time out.</para>
/// <para><b>Ignores:</b> assertions over non-browser data; values that reached the local through reassignment or
/// more than one hop; reads wrapped in page-object methods.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferWebFirstAssertionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET008";

    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["IsVisibleAsync"] = "ToBeVisibleAsync/ToBeHiddenAsync",
        ["IsHiddenAsync"] = "ToBeHiddenAsync",
        ["IsEnabledAsync"] = "ToBeEnabledAsync",
        ["IsDisabledAsync"] = "ToBeDisabledAsync",
        ["IsCheckedAsync"] = "ToBeCheckedAsync",
        ["IsEditableAsync"] = "ToBeEditableAsync",
        ["TextContentAsync"] = "ToHaveTextAsync/ToContainTextAsync",
        ["InnerTextAsync"] = "ToHaveTextAsync/ToContainTextAsync",
        ["AllTextContentsAsync"] = "ToHaveTextAsync",
        ["AllInnerTextsAsync"] = "ToHaveTextAsync",
        ["InputValueAsync"] = "ToHaveValueAsync",
        ["GetAttributeAsync"] = "ToHaveAttributeAsync",
        ["CountAsync"] = "ToHaveCountAsync",
        ["TitleAsync"] = "ToHaveTitleAsync",
        ["Url"] = "ToHaveURLAsync",
    };

    private static readonly ImmutableHashSet<string> StateHosts = ["ILocator", "IPage", "IFrame"];

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
            var read = FindStateRead(input, model, token) ?? FindStateReadThroughLocal(input, model, token);
            if (read != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), read, Replacements[read]));
                return;
            }
        }
    }

    /// <summary>Name of the first Playwright state read inside <paramref name="expression"/>, or null.</summary>
    private static string? FindStateRead(ExpressionSyntax expression, SemanticModel model, CancellationToken token)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            ISymbol? symbol = node switch
            {
                InvocationExpressionSyntax call when Replacements.ContainsKey(SyntaxHelpers.GetInvokedName(call) ?? string.Empty) =>
                    model.GetSymbolInfo(call, token).Symbol,
                MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Url" } access when access.Parent is not InvocationExpressionSyntax =>
                    model.GetSymbolInfo(access, token).Symbol,
                _ => null,
            };

            if (symbol is IMethodSymbol or IPropertySymbol
                && symbol.ContainingType != null
                && StateHosts.Contains(symbol.ContainingType.Name)
                && SyntaxHelpers.IsInPlaywrightNamespace(symbol.ContainingType))
            {
                return symbol.Name;
            }
        }

        return null;
    }

    // var text = await loc.TextContentAsync(); Assert.Equal("x", text);
    private static string? FindStateReadThroughLocal(ExpressionSyntax expression, SemanticModel model, CancellationToken token)
    {
        if (expression is not IdentifierNameSyntax identifier
            || model.GetSymbolInfo(identifier, token).Symbol is not ILocalSymbol local)
        {
            return null;
        }

        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(token) is VariableDeclaratorSyntax { Initializer.Value: var value })
            {
                return FindStateRead(value, model, token);
            }
        }

        return null;
    }
}
