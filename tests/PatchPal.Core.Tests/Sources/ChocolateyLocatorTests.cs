using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public sealed class ChocolateyLocatorTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"pp-choco-{Guid.NewGuid():N}")).FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeTrust(Func<string, bool> isAdminOnly, Func<string, bool>? hasFixedLocation = null) : IDirectoryTrust
    {
        public List<string> Asked { get; } = [];

        public bool IsAdminOnly(string path)
        {
            Asked.Add(path);
            return isAdminOnly(path);
        }

        public bool HasFixedLocation(string path)
        {
            Asked.Add(path);
            return hasFixedLocation?.Invoke(path) ?? true;
        }
    }

    private string Shim => Path.Combine(_dir, "bin", "choco.exe");
    private string RealExe => Path.Combine(_dir, "choco.exe");

    // The default layout: bin\choco.exe is a shim that starts <dir>\choco.exe.
    private void Install()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "bin"));
        File.WriteAllText(Shim, "");
        File.WriteAllText(RealExe, "");
    }

    private ChocolateyLocator Locator(IDirectoryTrust trust) => new(trust, () => _dir);

    [Fact]
    public void TrustedInstall_ReportsTheShim()
    {
        Install();
        Assert.Equal(Shim, Locator(new FakeTrust(_ => true)).ChocoExePath);
    }

    [Fact]
    public void FolderANormalUserCanChange_IsNotTrusted()
    {
        Install();
        Assert.Null(Locator(new FakeTrust(p => p != _dir)).ChocoExePath);
    }

    [Fact]
    public void BinFolderShimOrRealExeANormalUserCanChange_IsNotTrusted()
    {
        // A trusted install folder doesn't help if bin\, the shim or the exe it starts has its own looser ACL.
        Install();
        var bin = Path.Combine(_dir, "bin");
        foreach (var weak in new[] { bin, Shim, RealExe })
            Assert.Null(Locator(new FakeTrust(p => p != weak)).ChocoExePath);
    }

    [Fact]
    public void InstallFolderANormalUserCouldMoveAway_IsNotTrusted()
    {
        Install();
        Assert.Null(Locator(new FakeTrust(_ => true, hasFixedLocation: _ => false)).ChocoExePath);
    }

    [Fact]
    public void AFileInHelpersANormalUserCanChange_IsNotTrusted()
    {
        // helpers\*.psm1 are loaded into every elevated package script.
        Install();
        var helpers = Directory.CreateDirectory(Path.Combine(_dir, "helpers", "functions")).FullName;
        var module = Path.Combine(helpers, "Install-ChocolateyPackage.ps1");
        File.WriteAllText(module, "");
        Assert.Null(Locator(new FakeTrust(p => p != module)).ChocoExePath);
        Assert.Equal(Shim, Locator(new FakeTrust(_ => true)).ChocoExePath);
    }

    [Theory]
    [InlineData("extensions")]
    [InlineData("hooks")]
    [InlineData("config")]
    [InlineData("lib")]
    [InlineData("redirects")]
    public void AnExistingSubfolderANormalUserCanChange_IsNotTrusted(string name)
    {
        Install();
        var sub = Directory.CreateDirectory(Path.Combine(_dir, name)).FullName;
        Assert.Null(Locator(new FakeTrust(p => p != sub)).ChocoExePath);
    }

    [Fact]
    public void MissingRealExe_IsNotInstalled()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "bin"));
        File.WriteAllText(Shim, "");
        Assert.Null(Locator(new FakeTrust(_ => true)).ChocoExePath);
    }

    [Fact]
    public void NoChocoExe_IsNotInstalled_AndTrustIsNotAsked()
    {
        var trust = new FakeTrust(_ => true);
        Assert.Null(Locator(trust).ChocoExePath);
        Assert.Empty(trust.Asked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"%ProgramData%\chocolatey")]
    [InlineData(@"relative\chocolatey")]
    [InlineData(@"\\server\share\chocolatey")]
    [InlineData(@"\\?\C:\chocolatey")]
    public void MissingOrUnusableMachineValue_FallsBackToProgramDataChocolatey(string? machineValue)
    {
        var locator = new ChocolateyLocator(new FakeTrust(_ => true), () => machineValue);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
        Assert.Equal(expected, locator.InstallDirectory);
    }

    [Fact]
    public void MachineValue_IsUsedAsTheInstallDirectory()
        => Assert.Equal(_dir, Locator(new FakeTrust(_ => true)).InstallDirectory);

    [Fact]
    public void ReadMachineChocolateyInstall_DoesNotThrow()
        => _ = ChocolateyLocator.ReadMachineChocolateyInstall();
}
