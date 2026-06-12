using WinUpdateChecker.App.ViewModels;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WinUpdateChecker.App.Views.Pages;

public partial class UpdatesPage
{
    private readonly UpdatesViewModel _viewModel = new();
    private static bool _hasScannedOnLaunch;

    public UpdatesPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.ConfirmInteraction = async prompt =>
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Confirm",
                Content = prompt,
                PrimaryButtonText = "Update",
                CloseButtonText = "Cancel",
            };
            return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
        };

        _viewModel.BatchCompleted += (ok, failed) =>
        {
            var appearance = failed == 0 ? ControlAppearance.Success : ControlAppearance.Caution;
            AppServices.SnackbarService.Show(
                "Upgrade finished", $"Succeeded: {ok}   Failed: {failed}",
                appearance, TimeSpan.FromSeconds(5));
            _ = _viewModel.ScanAsync();                          // spec: rescan after a batch
        };

        Loaded += async (_, _) =>
        {
            if (!_hasScannedOnLaunch && AppServices.Settings.ScanOnLaunch)
            {
                _hasScannedOnLaunch = true;
                await _viewModel.ScanAsync();
            }
        };

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_viewModel.Rows);
        view.Filter = o => _viewModel.MatchesFilter((UpdateRowViewModel)o, _viewModel.FilterText);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpdatesViewModel.FilterText)) view.Refresh();
        };
    }
}
