using WinUpdateChecker.Core;

namespace WinUpdateChecker.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void Version_ComesFromAssemblyMetadata()
    {
        // csproj sets <Version>2.0.0</Version>; CI overrides with -p:Version=x.y.z
        Assert.Equal("2.0.0", AppInfo.Version);
    }

    [Fact]
    public void Version_HasNoBuildMetadataSuffix()
        => Assert.DoesNotContain("+", AppInfo.Version);
}
