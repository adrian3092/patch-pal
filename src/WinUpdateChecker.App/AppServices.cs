using WinUpdateChecker.Core.History;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Settings;
using WinUpdateChecker.Core.Sources;
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.App;

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
        Settings = SettingsStore.Load();
    }

    public static void SaveSettings() => SettingsStore.Save(Settings);
}
