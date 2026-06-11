using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.Core.Tests.Upgrading;

public class UpgradeExitMessagesTests
{
    [Fact]
    public void ExitZero_ReadsAsSuccess()
        => Assert.Equal("Succeeded.", UpgradeExitMessages.Translate("winget", 0));

    [Fact]
    public void WingetUpdateNotApplicable_IsExplained()
        => Assert.StartsWith("No applicable upgrade", UpgradeExitMessages.Translate("winget", -1978335189));

    [Fact]
    public void NullExitCode_IsExplained()
        => Assert.Equal("No exit code was returned by the installer.", UpgradeExitMessages.Translate("winget", null));

    [Fact]
    public void UnknownWingetCode_IncludesHex()
        => Assert.Equal("winget exited with code -1978335100 (0x8A150084).", UpgradeExitMessages.Translate("winget", -1978335100));

    [Fact]
    public void NonWingetSource_GetsGenericMessage()
        => Assert.Equal("scoop exited with code 2.", UpgradeExitMessages.Translate("scoop", 2));
}
