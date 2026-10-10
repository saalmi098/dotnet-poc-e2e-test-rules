using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet001Tests
{
    private static readonly AvoidDelayInE2ETestsAnalyzer Analyzer = new();

    private const string DelayCall = """
        using System.Threading.Tasks;
        class T { async Task M() { await Task.Delay(100); } }
        """;

    [Fact]
    public async Task Reports_Task_Delay_in_test_path()
    {
        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, DelayCall);

        var d = Assert.Single(diagnostics);
        Assert.Equal("PWNET001", d.Id);
    }

    [Fact]
    public async Task Ignores_Task_Delay_in_non_test_code()
    {
        var diagnostics = await AnalyzerTestHelper.RunAsync(
            Analyzer, DelayCall, AnalyzerTestHelper.NonTestPath, AnalyzerTestHelper.NonTestAssembly);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Reports_by_assembly_name_suffix_and_windows_e2e_path()
    {
        var byAssembly = await AnalyzerTestHelper.RunAsync(
            Analyzer, DelayCall, "/repo/src/X.cs", "My.App.Tests");
        var byWindowsPath = await AnalyzerTestHelper.RunAsync(
            Analyzer, DelayCall, @"C:\repo\E2E\X.cs", AnalyzerTestHelper.NonTestAssembly);

        Assert.Single(byAssembly);
        Assert.Single(byWindowsPath);
    }

    [Fact]
    public async Task Ignores_unrelated_Delay_method()
    {
        const string source = """
            class Clock { public void Delay(int ms) { } }
            class T { void M() { new Clock().Delay(5); } }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Theory]
    [InlineData("System.Threading.Thread.Sleep(500);", "Thread.Sleep")]
    [InlineData("await page.WaitForTimeoutAsync(500);", "WaitForTimeoutAsync")]
    [InlineData("await Task.Delay(500);", "Task.Delay")]
    public async Task Reports_other_fixed_delays_and_names_them(string body, string expectedName)
    {
        var source = $$"""
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            class T { async Task M(IPage page) { {{body}} } }
            """;

        var d = Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, source));

        Assert.Contains(expectedName, d.GetMessage());
    }

    [Fact]
    public async Task Ignores_condition_based_waits_and_unrelated_Sleep()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using static Microsoft.Playwright.Assertions;
            class Robot { public void Sleep(int ms) { } public Task WaitForTimeoutAsync(int ms) => Task.CompletedTask; }
            class T
            {
                async Task M(IPage page, Robot r)
                {
                    await Expect(page.GetByRole("button")).ToBeVisibleAsync();
                    r.Sleep(5);
                    await r.WaitForTimeoutAsync(5);
                }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
