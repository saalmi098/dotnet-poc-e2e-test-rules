using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules;

/// <summary>
/// PWNET006 - Playwright API calls inside test classes (should live in page objects).
/// <para><b>Finds:</b> calls to any type in <c>Microsoft.Playwright.*</c> inside a class that declares a test method
/// (including its helpers and lambdas).</para>
/// <para><b>Why:</b> keeps locators and browser interaction in page objects so tests read as scenarios.</para>
/// <para><b>Ignores:</b> <c>Expect(...)</c> and <c>*Assertions</c> types; classes without test methods (page objects).</para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsePageObjectsInTestsAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PWNET006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Use page objects in tests",
        messageFormat: "Do not call Playwright APIs directly in test classes. Move the interaction into a page object.",
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

        // Cheap syntactic filter first: only classes that declare a test method.
        var type = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (type is null || !HasTestMethod(type))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType;
        if (containingType is null || !SyntaxHelpers.IsInPlaywrightNamespace(containingType))
        {
            return;
        }

        // Expect(...) and the ToBe*/ToHave* assertion types (ILocatorAssertions, ...) are allowed in tests.
        if (containingType.Name.EndsWith("Assertions", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool HasTestMethod(TypeDeclarationSyntax type) =>
        type.Members.OfType<MethodDeclarationSyntax>().Any(SyntaxHelpers.IsTestMethod);
}
