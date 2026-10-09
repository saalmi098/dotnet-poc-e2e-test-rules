using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules
{
    /// <summary>PWNET003: test methods without any assertion-like invocation.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RequireAssertionInTestAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PWNET003";

        private static readonly string[] TestAttributes = { "Fact", "Theory", "Test", "TestMethod" };
        private static readonly string[] AssertionPrefixes = { "Expect", "Assert", "Should" };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Require assertion in test",
            messageFormat: "E2E test contains no meaningful assertion.",
            category: "Playwright E2E",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
                .Where(IsTestAttribute)
                .ToList();
            if (testAttributes.Count == 0)
            {
                return;
            }

            SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body is null || (method.Body is { Statements.Count: 0 }))
            {
                return;
            }

            if (method.Identifier.ValueText.IndexOf("Skip", StringComparison.Ordinal) >= 0
                || testAttributes.Any(HasSkipArgument)
                || body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsSkipInvocation))
            {
                return;
            }

            if (body.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsAssertionInvocation))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation()));
        }

        private static bool IsTestAttribute(AttributeSyntax attribute)
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

        private static bool HasSkipArgument(AttributeSyntax attribute) =>
            attribute.ArgumentList?.Arguments.Any(a =>
                a.NameEquals?.Name.Identifier.ValueText == "Skip"
                || a.NameColon?.Name.Identifier.ValueText == "Skip") == true;

        private static bool IsSkipInvocation(InvocationExpressionSyntax invocation)
        {
            var name = SyntaxHelpers.GetInvokedName(invocation);
            return name != null && name.IndexOf("Skip", StringComparison.Ordinal) >= 0;
        }

        private static bool IsAssertionInvocation(InvocationExpressionSyntax invocation)
        {
            var name = SyntaxHelpers.GetInvokedName(invocation);
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
    }
}
