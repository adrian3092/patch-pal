namespace PatchPal.Core.Upgrading;

public sealed record UpgradeResult(
    string Name,
    string Id,
    string Source,
    bool Success,
    int? ExitCode,
    string Message,
    string Log)
{
    /// <summary>Why the attempt couldn't be recorded in History, or null when it was.</summary>
    public string? HistoryError { get; init; }
}
