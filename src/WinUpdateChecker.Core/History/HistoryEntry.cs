namespace WinUpdateChecker.Core.History;

/// <summary>One applied (or attempted) upgrade, persisted for the History page.</summary>
public sealed record HistoryEntry(
    DateTimeOffset Timestamp,
    string Name,
    string PackageId,
    string Source,
    string FromVersion,
    string ToVersion,
    bool Success,
    string Message,
    string Log);
