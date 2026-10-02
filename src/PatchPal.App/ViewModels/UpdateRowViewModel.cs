using CommunityToolkit.Mvvm.ComponentModel;
using PatchPal.Core.Models;
using PatchPal.Core.Upgrading;

namespace PatchPal.App.ViewModels;

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

    /// <summary>An update the app installs itself because its package manager can't (Microsoft Edge).</summary>
    public bool UpdatesItself => IsUpdate && SelfUpdatingApps.IsSelfUpdating(Source, Row.PackageId);

    /// <summary>An update Patch Pal can apply through the row's package manager.</summary>
    public bool IsUpdatable => IsUpdate && !UpdatesItself;

    /// <summary>Hover text for a failed row: the whole message, which may be trimmed on screen, then the log.</summary>
    public string FailureDetails => string.IsNullOrWhiteSpace(Log) ? StateMessage : $"{StateMessage}\n\n{Log}";

    /// <summary>Status as shown on the All apps page: an app that updates itself says so.</summary>
    public string DisplayStatus => UpdatesItself ? "Updates itself" : Status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private RowState _state = RowState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailureDetails))]
    private string _stateMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailureDetails))]
    private string _log = "";

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Per-row Update button enabled: only for updatable rows not already running.</summary>
    public bool CanUpdate => IsUpdatable && State is RowState.Idle or RowState.Failed;
}
