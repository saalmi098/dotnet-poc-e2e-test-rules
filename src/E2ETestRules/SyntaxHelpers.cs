using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E2ETestRules;

internal static class SyntaxHelpers
{
    public const string PlaywrightNamespace = "Microsoft.Playwright";

    /// <summary>Returns the simple name of the invoked method (right-most name), or null.</summary>
    public static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => null,
        };
    }

    // Only xUnit v3 is supported.
    private static readonly string[] TestAttributes = ["Fact", "Theory"];

    /// <summary>True for xUnit <c>[Fact]</c>/<c>[Theory]</c> (matched by name, with or without "Attribute").</summary>
    public static bool IsTestAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name switch
        {
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax a => a.Name.Identifier.ValueText,
            SimpleNameSyntax s => s.Identifier.ValueText,
            _ => string.Empty,
        };

        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Attribute".Length);
        }

        return Array.IndexOf(TestAttributes, name) >= 0;
    }

    public static bool IsTestMethod(MethodDeclarationSyntax method) =>
        method.AttributeLists.SelectMany(l => l.Attributes).Any(IsTestAttribute);

    /// <summary>The test method that contains <paramref name="node"/> (including lambdas inside it), or null.</summary>
    public static MethodDeclarationSyntax? GetContainingTestMethod(SyntaxNode node)
    {
        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        return method != null && IsTestMethod(method) ? method : null;
    }

    private static readonly string[] AssertionPrefixes = ["Expect", "Assert", "Should"];

    /// <summary>
    /// Name-based assertion heuristic shared by PWNET003 and PWNET010: <c>Expect*</c>/<c>Assert*</c>/<c>Should*</c>
    /// calls, or calls on an <c>Assert*</c>/<c>Should*</c> receiver such as <c>Assert.Equal</c>.
    /// </summary>
    public static bool IsAssertionInvocation(InvocationExpressionSyntax invocation)
    {
        var name = GetInvokedName(invocation);
        if (name != null && StartsWithAssertionPrefix(name))
        {
            return true;
        }

        // Assert.Equal(...), Assert.That(...): the receiver carries the assertion intent.
        return invocation.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver }
            && StartsWithAssertionPrefix(receiver.Identifier.ValueText);
    }

    private static bool StartsWithAssertionPrefix(string name) =>
        AssertionPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>True when the symbol is declared in <c>Microsoft.Playwright</c> or a sub-namespace.</summary>
    public static bool IsInPlaywrightNamespace(ISymbol? symbol)
    {
        var name = symbol?.ContainingNamespace?.ToDisplayString();
        return name == PlaywrightNamespace
            || (name != null && name.StartsWith(PlaywrightNamespace + ".", StringComparison.Ordinal));
    }

    public static string DocUri(string id) => "https://example.com/docs/e2e-test-rules/" + id;
}
