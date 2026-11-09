using System.IO;
using PatchPal.Core.History;
using PatchPal.Core.Persistence;
using PatchPal.Core.Scanning;
using PatchPal.Core.Settings;
using PatchPal.Core.Sources;
using PatchPal.Core.Upgrading;

namespace PatchPal.App;

/// <summary>
/// Composition root. NavigationView instantiates pages via parameterless constructors,
/// so pages and view-models pull shared services from here instead of constructor DI.
/// </summary>
public static class AppServices
{
    public static SettingsStore SettingsStore { get; } = new();
    public static AppSettings Settings { get; set; } = null!;
    public static HistoryStore HistoryStore { get; } = new();
    public static IReadOnlyList<IPackageSource> Sources { get; }
    public static ScanService ScanService { get; }
    public static BatchUpgradeRunner BatchUpgradeRunner { get; }
    public static ScanState ScanState { get; } = new();
    public static Wpf.Ui.ISnackbarService SnackbarService { get; } = new Wpf.Ui.SnackbarService();

    static AppServices()
    {
        var runner = new ProcessRunner();
        Sources = [new WingetSource(runner), new ScoopSource(runner), new ChocoSource(runner)];
        ScanService = new ScanService(new RegistryScanner(), Sources);
        BatchUpgradeRunner = new BatchUpgradeRunner(new UpgradeRunner(Sources), HistoryStore);
        Settings = LoadSettings();
    }

    // Settings that can't be read right now (locked, or access denied) give way to the defaults for this run.
    private static AppSettings LoadSettings()
    {
        try
        {
            return SettingsStore.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "Couldn't read settings.json; using the defaults");
            return new AppSettings();
        }
    }

    public static void SaveSettings() => SettingsStore.Save(Settings);
}
