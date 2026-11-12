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

public class UpgradeRunnerArchitectureTests
{
    private const int FatalInstallerError = 1603;

    private static UpgradeRunner Runner(FakeProcessRunner fake, System.Runtime.InteropServices.Architecture machine)
        => new([new WingetSource(fake), new ChocoSource(fake, FakeChocolateyLocator.Installed)], machine);

    private static FakeProcessRunner Failing(string showOutput, int exitCode = FatalInstallerError)
        => new() { ExitCode = exitCode, StdOut = showOutput };

    [Fact]
    public async Task ArmPc_NoArm64InstallerInWinget_ExplainsWhy()
    {
        var fake = Failing(WingetShowParserTests.NoArm64Installer);

        var result = await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.False(result.Success);
        Assert.Contains("no ARM64 installer", result.Message);
        Assert.Contains("Fatal installer error (1603)", result.Log);   // the original reason stays in the log
        var show = fake.Calls[1];
        Assert.False(show.Elevated);
        Assert.Contains("show --id NordSecurity.NordVPN --exact --source winget --architecture arm64", show.Arguments);
    }

    [Fact]
    public async Task ArmPc_Arm64InstallerExists_KeepsTheInstallersOwnReason()
    {
        var fake = Failing(WingetShowParserTests.HasInstaller);

        var result = await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.StartsWith("Fatal installer error (1603)", result.Message);
    }

    [Fact]
    public async Task X64Pc_NeverAsksAboutArm64()
    {
        var fake = Failing(WingetShowParserTests.NoArm64Installer);

        var result = await Runner(fake, System.Runtime.InteropServices.Architecture.X64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.Single(fake.Calls);
        Assert.StartsWith("Fatal installer error (1603)", result.Message);
    }

    [Fact]
    public async Task ArmPc_Success_DoesNotAsk()
    {
        var fake = Failing(WingetShowParserTests.NoArm64Installer, exitCode: 0);

        var result = await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.True(result.Success);
        Assert.Single(fake.Calls);
    }

    [Theory]
    [InlineData("winget", "msstore")]                 // Store listings carry no installer hash to read
    [InlineData("chocolatey", null)]                  // only winget can be asked
    public async Task ArmPc_OtherSources_AreNotAsked(string source, string? repository)
    {
        var fake = Failing(WingetShowParserTests.NoArm64Installer);

        await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync(source, "Some.App", "Some App", repository);

        Assert.Single(fake.Calls);
    }

    [Theory]
    [InlineData(1602)]                                // the user cancelled the installer
    [InlineData(-1978334969)]                         // 0x8A150107: no network
    [InlineData(-2147023673)]                         // UAC prompt declined
    public async Task ArmPc_FailuresWithAnotherClearCause_AreNotAsked(int exitCode)
    {
        var fake = Failing(WingetShowParserTests.NoArm64Installer, exitCode);

        await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task ArmPc_TheCheckItselfFails_KeepsTheOriginalReason()
    {
        var fake = Failing("No package found matching input criteria.");

        var result = await Runner(fake, System.Runtime.InteropServices.Architecture.Arm64)
            .UpgradeAsync("winget", "NordSecurity.NordVPN", "NordVPN");

        Assert.StartsWith("Fatal installer error (1603)", result.Message);
    }
}
