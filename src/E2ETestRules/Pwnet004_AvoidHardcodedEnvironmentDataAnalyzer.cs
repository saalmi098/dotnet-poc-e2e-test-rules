using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules
{
    /// <summary>
    /// PWNET004 - hardcoded non-local URLs and credential-like names/literals in test code.
    /// <para><b>Finds:</b> in test code, string literals with an <c>http(s)://</c> URL whose host is not loopback, and
    /// variable/parameter/property names or string literals containing <c>password</c>, <c>passwd</c>, <c>secret</c>,
    /// <c>token</c> or <c>apiKey</c> (case-insensitive).</para>
    /// <para><b>Why:</b> environment URLs and credentials belong in configuration or user secrets.</para>
    /// <para><b>Ignores:</b> <c>localhost</c>/<c>127.0.0.1</c>, scheme-only interpolation like <c>$"https://{host}"</c>, non-test code.</para>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class Pwnet004_AvoidHardcodedEnvironmentDataAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PWNET004";

        private static readonly string[] SensitiveWords = { "password", "passwd", "secret", "token", "apikey" };
        private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1" };
        private static readonly char[] HostTerminators = { '/', '?', '#', ':', '\\', '"', '\'', ' ' };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            title: "Avoid hardcoded environment data",
            messageFormat: "Move environment-specific URLs and credentials to configuration or user secrets.",
            category: "Playwright E2E",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: SyntaxHelpers.DocUri(DiagnosticId));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression, SyntaxKind.InterpolatedStringText);
            context.RegisterSyntaxNodeAction(
                AnalyzeDeclaration,
                SyntaxKind.VariableDeclarator,
                SyntaxKind.Parameter,
                SyntaxKind.PropertyDeclaration);
        }

        private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
        {
            var token = context.Node switch
            {
                LiteralExpressionSyntax literal => literal.Token,
                InterpolatedStringTextSyntax text => text.TextToken,
                _ => default,
            };

            var value = token.ValueText;
            if (string.IsNullOrEmpty(value) || !IsTestCode(context))
            {
                return;
            }

            if (ContainsRemoteUrl(value) || ContainsSensitiveWord(value))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
            }
        }

        private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
        {
            var identifier = context.Node switch
            {
                VariableDeclaratorSyntax v => v.Identifier,
                ParameterSyntax p => p.Identifier,
                PropertyDeclarationSyntax prop => prop.Identifier,
                _ => default,
            };

            if (!identifier.IsKind(SyntaxKind.IdentifierToken) || !ContainsSensitiveWord(identifier.ValueText) || !IsTestCode(context))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation()));
        }

        private static bool IsTestCode(SyntaxNodeAnalysisContext context) =>
            TestCodeDetector.IsTestCode(context.Node.SyntaxTree, context.Compilation);

        private static bool ContainsSensitiveWord(string text)
        {
            foreach (var word in SensitiveWords)
            {
                if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsRemoteUrl(string text)
        {
            var start = 0;
            while (start < text.Length)
            {
                var index = text.IndexOf("://", start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return false;
                }

                start = index + 3;
                var isHttp = EndsWithScheme(text, index, "https") || EndsWithScheme(text, index, "http");
                if (!isHttp)
                {
                    continue;
                }

                var hostEnd = text.IndexOfAny(HostTerminators, start);
                var host = hostEnd < 0 ? text.Substring(start) : text.Substring(start, hostEnd - start);

                // "https://{baseHost}/x": nothing hardcoded after the scheme.
                if (host.Length == 0)
                {
                    continue;
                }

                if (Array.IndexOf(LoopbackHosts, host.ToLowerInvariant()) < 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EndsWithScheme(string text, int schemeEnd, string scheme)
        {
            var begin = schemeEnd - scheme.Length;
            return begin >= 0 && string.Compare(text, begin, scheme, 0, scheme.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }
    }
}
