using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public class WindowsToolsTests
{
    [Fact]
    public void PathsAreAbsoluteUnderSystem32()
    {
        var sys = Environment.SystemDirectory;
        Assert.True(Path.IsPathFullyQualified(WindowsTools.WherePath));
        Assert.Equal(Path.Combine(sys, "where.exe"), WindowsTools.WherePath);
        Assert.Equal(Path.Combine(sys, "cmd.exe"), WindowsTools.CmdPath);
        Assert.Equal(Path.Combine(sys, "WindowsPowerShell", "v1.0", "powershell.exe"), WindowsTools.PowerShellPath);
        Assert.EndsWith("explorer.exe", WindowsTools.ExplorerPath);
        Assert.True(Path.IsPathFullyQualified(WindowsTools.ExplorerPath));
    }
}
