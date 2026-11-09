using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PatchPal.Core.Persistence;
using PatchPal.Core.Settings;

namespace PatchPal.App.ViewModels;

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
    private readonly Action<Exception, string> _logError;
    private AppSettings _settings;
    private bool _initializing = true;

    public SettingsViewModel() : this(
        AppServices.SettingsStore, AppServices.Settings,
        name => AppServices.Sources.First(s => s.Name == name).IsInstalled(),
        ThemeApplier.Apply,
        (error, context) => ErrorLog.Write(error, context))
    {
    }

    public SettingsViewModel(SettingsStore store, AppSettings settings,
        Func<string, bool> sourceInstalled, Action<ThemePreference> applyTheme,
        Action<Exception, string>? logError = null)
    {
        _store = store;
        _settings = settings;
        _applyTheme = applyTheme;
        _logError = logError ?? ((_, _) => { });

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
        try
        {
            // Change what the file holds now, not the copy read at startup: that copy is the
            // defaults when settings.json couldn't be read then, and saving it would reset the rest.
            _settings = _store.Update(update);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the change for this session without saving over a file that can't be read now.
            _settings = update(_settings);
            _logError(ex, "Couldn't save settings.json; the change lasts until Patch Pal closes");
        }
        AppServices.Settings = _settings;                        // keep the shared instance fresh
    }
}
