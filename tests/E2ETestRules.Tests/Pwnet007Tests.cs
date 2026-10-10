using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet007Tests
{
    private static readonly AvoidAsyncVoidTestAnalyzer Analyzer = new();

    [Theory]
    [InlineData("using Xunit;", "[Fact]")]
    [InlineData("using Xunit;", "[Theory]")]
    public async Task Reports_async_void_test_methods(string usings, string attribute)
    {
        var source = $$"""
            {{usings}}
            using System.Threading.Tasks;
            class T { {{attribute}} public async void Works() { await Task.Yield(); } }
            """;

        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));

        Assert.Equal("PWNET007", d.Id);
        Assert.Contains("Works", d.GetMessage());
    }

    [Fact]
    public async Task Ignores_async_Task_tests_and_sync_void_tests()
    {
        const string source = """
            using Xunit;
            using System.Threading.Tasks;
            class T
            {
                [Fact] public async Task A() { await Task.Yield(); }
                [Fact] public void B() { }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_async_void_event_handlers()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            class Form { async void OnClick(object? sender, EventArgs e) { await Task.Yield(); } }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
