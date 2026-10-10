using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet010Tests
{
    private static readonly Pwnet010_AvoidSwallowedAssertionsAnalyzer Analyzer = new();

    private static string Wrap(string body) => $$"""
        using System;
        using System.Threading.Tasks;
        using Microsoft.Playwright;
        using Xunit;
        using static Microsoft.Playwright.Assertions;
        class T { [Fact] public async Task M(ILocator loc) { {{body}} } static void Log(Exception e) { } }
        """;

    [Theory]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch { }")]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (Exception ex) { Log(ex); }")]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (PlaywrightException) { }")]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (TimeoutException) { return; }")]
    [InlineData("try { Assert.True(false); } catch (Xunit.Sdk.XunitException) { }")]
    public async Task Reports_swallowed_assertion_failures(string body)
    {
        Assert.Equal("PWNET010", Assert.Single(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body))).Id);
    }

    [Theory]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (Exception) { throw; }")]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (Exception ex) { Assert.Fail(ex.Message); }")]
    [InlineData("try { await loc.ClickAsync(); Assert.Fail(\"should throw\"); } catch (PlaywrightException) { }")]
    [InlineData("try { Assert.True(false); } catch (ArgumentException) { }")]
    [InlineData("try { await Expect(loc).ToBeVisibleAsync(); } catch (Exception ex) when (ex.Message.Contains(\"detached\")) { }")]
    [InlineData("try { await loc.ClickAsync(); } catch (PlaywrightException) { }")]
    public async Task Ignores_rethrow_expected_exception_narrow_and_filtered_catches(string body)
    {
        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, Wrap(body)));
    }

    [Fact]
    public async Task Edge_ignores_catches_outside_test_methods()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Playwright;
            using static Microsoft.Playwright.Assertions;
            class CookieBanner
            {
                public async Task DismissIfShownAsync(ILocator banner)
                {
                    try { await Expect(banner).ToBeVisibleAsync(); await banner.ClickAsync(); } catch { }
                }
            }
            """;

        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }
}
