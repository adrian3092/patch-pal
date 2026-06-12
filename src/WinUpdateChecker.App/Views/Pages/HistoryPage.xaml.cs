using WinUpdateChecker.App.ViewModels;
using Wpf.Ui.Controls;

namespace WinUpdateChecker.App.Views.Pages;

public partial class HistoryPage
{
    private readonly HistoryViewModel _viewModel = new();

    public HistoryPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _viewModel.ConfirmInteraction = async prompt =>
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Clear history",
                Content = prompt,
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
            };
            return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
        };

        Loaded += (_, _) => _viewModel.Refresh();
    }
}
