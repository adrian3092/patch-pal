using System.Runtime.InteropServices;
using System.Windows;
using WinUpdateChecker.Core;
using WinUpdateChecker.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinUpdateChecker.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        ThemeApplier.Apply(AppServices.Settings.Theme);
        if (AppServices.Settings.Theme == ThemePreference.System)
        {
            // v4 adaptation: Watch(window, backdropType, updateAccents) replaces ApplySystemTheme().
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);
        }

        AppServices.SnackbarService.SetSnackbarPresenter(RootSnackbar);

        // v4 adaptation: NavigationViewItem.Click is only on INavigationViewItem interface;
        // cast to the interface to subscribe.
        ((INavigationViewItem)AboutItem).Click += OnAboutClicked;

        Loaded += (_, _) => RootNavigation.Navigate(typeof(Views.Pages.UpdatesPage));
    }

    private async void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = $"About {AppInfo.ProductName}",
            Content = $"{AppInfo.ProductName} v{AppInfo.Version}  ·  {arch}\n\n" +
                      "Scans installed programs and reports available updates from\n" +
                      "winget, Scoop, and Chocolatey.\n\n" + AppInfo.RepoUrl,
            PrimaryButtonText = "Open GitHub",
            CloseButtonText = "Close",
        };
        var result = await box.ShowDialogAsync();
        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(AppInfo.RepoUrl) { UseShellExecute = true });
        }
    }
}
