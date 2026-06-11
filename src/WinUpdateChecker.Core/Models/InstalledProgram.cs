namespace WinUpdateChecker.Core.Models;

/// <summary>A program found in the Windows uninstall registry keys.</summary>
public sealed record InstalledProgram(string Name, string Version, string? Publisher);
