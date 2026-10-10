using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET005 - Playwright <c>*Async</c> task-returning calls whose result is discarded or blocked on.
/// <para><b>Finds:</b> <c>*Async</c> methods returning <c>Task</c>/<c>Task&lt;T&gt;</c>, declared on a type whose name or
/// namespace contains <c>Playwright</c>, when the result is dropped (bare statement or <c>_ = ...</c>) or blocked on
/// directly (<c>.Wait()</c>, <c>.Result</c>, <c>.GetAwaiter().GetResult()</c>).</para>
/// <para><b>Why:</b> un-awaited calls cause race conditions and unobserved exceptions; blocking on them risks
/// deadlocks and thread-pool starvation in test runners.</para>
/// <para><b>Ignores:</b> awaited, stored, returned or passed-on tasks; non-Playwright async methods.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Pwnet005_AwaitPlaywrightAsyncCallsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Await Playwright async calls",
        messageFormat: "Await Playwright asynchronous operations to avoid race conditions and unobserved tasks.",
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
        var name = SyntaxHelpers.GetInvokedName(invocation);
        if (name is null || !name.EndsWith("Async", StringComparison.Ordinal) || !(IsDiscarded(invocation) || IsBlockedOn(invocation)))
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

    // "page.GotoAsync(url).Wait()", ".Result", ".GetAwaiter().GetResult()" directly on the call.
    private static bool IsBlockedOn(InvocationExpressionSyntax invocation)
    {
        if (invocation.Parent is not MemberAccessExpressionSyntax member || member.Expression != invocation)
        {
            return false;
        }

        return member.Name.Identifier.ValueText switch
        {
            "Result" => true,
            "Wait" => member.Parent is InvocationExpressionSyntax,
            "GetAwaiter" => member.Parent is InvocationExpressionSyntax getAwaiter
                                && getAwaiter.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetResult" } getResult
                                && getResult.Parent is InvocationExpressionSyntax,
            _ => false,
        };
    }

    private static bool IsTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task" } named
        && named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";
}
