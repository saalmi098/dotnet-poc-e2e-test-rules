using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E2ETestRules;

/// <summary>
/// One-shot Playwright reads of DOM/page state (<c>IsVisibleAsync</c>, <c>TextContentAsync</c>, <c>IPage.Url</c>, ...)
/// that return immediately instead of waiting. Shared by PWNET008 and PWNET011.
/// </summary>
internal static class BrowserStateReads
{
    /// <summary>Read name → retrying web-first assertion to use instead.</summary>
    public static readonly IReadOnlyDictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal)
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

    /// <summary>
    /// Name of the first state read inside <paramref name="expression"/>, or inside the initializer of a local it
    /// names (<c>var text = await loc.TextContentAsync(); ... text ...</c>); null if none.
    /// </summary>
    public static string? Find(ExpressionSyntax expression, SemanticModel model, CancellationToken token) =>
        FindDirect(expression, model, token) ?? FindThroughLocal(expression, model, token);

    private static string? FindDirect(ExpressionSyntax expression, SemanticModel model, CancellationToken token)
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

    // One hop only: a local whose declaration initializer contains a read. Later reassignments are not tracked.
    private static string? FindThroughLocal(ExpressionSyntax expression, SemanticModel model, CancellationToken token)
    {
        foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (model.GetSymbolInfo(identifier, token).Symbol is not ILocalSymbol local)
            {
                continue;
            }

            foreach (var reference in local.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(token) is VariableDeclaratorSyntax { Initializer.Value: var value }
                    && FindDirect(value, model, token) is { } read)
                {
                    return read;
                }
            }
        }

        return null;
    }
}
