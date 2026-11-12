using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public class WingetShowParserTests
{
    internal const string NoArm64Installer = """
        Found NordVPN [NordSecurity.NordVPN]
        Version: 8.15.2.0
        Publisher: Nord Security
        Publisher Support Url: https://support.nordvpn.com/
        Tags:
          vpn
        Installer:
          No applicable installer found; see logs for more details.
        """;

    internal const string HasInstaller = """
        Found NordVPN [NordSecurity.NordVPN]
        Version: 8.15.2.0
        Publisher: Nord Security
        Installer:
          Installer Type: inno
          Installer Url: https://downloads.nordcdn.com/apps/windows/NordVPN/8.15.2.0/NordVPNInstall.exe
          Installer SHA256: db8286f09d749b285234cfe4bc487557d4c4a878d55fe9c56c1b5934f76a2e2f
          Release Date: 2026-11-09
        """;

    // winget prints its labels in the Windows display language; only the layout and the hash stay put.
    private const string GermanHasInstaller = """
        Gefunden NordVPN [NordSecurity.NordVPN]
        Version: 8.15.2.0
        Installationsprogramm:
          Installertyp: inno
          Installer-URL: https://downloads.nordcdn.com/apps/windows/NordVPN/8.15.2.0/NordVPNInstall.exe
          Installer-SHA256: DB8286F09D749B285234CFE4BC487557D4C4A878D55FE9C56C1B5934F76A2E2F
        """;

    private const string GermanNoInstaller = """
        Gefunden NordVPN [NordSecurity.NordVPN]
        Version: 8.15.2.0
        Installationsprogramm:
          Kein anwendbares Installationsprogramm gefunden. Weitere Informationen finden Sie in den Protokollen.
        """;

    [Theory]
    [InlineData(NoArm64Installer, false)]
    [InlineData(HasInstaller, true)]
    [InlineData(GermanHasInstaller, true)]
    [InlineData(GermanNoInstaller, false)]
    public void HasInstaller_ReadsTheInstallerHash_InAnyLanguage(string output, bool expected)
        => Assert.Equal(expected, WingetShowParser.HasInstaller(output, "NordSecurity.NordVPN"));

    [Theory]
    [InlineData("")]
    [InlineData("No package found matching input criteria.")]
    [InlineData("Found Something Else [Other.Package]\nInstaller:\n  No applicable installer found.")]
    public void HasInstaller_IsUnknown_WhenTheOutputIsntAboutThePackage(string output)
        => Assert.Null(WingetShowParser.HasInstaller(output, "NordSecurity.NordVPN"));
}
