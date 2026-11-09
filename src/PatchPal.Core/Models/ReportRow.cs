namespace PatchPal.Core.Models;

/// <summary>
/// One merged row of the report: an installed program and/or an available update.
/// <paramref name="Repository"/> is where the package manager gets the update, when it says.
/// </summary>
public sealed record ReportRow(
    string Name,
    string? Publisher,
    string Current,
    string Available,
    string Status,
    string PackageId,
    string PackageSource,
    string? Repository = null)
{
    public bool IsUpdate => Status.StartsWith("Update available", StringComparison.Ordinal);
}
