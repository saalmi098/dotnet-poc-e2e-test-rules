using System.Threading.Tasks;
using Xunit;

namespace E2ETestRules.Tests;

public class Pwnet004Tests
{
    private static readonly AvoidHardcodedEnvironmentDataAnalyzer Analyzer = new();

    [Theory]
    [InlineData("""class T { string Url = "https://staging.example.com/login"; }""")]
    [InlineData("""class T { string Url = "http://example.com"; }""")]
    [InlineData("""class T { string password = "x"; }""")]
    [InlineData("""class T { string V = "my-api-token"; }""")]
    [InlineData("""class T { void M(string apiKey) { } }""")]
    [InlineData("""class T { string Secret { get; set; } = ""; }""")]
    public async Task Reports_hardcoded_data(string source)
    {
        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("PWNET004", d.Id));
    }

    [Theory]
    [InlineData("""class T { string Url = "http://localhost:5000/x"; }""")]
    [InlineData("""class T { string Url = "https://localhost"; }""")]
    [InlineData("""class T { string Name = "hello"; int count = 1; }""")]
    public async Task Ignores_localhost_and_benign_values(string source)
    {
        Assert.Empty(await AnalyzerTestHelper.RunAsync(Analyzer, source));
    }

    [Fact]
    public async Task Ignores_non_test_code()
    {
        const string source = """class T { string Url = "https://prod.example.com"; string password = ""; }""";

        var diagnostics = await AnalyzerTestHelper.RunAsync(
            Analyzer, source, AnalyzerTestHelper.NonTestPath, AnalyzerTestHelper.NonTestAssembly);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Edge_cases_scheme_only_interpolation_and_single_report_per_literal()
    {
        const string source = """
            class T
            {
                void M(string host)
                {
                    var a = $"https://{host}/x";
                    var b = "https://prod.example.com/password-reset";
                    var c = "see ftp://x and http://localhost/ok";
                }
            }
            """;

        var diagnostics = await AnalyzerTestHelper.RunAsync(Analyzer, source);

        // Only literal b: remote host AND a keyword, but reported once.
        Assert.Single(diagnostics);
    }
}
