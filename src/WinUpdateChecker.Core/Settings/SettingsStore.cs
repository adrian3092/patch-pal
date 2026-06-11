using WinUpdateChecker.Core.Persistence;

namespace WinUpdateChecker.Core.Settings;

/// <summary>Settings persistence. Pass a directory for tests; defaults to %APPDATA%\WinUpdateChecker.</summary>
public sealed class SettingsStore(string? directory = null)
{
    private readonly string _path = Path.Combine(
        directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinUpdateChecker"),
        "settings.json");

    public AppSettings Load() => JsonFileStore.Load(_path, () => new AppSettings());

    public void Save(AppSettings settings) => JsonFileStore.Save(_path, settings);
}
