using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PatchPal.Core.History;

namespace PatchPal.App.ViewModels;

public sealed record HistoryGroup(DateOnly Date, IReadOnlyList<HistoryEntry> Entries)
{
    public string Title => Date == DateOnly.FromDateTime(DateTime.Today) ? "Today"
        : Date == DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) ? "Yesterday"
        : LocalFormat.Date(Date);
}

public sealed partial class HistoryViewModel(HistoryStore store) : ObservableObject
{
    public HistoryViewModel() : this(AppServices.HistoryStore) { }

    public Func<string, Task<bool>> ConfirmInteraction { get; set; } = _ => Task.FromResult(true);

    public ObservableCollection<HistoryGroup> Groups { get; } = [];

    private const string NothingAppliedText = "No updates applied yet";

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>What the page says when it lists nothing: none applied yet, or why the list couldn't be read.</summary>
    [ObservableProperty]
    private string _emptyText = NothingAppliedText;

    public void Refresh()
    {
        Groups.Clear();
        IReadOnlyList<HistoryEntry> entries;
        try
        {
            entries = store.Load();
            EmptyText = NothingAppliedText;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // There may well be entries; another program just has the file right now.
            entries = [];
            EmptyText = $"Couldn't read the update history: {ex.Message}";
        }

        foreach (var group in entries
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
