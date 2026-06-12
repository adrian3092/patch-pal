using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUpdateChecker.Core.History;

namespace WinUpdateChecker.App.ViewModels;

public sealed record HistoryGroup(DateOnly Date, IReadOnlyList<HistoryEntry> Entries)
{
    public string Title => Date == DateOnly.FromDateTime(DateTime.Today) ? "Today"
        : Date == DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) ? "Yesterday"
        : Date.ToString("d MMMM yyyy");
}

public sealed partial class HistoryViewModel(HistoryStore store) : ObservableObject
{
    public HistoryViewModel() : this(AppServices.HistoryStore) { }

    public Func<string, Task<bool>> ConfirmInteraction { get; set; } = _ => Task.FromResult(true);

    public ObservableCollection<HistoryGroup> Groups { get; } = [];

    [ObservableProperty]
    private bool _isEmpty = true;

    public void Refresh()
    {
        Groups.Clear();
        foreach (var group in store.Load()
                     .OrderByDescending(e => e.Timestamp)
                     .GroupBy(e => DateOnly.FromDateTime(e.Timestamp.LocalDateTime)))
        {
            Groups.Add(new HistoryGroup(group.Key, group.ToList()));
        }
        IsEmpty = Groups.Count == 0;
    }

    [RelayCommand]
    public async Task ClearAsync()
    {
        if (!await ConfirmInteraction("Clear the entire update history?")) return;
        store.Clear();
        Refresh();
    }
}
