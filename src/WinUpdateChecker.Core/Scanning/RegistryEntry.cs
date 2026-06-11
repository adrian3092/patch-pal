namespace WinUpdateChecker.Core.Scanning;

/// <summary>Raw values read from one uninstall registry key.</summary>
public sealed record RegistryEntry(
    string? DisplayName,
    string? DisplayVersion,
    string? Publisher,
    int? SystemComponent,
    string? ParentKeyName,
    string? ReleaseType);
