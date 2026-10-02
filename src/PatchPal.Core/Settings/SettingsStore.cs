using PatchPal.Core.Persistence;

namespace PatchPal.Core.Settings;

/// <summary>Settings persistence. Pass a directory for tests; defaults to %APPDATA%\PatchPal.</summary>
public sealed class SettingsStore(string? directory = null)
{
    private readonly string _path = Path.Combine(
        directory ?? AppDataPaths.ResolveRoot(),
        "settings.json");

    public AppSettings Load()
    {
        var settings = JsonFileStore.Load(_path, () => new AppSettings());
        // A hand-edited file can say "DisabledSources": null, which replaces the empty default.
        return settings.DisabledSources is null ? settings with { DisabledSources = [] } : settings;
    }

    public void Save(AppSettings settings) => JsonFileStore.Save(_path, settings);
}
