using PatchPal.Core.Settings;
using Wpf.Ui.Appearance;

namespace PatchPal.App;

/// <summary>
/// Maps the persisted ThemePreference onto WPF-UI's theme manager.
/// WPF-UI v4 removed ApplySystemTheme(), so for the System preference this reads the current
/// system theme and applies it; SystemThemeWatcher (wired in MainWindow) follows later changes.
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
