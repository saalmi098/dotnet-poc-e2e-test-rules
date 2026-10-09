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

        public static string DocUri(string id) => "https://example.com/docs/e2e-test-rules/" + id;
    }
}
