namespace WinUpdateChecker.Core.Upgrading;

public sealed record UpgradeResult(
    string Name,
    string Id,
    string Source,
    bool Success,
    int? ExitCode,
    string Message,
    string Log);
