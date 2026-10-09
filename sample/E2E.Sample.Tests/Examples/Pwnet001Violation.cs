// Documentation only: excluded from compilation (see csproj).
using System.Threading.Tasks;
using Xunit;

namespace E2E.Sample.Tests.Examples;

public class Pwnet001Violation
{
    [Fact]
    public async Task Waits_with_fixed_delay()
    {
        await Task.Delay(2000); // PWNET001
        Assert.True(true);
    }
}
