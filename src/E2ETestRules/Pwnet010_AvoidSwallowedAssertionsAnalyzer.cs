using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET010 - assertion failures swallowed by a catch block in a test method.
/// <para><b>Finds:</b> in a test method, a <c>try</c> whose block contains an assertion (<c>Expect*</c>/<c>Assert*</c>/
/// <c>Should*</c>, excluding <c>Fail</c>) and a broad <c>catch</c> (no type, <c>Exception</c>, <c>SystemException</c>,
/// <c>TimeoutException</c>, Playwright exceptions, or xUnit's <c>XunitException</c>) that neither rethrows
/// nor asserts.</para>
/// <para><b>Why:</b> the assertion can never fail the test, so the test passes when the app is broken.</para>
/// <para><b>Ignores:</b> expected-exception patterns (<c>try { act; Assert.Fail(); } catch (X) { }</c>), catches with a
/// <c>when</c> filter, narrow exception types, catches that rethrow or assert, code outside test methods.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet010_AvoidSwallowedAssertionsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET010";

    private static readonly ImmutableHashSet<string> BroadSystemTypes =
        ["System.Exception", "System.SystemException", "System.TimeoutException"];

    // Base type of all exceptions thrown by failed xUnit assertions.
    private const string XunitException = "Xunit.Sdk.XunitException";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Do not swallow assertion failures",
        messageFormat: "This catch block swallows failures of the assertion in its try block, so the test cannot fail. Rethrow, assert, or remove the try/catch.",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Filter != null
            || catchClause.Parent is not TryStatementSyntax tryStatement
            || SyntaxHelpers.GetContainingTestMethod(catchClause) is null)
        {
            return;
        }

        if (!tryStatement.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsNonFailAssertion)
            || RethrowsOrAsserts(catchClause.Block)
            || !IsBroad(context, catchClause))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, catchClause.CatchKeyword.GetLocation()));
    }

    private static bool IsNonFailAssertion(InvocationExpressionSyntax invocation) =>
        SyntaxHelpers.IsAssertionInvocation(invocation) && SyntaxHelpers.GetInvokedName(invocation) != "Fail";

    private static bool RethrowsOrAsserts(BlockSyntax block) =>
        block.DescendantNodes().Any(n =>
            n is ThrowStatementSyntax or ThrowExpressionSyntax
            || (n is InvocationExpressionSyntax call && SyntaxHelpers.IsAssertionInvocation(call)));

    private static bool IsBroad(SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration is null)
        {
            return true;
        }

        if (context.SemanticModel.GetTypeInfo(catchClause.Declaration.Type, context.CancellationToken).Type is not INamedTypeSymbol type)
        {
            return false;
        }

        return BroadSystemTypes.Contains(type.ToDisplayString())
            || SyntaxHelpers.IsInPlaywrightNamespace(type)
            || type.ToDisplayString() == XunitException;
    }
}
