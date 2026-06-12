using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinUpdateChecker.App.ViewModels;

/// <summary>All apps page: every detected program from the shared last scan.</summary>
public sealed partial class AllAppsViewModel(ScanState scanState) : ObservableObject
{
    public AllAppsViewModel() : this(AppServices.ScanState) { }

    public ObservableCollection<UpdateRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private string _summaryText = "";

    public void Refresh()
    {
        Rows.Clear();
        var result = scanState.LastResult;
        if (result is null)
        {
            SummaryText = "No scan yet — run a scan from the Updates page";
            return;
        }
        foreach (var row in result.Rows)
            Rows.Add(new UpdateRowViewModel(row));
        var updates = result.Rows.Count(r => r.IsUpdate);
        SummaryText = $"{result.Rows.Count} programs · {updates} update{(updates == 1 ? "" : "s")}";
    }

    public bool MatchesFilter(UpdateRowViewModel row, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (row.Publisher?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || row.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Source.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
