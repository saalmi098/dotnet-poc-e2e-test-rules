using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet003Tests
{
    private static readonly RequireAssertionInTestAnalyzer Analyzer = new();

    [Theory]
    [InlineData("using Xunit;", "[Fact]")]
    [InlineData("using Xunit;", "[Theory]")]
    [InlineData("using NUnit.Framework;", "[Test]")]
    [InlineData("using Microsoft.VisualStudio.TestTools.UnitTesting;", "[TestMethod]")]
    public async Task Reports_test_without_assertion(string usings, string attribute)
    {
        var source = $$"""
            {{usings}}
            class T { {{attribute}} public void Works() { var x = 1; } }
            """;

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.Equal("PWNET003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Ignores_test_with_assertions()
    {
        const string source = """
            using Xunit;
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using static Microsoft.Playwright.Assertions;
            class T
            {
                [Fact] public void A() { Assert.True(true); }
                [Fact] public async Task B(ILocator l) { await Expect(l).ToBeVisibleAsync(); }
                [Fact] public void C() { ShouldBeFine(); }
                void ShouldBeFine() { }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_non_test_methods()
    {
        const string source = "class T { public void NotATest() { var x = 1; } }";

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_empty_skipped_and_expression_bodied_assertion()
    {
        const string source = """
            using Xunit;
            class T
            {
                [Fact] public void Empty() { }
                [Fact(Skip = "later")] public void Skipped() { var x = 1; }
                [Fact] public void SkipByName_SkipMe() { var x = 1; }
                [Fact] public void Expr() => Assert.True(true);
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
