using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public class ShellLauncherTests
{
    [Fact]
    public void OpenUrl_LaunchesExplorerWithTheQuotedUrl()
    {
        (string File, string Args) captured = default;
        ShellLauncher.OpenUrl("https://example.com/x", (f, a) => captured = (f, a));
        Assert.Equal(WindowsTools.ExplorerPath, captured.File);
        Assert.Equal("\"https://example.com/x\"", captured.Args);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"https://example.com/"" /select,""C:\x")]
    [InlineData("not a url")]
    public void OpenUrl_RefusesAnythingButAPlainWebUrl(string url)
    {
        // explorer.exe opens files and folders too; only http(s) links may go through it,
        // and a quote must never break out of the argument.
        var called = false;
        Assert.Throws<ArgumentException>(() => ShellLauncher.OpenUrl(url, (_, _) => called = true));
        Assert.False(called);
    }
}
