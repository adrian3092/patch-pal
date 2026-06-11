using CommunityToolkit.Mvvm.ComponentModel;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.App.ViewModels;

/// <summary>One row on the Updates / All apps pages: a ReportRow plus UI state.</summary>
public sealed partial class UpdateRowViewModel(ReportRow row) : ObservableObject
{
    public ReportRow Row { get; } = row;

    public string Name => Row.Name;
    public string? Publisher => Row.Publisher;
    public string Current => Row.Current;
    public string Available => Row.Available;
    public string Source => Row.PackageSource;
    public string Status => Row.Status;
    public bool IsUpdate => Row.IsUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private RowState _state = RowState.Idle;

    [ObservableProperty]
    private string _stateMessage = "";

    [ObservableProperty]
    private string _log = "";

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Per-row Update button enabled: only for real updates not already running.</summary>
    public bool CanUpdate => IsUpdate && State is RowState.Idle or RowState.Failed;
}
