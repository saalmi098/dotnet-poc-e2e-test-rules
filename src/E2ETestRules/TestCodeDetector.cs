using System;
using Microsoft.CodeAnalysis;

namespace E2ETestRules
{
    /// <summary>Decides whether a syntax tree belongs to test code (shared by PWNET001 and PWNET004).</summary>
    internal static class TestCodeDetector
    {
        private static readonly string[] PathMarkers = { "/e2e/", "/tests/" };

        public static bool IsTestCode(SyntaxTree tree, Compilation compilation)
        {
            var assemblyName = compilation.AssemblyName;
            if (assemblyName != null && assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var path = tree.FilePath;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Normalize separators and anchor so relative paths like "tests/Foo.cs" also match.
            var normalized = "/" + path.Replace('\\', '/');
            foreach (var marker in PathMarkers)
            {
                if (normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
