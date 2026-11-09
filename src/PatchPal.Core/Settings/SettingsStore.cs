using PatchPal.Core.Persistence;

namespace PatchPal.Core.Settings;

/// <summary>
/// Settings persistence. Pass a directory for tests; defaults to %APPDATA%\PatchPal.
/// <see cref="Load"/> throws when the file exists but can't be read; callers fall back to defaults.
/// </summary>
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

    /// <summary>
    /// Applies <paramref name="change"/> to the settings the file holds now and saves the result,
    /// so every other setting stays as it is. Throws, without saving, when the file exists but
    /// can't be read.
    /// </summary>
    public AppSettings Update(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Load());
        Save(updated);
        return updated;
    }
}
