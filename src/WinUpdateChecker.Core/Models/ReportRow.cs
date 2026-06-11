namespace WinUpdateChecker.Core.Models;

/// <summary>One merged row of the report: an installed program and/or an available update.</summary>
public sealed record ReportRow(
    string Name,
    string? Publisher,
    string Current,
    string Available,
    string Status,
    string PackageId,
    string PackageSource)
{
    public bool IsUpdate => Status.StartsWith("Update available", StringComparison.Ordinal);
}
