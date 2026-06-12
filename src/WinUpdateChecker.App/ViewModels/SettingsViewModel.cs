using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinUpdateChecker.Core.Settings;

namespace WinUpdateChecker.App.ViewModels;

public sealed partial class SourceToggleViewModel(string name, bool isInstalled, bool isEnabled) : ObservableObject
{
    public string Name { get; } = name;
    public string DisplayName { get; } = name switch
    {
        "winget" => "winget",
        "scoop" => "Scoop",
        _ => "Chocolatey",
    };
    public bool IsInstalled { get; } = isInstalled;
    public string InstalledText => IsInstalled ? "" : "not installed";

    [ObservableProperty]
    private bool _isEnabled = isEnabled;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly Action<ThemePreference> _applyTheme;
    private AppSettings _settings;
    private bool _initializing = true;

    public SettingsViewModel() : this(
        AppServices.SettingsStore, AppServices.Settings,
        name => AppServices.Sources.First(s => s.Name == name).IsInstalled(),
        ThemeApplier.Apply)
    {
    }

    public SettingsViewModel(SettingsStore store, AppSettings settings,
        Func<string, bool> sourceInstalled, Action<ThemePreference> applyTheme)
    {
        _store = store;
        _settings = settings;
        _applyTheme = applyTheme;

        SelectedTheme = settings.Theme;
        ScanOnLaunch = settings.ScanOnLaunch;
        IncludeSystemComponents = settings.IncludeSystemComponents;

        foreach (var name in new[] { "winget", "scoop", "chocolatey" })
        {
            var toggle = new SourceToggleViewModel(name, sourceInstalled(name),
                !settings.DisabledSources.Contains(name, StringComparer.OrdinalIgnoreCase));
            toggle.PropertyChanged += (_, _) => PersistSourceStates();
            Sources.Add(toggle);
        }
        _initializing = false;
    }

    public ObservableCollection<SourceToggleViewModel> Sources { get; } = [];
    public IReadOnlyList<ThemePreference> ThemeChoices { get; } = [ThemePreference.System, ThemePreference.Light, ThemePreference.Dark];

    [ObservableProperty]
    private ThemePreference _selectedTheme;

    [ObservableProperty]
    private bool _scanOnLaunch;

    [ObservableProperty]
    private bool _includeSystemComponents;

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (_initializing) return;
        _applyTheme(value);
        Persist(s => s with { Theme = value });
    }

    partial void OnScanOnLaunchChanged(bool value)
    {
        if (!_initializing) Persist(s => s with { ScanOnLaunch = value });
    }

    partial void OnIncludeSystemComponentsChanged(bool value)
    {
        if (!_initializing) Persist(s => s with { IncludeSystemComponents = value });
    }

    public void PersistSourceStates()
    {
        if (_initializing) return;
        var disabled = Sources.Where(s => !s.IsEnabled).Select(s => s.Name).ToList();
        Persist(s => s with { DisabledSources = disabled });
    }

    private void Persist(Func<AppSettings, AppSettings> update)
    {
        _settings = update(_settings);
        _store.Save(_settings);
        AppServices.Settings = _settings;                        // keep the shared instance fresh
    }
}
