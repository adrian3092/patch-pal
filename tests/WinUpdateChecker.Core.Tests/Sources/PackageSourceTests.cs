using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

public class PackageSourceTests
{
    [Fact]
    public async Task Winget_ListOutdated_UsesExactArguments()
    {
        var runner = new FakeProcessRunner();
        await new WingetSource(runner).ListOutdatedAsync();
        var call = Assert.Single(runner.Calls);
        Assert.Equal("winget", call.FileName);
        Assert.Equal("upgrade --include-unknown --accept-source-agreements", call.Arguments);
        Assert.False(call.Elevated);
    }

    [Fact]
    public async Task Winget_Upgrade_IsElevatedWithExactIdMatch()
    {
        var runner = new FakeProcessRunner();
        await new WingetSource(runner).RunUpgradeAsync("Git.Git");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("winget", call.FileName);
        Assert.Equal(
            "upgrade --id Git.Git --exact --source winget --accept-package-agreements --accept-source-agreements --disable-interactivity -h",
            call.Arguments);
        Assert.True(call.Elevated);
    }

    [Fact]
    public async Task Scoop_Upgrade_RunsAsCurrentUserThroughPowerShell()
    {
        // The id is interpolated into a child-shell command, so callers MUST validate it
        // with PackageIdValidator first (enforced by UpgradeRunner in the next task).
        var runner = new FakeProcessRunner();
        await new ScoopSource(runner).RunUpgradeAsync("extras-app_name");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("powershell", call.FileName);
        Assert.Equal("-NoProfile -Command \"scoop update extras-app_name\"", call.Arguments);
        Assert.False(call.Elevated);
    }

    [Fact]
    public async Task Choco_Upgrade_IsElevated()
    {
        var runner = new FakeProcessRunner();
        await new ChocoSource(runner).RunUpgradeAsync("git.install");
        var call = Assert.Single(runner.Calls);
        Assert.Equal("choco", call.FileName);
        Assert.Equal("upgrade git.install -y", call.Arguments);
        Assert.True(call.Elevated);
    }

    [Fact]
    public async Task ListOutdated_FeedsRawOutputToParser()
    {
        var runner = new FakeProcessRunner { StdOut = "7zip|23.1.0|24.8.0|false\n" };
        var result = await new ChocoSource(runner).ListOutdatedAsync();
        Assert.Single(result);
        Assert.Equal("7zip", result[0].Id);
    }

    [Theory]
    [InlineData("winget", "winget")]
    [InlineData("scoop", "scoop")]
    [InlineData("chocolatey", "choco")]
    public void IsInstalled_ChecksTheRightExecutable(string sourceName, string expectedExe)
    {
        var runner = new FakeProcessRunner();
        IPackageSource source = sourceName switch
        {
            "winget" => new WingetSource(runner),
            "scoop" => new ScoopSource(runner),
            _ => new ChocoSource(runner),
        };
        Assert.Equal(sourceName, source.Name);
        Assert.True(source.IsInstalled());
        // FakeProcessRunner can't observe the exe name for CommandExists, so assert the
        // contract on the real constant instead:
        Assert.Equal(expectedExe, source.ExecutableName);
    }
}
