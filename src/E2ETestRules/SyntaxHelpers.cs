using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E2ETestRules
{
    internal static class SyntaxHelpers
    {
        /// <summary>Returns the simple name of the invoked method (right-most name), or null.</summary>
        public static string? GetInvokedName(InvocationExpressionSyntax invocation)
        {
            switch (invocation.Expression)
            {
                case IdentifierNameSyntax id:
                    return id.Identifier.ValueText;
                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText;
                case MemberAccessExpressionSyntax member:
                    return member.Name.Identifier.ValueText;
                case MemberBindingExpressionSyntax binding:
                    return binding.Name.Identifier.ValueText;
                default:
                    return null;
            }
        }

        private static readonly string[] TestAttributes = { "Fact", "Theory", "Test", "TestMethod" };

        /// <summary>True for [Fact], [Theory], [Test], [TestMethod] (matched by name, with or without "Attribute").</summary>
        public static bool IsTestAttribute(AttributeSyntax attribute)
        {
            var name = attribute.Name switch
            {
                QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax a => a.Name.Identifier.ValueText,
                SimpleNameSyntax s => s.Identifier.ValueText,
                _ => string.Empty,
            };

            if (name.EndsWith("Attribute", System.StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "Attribute".Length);
            }

            return System.Array.IndexOf(TestAttributes, name) >= 0;
        }

        public static string DocUri(string id) => "https://example.com/docs/e2e-test-rules/" + id;
    }
}
