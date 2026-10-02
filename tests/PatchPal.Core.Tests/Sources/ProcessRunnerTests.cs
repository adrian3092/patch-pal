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
}
