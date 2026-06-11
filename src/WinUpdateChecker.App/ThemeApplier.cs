using WinUpdateChecker.Core.Settings;
using Wpf.Ui.Appearance;

namespace WinUpdateChecker.App;

/// <summary>
/// Maps the persisted ThemePreference onto WPF-UI's theme manager.
/// Adaptation from plan (v3) to v4: ApplySystemTheme() was removed; for System preference
/// we derive the current system theme and apply it, relying on SystemThemeWatcher for
/// subsequent auto-updates (wired in MainWindow).
/// </summary>
public static class ThemeApplier
{
    public static void Apply(ThemePreference preference)
    {
        switch (preference)
        {
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                break;
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                break;
            default:
                // For System: detect and apply the current OS theme.
                // SystemThemeWatcher.Watch() keeps it in sync after first application.
                var systemIsDark = SystemThemeManager.GetCachedSystemTheme()
                    is SystemTheme.Dark or SystemTheme.Flow or SystemTheme.Glow
                    or SystemTheme.CapturedMotion;
                ApplicationThemeManager.Apply(
                    systemIsDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                break;
        }
    }
}
