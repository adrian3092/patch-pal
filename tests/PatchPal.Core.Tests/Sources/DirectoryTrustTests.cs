using System.Diagnostics;
using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public class DirectoryTrustTests
{
    [Fact]
    public void FreshTempDir_IsNotAdminOnly()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pp-trust-{Guid.NewGuid():N}")).FullName;
        try { Assert.False(new DirectoryTrust().IsAdminOnly(dir)); }   // the current user owns and can write it
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FreshTempFile_IsNotAdminOnly()
    {
        var file = Path.Combine(Path.GetTempPath(), $"pp-trust-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");
        try { Assert.False(new DirectoryTrust().IsAdminOnly(file)); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void System32_IsAdminOnly()
        => Assert.True(new DirectoryTrust().IsAdminOnly(Environment.SystemDirectory));

    [Fact]
    public void System32Cmd_IsAdminOnly()
        => Assert.True(new DirectoryTrust().IsAdminOnly(WindowsTools.CmdPath));

    [Fact]
    public void ProgramData_IsNotAdminOnly()
    {
        // Normal users may create files and folders directly in ProgramData.
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Assert.False(new DirectoryTrust().IsAdminOnly(programData));
    }

    [Fact]
    public void MissingPath_IsNotAdminOnly()
        => Assert.False(new DirectoryTrust().IsAdminOnly(@"C:\no-such-folder-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void System32_HasAFixedLocation()
        => Assert.True(new DirectoryTrust().HasFixedLocation(Environment.SystemDirectory));

    [Fact]
    public void AFolderUnderTheUsersProfile_DoesNotHaveAFixedLocation()
    {
        // The user owns their profile folder, so they can rename anything beneath it.
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"pp-trust-{Guid.NewGuid():N}")).FullName;
        try { Assert.False(new DirectoryTrust().HasFixedLocation(dir)); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MissingPath_DoesNotHaveAFixedLocation()
        => Assert.False(new DirectoryTrust().HasFixedLocation(@"C:\no-such-folder-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void AJunctionToAnAdminOnlyFolder_IsNotTrusted()
    {
        // The junction's owner can retarget it after the check, so the target's ACL proves nothing.
        var junction = Path.Combine(Path.GetTempPath(), $"pp-junction-{Guid.NewGuid():N}");
        using (var mklink = Process.Start(new ProcessStartInfo(WindowsTools.CmdPath,
                   $"/c mklink /J \"{junction}\" \"{Environment.SystemDirectory}\"") { CreateNoWindow = true, UseShellExecute = false })!)
            mklink.WaitForExit();
        try
        {
            Assert.True(Directory.Exists(junction));
            Assert.False(new DirectoryTrust().IsAdminOnly(junction));
            Assert.False(new DirectoryTrust().HasFixedLocation(junction));
        }
        finally { Directory.Delete(junction); } // removes the link, not System32
    }
}
