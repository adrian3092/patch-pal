using PatchPal.Core.Persistence;

namespace PatchPal.Core.Settings;

/// <summary>Settings persistence. Pass a directory for tests; defaults to %APPDATA%\PatchPal.</summary>
public sealed class SettingsStore(string? directory = null)
{
    private readonly string _path = Path.Combine(
        directory ?? AppDataPaths.ResolveRoot(),
        "settings.json");

    public AppSettings Load() => JsonFileStore.Load(_path, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFileStore.Save(_path, settings);
}
