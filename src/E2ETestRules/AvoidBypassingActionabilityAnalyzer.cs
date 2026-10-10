using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET009 - interactions that bypass Playwright's actionability checks or real user input.
/// <para><b>Finds:</b> <c>Force = true</c> on a Playwright options type (<c>LocatorClickOptions</c>, ...);
/// <c>DispatchEventAsync(...)</c>; <c>EvaluateAsync</c>/<c>EvalOnSelector*Async</c> with a constant script that calls
/// <c>.click()</c> or <c>dispatchEvent(</c>.</para>
/// <para><b>Why:</b> these skip the visible/enabled/stable/not-covered checks, so the test can pass while a real user
/// could not click the element (overlay, disabled button, element off-screen). Often added to silence a flaky test.</para>
/// <para><b>Ignores:</b> <c>Force = false</c>, non-constant scripts, scripts that only read state. Review warning:
/// suppress with a justification where the bypass is intended.</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidBypassingActionabilityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET009";

    private static readonly ImmutableHashSet<string> EvaluateMethods =
        ["EvaluateAsync", "EvaluateHandleAsync", "EvalOnSelectorAsync", "EvalOnSelectorAllAsync"];

    private static readonly string[] SyntheticInteractions = [".click()", "dispatchevent("];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Avoid bypassing actionability checks",
        messageFormat: "{0} bypasses Playwright's actionability checks. Fix the cause (overlay, state, timing) or justify with a suppression.",
        category: "Playwright E2E",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    // new() { Force = true } and options.Force = true
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var left = assignment.Left switch
        {
            IdentifierNameSyntax id => id,
            MemberAccessExpressionSyntax member => member.Name,
            _ => null,
        };

        if (left?.Identifier.ValueText != "Force"
            || context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken).Value is not true
            || context.SemanticModel.GetSymbolInfo(left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !SyntaxHelpers.IsInPlaywrightNamespace(property.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, assignment.GetLocation(), "Force = true"));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var name = SyntaxHelpers.GetInvokedName(invocation);
        if (name != "DispatchEventAsync" && !(name != null && EvaluateMethods.Contains(name) && HasSyntheticInteractionScript(context, invocation)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && SyntaxHelpers.IsInPlaywrightNamespace(method.ContainingType))
        {
            var what = name == "DispatchEventAsync" ? "DispatchEventAsync" : name + " with a scripted click/event";
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), what));
        }
    }

    private static bool HasSyntheticInteractionScript(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken).Value is string script)
            {
                foreach (var pattern in SyntheticInteractions)
                {
                    if (script.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
