using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUpdateChecker.Core.Scanning;
using WinUpdateChecker.Core.Settings;
using WinUpdateChecker.Core.Upgrading;

namespace WinUpdateChecker.App.ViewModels;

/// <summary>
/// Drives the Updates page: scan, filter, select, and run upgrades with in-place
/// row progress. Constructed with explicit dependencies for testability; the page
/// uses the parameterless overload that pulls from AppServices.
/// </summary>
public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly ScanService _scanService;
    private readonly BatchUpgradeRunner _batchRunner;
    private readonly ScanState _scanState;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _scanCts;

    /// <summary>Asks the user to confirm a batch; the page wires a dialog, tests stub it.</summary>
    public Func<string, Task<bool>> ConfirmInteraction { get; set; } = _ => Task.FromResult(true);

    /// <summary>Fires after a batch finishes so the page can show a snackbar summary.</summary>
    public event Action<int, int>? BatchCompleted; // (succeeded, failed)

    public ObservableCollection<UpdateRowViewModel> Rows { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _lastScanText = "Not scanned yet";

    [ObservableProperty]
    private int _updateCount;

    public UpdatesViewModel() : this(
        AppServices.ScanService, AppServices.BatchUpgradeRunner, AppServices.ScanState, AppServices.Settings)
    {
    }

    public UpdatesViewModel(ScanService scanService, BatchUpgradeRunner batchRunner, ScanState scanState, AppSettings settings)
    {
        _scanService = scanService;
        _batchRunner = batchRunner;
        _scanState = scanState;
        _settings = settings;
    }

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsScanning) { _scanCts?.Cancel(); return; }          // Scan button doubles as Cancel

        IsScanning = true;
        _scanState.IsScanning = true;
        _scanCts = new CancellationTokenSource();
        try
        {
            var options = new ScanOptions(
                AllSourcesExcept(_settings.DisabledSources), _settings.IncludeSystemComponents);
            var result = await _scanService.ScanAsync(options, _scanCts.Token);

            _scanState.LastResult = result;
            _scanState.LastScanTime = DateTimeOffset.Now;

            Rows.Clear();
            foreach (var row in result.Rows.Where(r => r.IsUpdate))
                Rows.Add(new UpdateRowViewModel(row));
            UpdateCount = Rows.Count;

            Warnings.Clear();
            foreach (var w in result.Warnings) Warnings.Add(w);
            if (result.EnabledSources.Count == 0)
                Warnings.Add("No package manager detected (winget, Scoop, Chocolatey) — updates cannot be checked.");

            LastScanText = $"Last scanned {DateTimeOffset.Now:HH:mm}";
        }
        catch (OperationCanceledException)
        {
            LastScanText = "Scan cancelled";
        }
        finally
        {
            IsScanning = false;
            _scanState.IsScanning = false;
            _scanCts = null;
        }
    }

    private static List<string> AllSourcesExcept(List<string> disabled)
        => disabled.Count == 0
            ? []
            : new[] { "winget", "scoop", "chocolatey" }.Except(disabled, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Filter predicate shared by the page's CollectionView and the tests.</summary>
    public bool MatchesFilter(UpdateRowViewModel row, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Row.PackageId.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || row.Source.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    public Task UpdateAllAsync()
        => RunBatchAsync(Rows.Where(r => r.CanUpdate).ToList(), $"Update all {UpdateCount} packages?");

    [RelayCommand]
    public Task UpdateSelectedAsync()
    {
        var selected = Rows.Where(r => r.IsSelected && r.CanUpdate).ToList();
        return selected.Count == 0
            ? Task.CompletedTask
            : RunBatchAsync(selected, $"Update {selected.Count} selected package(s)?");
    }

    [RelayCommand]
    public Task UpdateOneAsync(UpdateRowViewModel row)
        => row.CanUpdate ? RunBatchAsync([row], null) : Task.CompletedTask;

    private async Task RunBatchAsync(IReadOnlyList<UpdateRowViewModel> rows, string? confirmPrompt)
    {
        if (rows.Count == 0 || IsUpdating) return;
        if (confirmPrompt is not null && !await ConfirmInteraction(confirmPrompt)) return;

        IsUpdating = true;
        var byKey = rows.ToDictionary(r => $"{r.Source}|{r.Row.PackageId}");
        var items = rows.Select(r => new BatchItem(r.Source, r.Row.PackageId, r.Name, r.Current, r.Available)).ToList();

        void OnUi(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }

        try
        {
            var results = await _batchRunner.RunAsync(items,
                onItemStarted: item => OnUi(() =>
                {
                    var row = byKey[$"{item.Source}|{item.PackageId}"];
                    row.State = RowState.Updating;
                    row.StateMessage = "Updating…";
                }),
                onItemFinished: (item, result) => OnUi(() =>
                {
                    var row = byKey[$"{item.Source}|{item.PackageId}"];
                    row.State = result.Success ? RowState.Succeeded : RowState.Failed;
                    row.StateMessage = result.Success ? "Updated" : result.Message;
                    row.Log = result.Log;
                }));

            var ok = results.Count(r => r.Success);
            BatchCompleted?.Invoke(ok, results.Count - ok);
        }
        finally
        {
            IsUpdating = false;
        }
    }
}
