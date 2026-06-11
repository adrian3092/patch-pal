namespace WinUpdateChecker.Core.Settings;

public enum ThemePreference { System, Light, Dark }

/// <summary>User preferences persisted to %APPDATA%\WinUpdateChecker\settings.json.</summary>
public sealed record AppSettings
{
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public bool ScanOnLaunch { get; init; } = true;
    public bool IncludeSystemComponents { get; init; }

    /// <summary>Sources the user switched off. Default: none (all detected sources active).</summary>
    public List<string> DisabledSources { get; init; } = [];
}
