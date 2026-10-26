using AcManager.App;
using Xunit;

namespace AcManager.App.Tests;

public class AppVersionTests
{
    [Fact]
    public void Current_is_the_app_version()
    {
        Assert.Equal("1.3.0", AppVersion.Current);
    }
}
