using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public class ProcessRunnerTests
{
    [Fact]
    public void CommandExists_FindsCmd()
        => Assert.True(new ProcessRunner().CommandExists("cmd"));

    [Fact]
    public void CommandExists_RejectsNonexistentCommand()
        => Assert.False(new ProcessRunner().CommandExists("definitely-not-a-real-command-xyz"));

    [Fact]
    public async Task RunAsync_CapturesOutputAndExitCode()
    {
        var result = await new ProcessRunner().RunAsync("cmd", "/c echo hello & exit 3");
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_Cancelled_StopsTheProcess()
    {
        // Cancelling a scan must not leave the package manager running in the background.
        // The child would write the marker after ~2 s if it were left running.
        var marker = Path.Combine(Path.GetTempPath(), $"patchpal-cancel-{Guid.NewGuid():N}.txt");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessRunner().RunAsync(
                "cmd", $"/c ping -n 3 127.0.0.1 >nul & echo done>\"{marker}\"", cts.Token));

            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(File.Exists(marker));
        }
        finally
        {
            File.Delete(marker);
        }
    }

    [Fact]
    public async Task RunAsync_RemovesDotnetLoaderVarsFromChildren()
    {
        // Loader-injection variables must not reach the (elevated) children Patch Pal starts.
        // cmd prints an unset %NAME% as its literal text.
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", @"C:\x.dll");
        try
        {
            var r = await new ProcessRunner().RunAsync(WindowsTools.CmdPath, "/c echo [%DOTNET_STARTUP_HOOKS%]");
            Assert.Contains("[%DOTNET_STARTUP_HOOKS%]", r.StdOut);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null);
        }
    }

    [Fact]
    public async Task RunAsUserAsync_RunsAndCapturesOutput()
    {
        // Non-elevated (CI, dev shell) it runs as the same user; elevated it de-elevates.
        var result = await new ProcessRunner().RunAsUserAsync(WindowsTools.CmdPath, "/c echo hi & exit 4");
        Assert.Equal(4, result.ExitCode);
        Assert.Contains("hi", result.StdOut);
    }

    [Fact]
    public async Task RunAsUserAsync_Cancelled_StopsTheProcess()
    {
        var marker = Path.Combine(Path.GetTempPath(), $"patchpal-cancel-user-{Guid.NewGuid():N}.txt");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessRunner().RunAsUserAsync(
                WindowsTools.CmdPath, $"/c ping -n 3 127.0.0.1 >nul & echo done>\"{marker}\"", cts.Token));
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(File.Exists(marker));
        }
        finally { File.Delete(marker); }
    }

    [Fact]
    public async Task RunAsUserAsync_PassesTheEnvironmentWithoutLoaderVariables()
    {
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", @"C:\x.dll");
        Environment.SetEnvironmentVariable("PATCHPAL_TEST_KEEP", "kept");
        try
        {
            var r = await new ProcessRunner().RunAsUserAsync(
                WindowsTools.CmdPath, "/c echo [%DOTNET_STARTUP_HOOKS%] [%PATCHPAL_TEST_KEEP%]");
            Assert.Contains("[%DOTNET_STARTUP_HOOKS%]", r.StdOut);
            Assert.Contains("[kept]", r.StdOut);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null);
            Environment.SetEnvironmentVariable("PATCHPAL_TEST_KEEP", null);
        }
    }

    [Fact]
    public async Task RunAsync_EnvironmentOverrides_SetAndRemoveVariables()
    {
        Environment.SetEnvironmentVariable("PATCHPAL_TEST_DROP", "inherited");
        try
        {
            var r = await new ProcessRunner().RunAsync(WindowsTools.CmdPath,
                "/c echo [%PATCHPAL_TEST_SET%] [%PATCHPAL_TEST_DROP%]", default,
                new Dictionary<string, string?> { ["PATCHPAL_TEST_SET"] = "forced", ["PATCHPAL_TEST_DROP"] = null });
            Assert.Contains("[forced]", r.StdOut);
            Assert.Contains("[%PATCHPAL_TEST_DROP%]", r.StdOut);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATCHPAL_TEST_DROP", null);
        }
    }

    [Fact]
    public async Task RunElevatedAsync_WithAnEnvironment_NeverStartsWithoutIt()
    {
        // Elevated (the shipped case) it runs directly with the overrides applied; not elevated it
        // would need a UAC prompt, which can't carry them, so it refuses rather than start without.
        var env = new Dictionary<string, string?> { ["PATCHPAL_TEST_SET"] = "forced" };
        if (IsElevated())
        {
            var r = await new ProcessRunner().RunElevatedAsync(WindowsTools.CmdPath, "/c echo [%PATCHPAL_TEST_SET%]", default, env);
            Assert.Contains("[forced]", r.StdOut);
        }
        else
        {
            await Assert.ThrowsAsync<ProcessLaunchException>(() =>
                new ProcessRunner().RunElevatedAsync(WindowsTools.CmdPath, "/c exit 0", default, env));
        }
    }

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
