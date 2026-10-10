using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET003 - test methods without any assertion-like invocation.
/// <para><b>Finds:</b> xUnit test methods (<c>[Fact]</c>/<c>[Theory]</c>) whose body, including an empty body, has no
/// invocation named <c>Expect*</c>/<c>Assert*</c>/<c>Should*</c> (or called on an <c>Assert*</c>/<c>Should*</c> receiver).</para>
/// <para><b>Why:</b> a test that never asserts passes even when the app is broken.</para>
/// <para><b>Ignores:</b> skipped tests (<c>Skip</c> in the name, attribute argument or a called method such as <c>Assert.Skip</c>).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet003_RequireAssertionInTestAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Require assertion in test",
        messageFormat: "E2E test contains no meaningful assertion.",
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

        var testAttributes = method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(SyntaxHelpers.IsTestAttribute)
            .ToList();
        if (testAttributes.Count == 0)
        {
            return;
        }

        // Empty bodies are reported too: an empty test always passes. Use Skip for intentional placeholders.
        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        if (method.Identifier.ValueText.IndexOf("Skip", StringComparison.Ordinal) >= 0
            || testAttributes.Any(HasSkipArgument)
            || body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsSkipInvocation))
        {
            return;
        }

        if (body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(SyntaxHelpers.IsAssertionInvocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation()));
    }

    private static bool HasSkipArgument(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(a =>
            a.NameEquals?.Name.Identifier.ValueText == "Skip"
            || a.NameColon?.Name.Identifier.ValueText == "Skip") == true;

    private static bool IsSkipInvocation(InvocationExpressionSyntax invocation)
    {
        var name = SyntaxHelpers.GetInvokedName(invocation);
        return name != null && name.IndexOf("Skip", StringComparison.Ordinal) >= 0;
    }
}
