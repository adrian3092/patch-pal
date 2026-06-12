using WinUpdateChecker.App.ViewModels;

namespace WinUpdateChecker.App.Views.Pages;

public partial class AllAppsPage
{
    private readonly AllAppsViewModel _viewModel = new();

    public AllAppsPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_viewModel.Rows);
        view.Filter = o => _viewModel.MatchesFilter((UpdateRowViewModel)o, _viewModel.FilterText);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AllAppsViewModel.FilterText)) view.Refresh();
        };

        Loaded += (_, _) => _viewModel.Refresh();                // re-read shared scan on each visit
    }
}
