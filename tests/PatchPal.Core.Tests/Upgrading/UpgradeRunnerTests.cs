using PatchPal.Core.Sources;
using PatchPal.Core.Tests.Sources;
using PatchPal.Core.Upgrading;

namespace PatchPal.Core.Tests.Upgrading;

public class UpgradeRunnerTests
{
    private static UpgradeRunner CreateRunner(FakeProcessRunner fake)
        => new([new WingetSource(fake), new ScoopSource(fake), new ChocoSource(fake, FakeChocolateyLocator.Installed)]);

    [Fact]
    public async Task RefusesCraftedId_WithoutLaunchingAnyProcess()
    {
        // Ports the Pester case: Invoke-PackageUpgrade blocks crafted id.
        var fake = new FakeProcessRunner();
        var result = await CreateRunner(fake).UpgradeAsync("scoop", "evil; calc.exe", "evil");
        Assert.False(result.Success);
        Assert.StartsWith("Refused:", result.Message);
        Assert.Empty(fake.Calls); // nothing was executed
    }

    [Fact]
    public async Task UpgradesFromTheSourceTheUpdateCameFrom()
    {
        var fake = new FakeProcessRunner();
        await CreateRunner(fake).UpgradeAsync("winget", "9NKSQGP7F2NH", "WhatsApp", repository: "msstore");
        Assert.Contains("--source msstore", Assert.Single(fake.Calls).Arguments);
    }

    [Fact]
    public async Task RefusesAnUnsafeSourceName_WithoutLaunchingAnyProcess()
    {
        // The source name comes from winget's output and lands on an elevated command line, like the id.
        var fake = new FakeProcessRunner();
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git", repository: "winget & calc");
        Assert.False(result.Success);
        Assert.StartsWith("Refused:", result.Message);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task UnknownSource_FailsCleanly()
    {
        var result = await CreateRunner(new FakeProcessRunner()).UpgradeAsync("npm", "left-pad", "left-pad");
        Assert.False(result.Success);
        Assert.Contains("Unknown package source", result.Message);
    }

    [Fact]
    public async Task ExitZero_IsSuccess()
    {
        var fake = new FakeProcessRunner { ExitCode = 0 };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.True(result.Success);
        Assert.Equal("Succeeded.", result.Message);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task NonZeroExit_IsTranslatedFailure()
    {
        var fake = new FakeProcessRunner { ExitCode = -1978335189 };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.False(result.Success);
        Assert.StartsWith("No applicable upgrade", result.Message);
    }

    [Fact]
    public async Task LaunchException_BecomesFailureMessage()
    {
        // Most commonly the user declined the UAC elevation prompt.
        var fake = new FakeProcessRunner { ThrowOnRun = new InvalidOperationException("The operation was canceled by the user.") };
        var result = await CreateRunner(fake).UpgradeAsync("winget", "Git.Git", "Git");
        Assert.False(result.Success);
        Assert.Equal("The operation was canceled by the user.", result.Message);
    }

    [Fact]
    public async Task CapturedOutput_BecomesLog()
    {
        var fake = new FakeProcessRunner { StdOut = "Updating extras-app_name...\nDone." };
        var result = await CreateRunner(fake).UpgradeAsync("scoop", "extras-app_name", "App");
        Assert.Contains("Updating extras-app_name", result.Log);
    }
}
