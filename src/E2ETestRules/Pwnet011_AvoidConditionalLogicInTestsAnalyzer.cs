using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET011 - conditional logic in tests, and branches that depend on a one-shot browser-state read.
/// <para><b>Finds:</b> (1) <c>[Fact]</c>/<c>[Theory]</c> methods with any branch (<c>if</c>, <c>switch</c>,
/// <c>?:</c>, <c>for</c>/<c>foreach</c>/<c>while</c>/<c>do</c>; reported once per test).
/// (2) Anywhere, including page objects and helpers: an <c>if</c>/<c>switch</c>/<c>?:</c>/loop condition that reads
/// browser state once (<c>if (await banner.IsVisibleAsync())</c>), directly or through a local.</para>
/// <para><b>Why:</b> (1) a test with branches hides several scenarios and does not always run the same checks; split it
/// or use <c>[Theory]</c>. (2) the read returns immediately, so which path runs depends on timing.</para>
/// <para><b>Ignores:</b> <c>??</c>, <c>&amp;&amp;</c>, <c>||</c>, <c>?.</c> (value fallbacks, not separate scenarios); branches
/// in page objects/helpers that do not read browser state. A state-dependent branch in a test is reported only by (2).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet011_AvoidConditionalLogicInTestsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET011";

    public const int MaxBranches = 0;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Avoid conditional logic in tests",
        messageFormat: "{0}",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    private static readonly SyntaxKind[] BranchKinds =
    [
        SyntaxKind.IfStatement,
        SyntaxKind.SwitchStatement,
        SyntaxKind.SwitchExpression,
        SyntaxKind.ConditionalExpression,
        SyntaxKind.ForStatement,
        SyntaxKind.ForEachStatement,
        SyntaxKind.ForEachVariableStatement,
        SyntaxKind.WhileStatement,
        SyntaxKind.DoStatement,
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBranch, BranchKinds);
        context.RegisterSyntaxNodeAction(AnalyzeTestMethod, SyntaxKind.MethodDeclaration);
    }

    // (2) State-dependent branch, in any class.
    private static void AnalyzeBranch(SyntaxNodeAnalysisContext context)
    {
        var condition = GetCondition(context.Node);
        if (condition is null || BrowserStateReads.Find(condition, context.SemanticModel, context.CancellationToken) is not { } read)
        {
            return;
        }

        var message =
            $"Branch depends on browser state read once via {read}, so the path taken depends on timing. "
            + "Wait for the expected state with Expect(...) or handle optional overlays with Page.AddLocatorHandlerAsync.";
        context.ReportDiagnostic(Diagnostic.Create(Rule, condition.GetLocation(), message));
    }

    // (1) Branch count per test method.
    private static void AnalyzeTestMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null || !SyntaxHelpers.IsTestMethod(method))
        {
            return;
        }

        var branches = body.DescendantNodes()
            .Where(n => BranchKinds.Contains(n.Kind()))
            .Count(n => GetCondition(n) is not { } c || BrowserStateReads.Find(c, context.SemanticModel, context.CancellationToken) is null);

        if (branches > MaxBranches)
        {
            var message =
                $"Test '{method.Identifier.ValueText}' contains {branches} branch(es) or loop(s). "
                + "Conditional logic hides separate scenarios; split it into separate tests or use [Theory] with [InlineData].";
            context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), message));
        }
    }

    /// <summary>The expression that decides which path runs; null for <c>foreach</c> (iterates, does not decide).</summary>
    private static ExpressionSyntax? GetCondition(SyntaxNode branch) =>
        branch switch
        {
            IfStatementSyntax s => s.Condition,
            SwitchStatementSyntax s => s.Expression,
            SwitchExpressionSyntax s => s.GoverningExpression,
            ConditionalExpressionSyntax s => s.Condition,
            ForStatementSyntax s => s.Condition,
            WhileStatementSyntax s => s.Condition,
            DoStatementSyntax s => s.Condition,
            _ => null,
        };
}
