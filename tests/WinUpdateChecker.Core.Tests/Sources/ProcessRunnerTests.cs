using WinUpdateChecker.Core.Sources;

namespace WinUpdateChecker.Core.Tests.Sources;

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
}
