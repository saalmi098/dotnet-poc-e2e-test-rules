using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E2ETestRules.Tests;

/// <summary>Lightweight in-memory compilation + analyzer runner.</summary>
internal static class AnalyzerTestHelper
{
    public const string TestPath = "/repo/tests/Sample.cs";
    public const string TestAssembly = "Sample.Tests";
    public const string NonTestPath = "/repo/src/Sample.cs";
    public const string NonTestAssembly = "Sample.App";

    // Minimal stand-ins so analyzers see the real metadata names without referencing Playwright.
    private const string Stubs = """
        using System.Threading.Tasks;

        namespace Microsoft.Playwright
        {
            public interface ILocator
            {
                ILocator Locator(string selector);
                ILocator GetByRole(string role);
                ILocator GetByTestId(string id);
                ILocator GetByLabel(string label);
                ILocator GetByText(string text);
                ILocator GetByPlaceholder(string text);
                ILocator Nth(int index);
                Task ClickAsync(LocatorClickOptions? options = null);
                Task<string?> TextContentAsync();
                Task<bool> IsVisibleAsync();
                Task<string> InputValueAsync();
                Task<string?> GetAttributeAsync(string name);
                Task<int> CountAsync();
                Task DispatchEventAsync(string type, object? eventInit = null);
                Task<object?> EvaluateAsync(string expression, object? arg = null);
            }

            public interface IFrame : ILocator { }

            public interface IPage : ILocator
            {
                Task GotoAsync(string url);
                Task WaitForTimeoutAsync(float timeout);
                Task<string> TitleAsync();
                string Url { get; }
            }

            public class LocatorClickOptions { public bool? Force { get; set; } }
            public class PlaywrightException : System.Exception { }
        }

        namespace SomeOther
        {
            public interface IPage { object Locator(string selector); Task ClickAsync(); }
        }

        namespace Microsoft.Playwright
        {
            public static class Assertions
            {
                public static ILocatorAssertions Expect(ILocator locator) => new LocatorAssertionsImpl();
            }
            public interface ILocatorAssertions { Task ToBeVisibleAsync(); }
            internal class LocatorAssertionsImpl : ILocatorAssertions { public Task ToBeVisibleAsync() => Task.CompletedTask; }
        }

        namespace Xunit
        {
            public class FactAttribute : System.Attribute { public string? Skip { get; set; } }
            public class TheoryAttribute : FactAttribute { }
            public static class Assert
            {
                public static void Equal(object? a, object? b) { }
                public static void True(bool b) { }
                public static void Fail(string message) { }
                public static void Skip(string reason) { }
                public static void NotNull(object? value) { }
            }
        }

        namespace Xunit.Sdk { public class XunitException : System.Exception { } }

        namespace OtherFramework { public class TestAttribute : System.Attribute { } }
        """;

    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string path = TestPath,
        string assemblyName = TestAssembly)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [
                CSharpSyntaxTree.ParseText(Stubs, options, path: "/stubs/Stubs.cs"),
                CSharpSyntaxTree.ParseText(source, options, path: path),
            ],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Count > 0)
        {
            throw new InvalidOperationException("Test source does not compile:\n" + string.Join("\n", compileErrors));
        }

        var all = await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync();

        var ids = analyzer.SupportedDiagnostics.Select(d => d.Id).ToHashSet();
        return all.Where(d => ids.Contains(d.Id)).ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return trusted
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Private.CoreLib.dll" or "System.Runtime.dll" or "System.Threading.Tasks.dll"
                or "System.Collections.dll" or "System.Linq.dll" or "netstandard.dll")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
    }
}
