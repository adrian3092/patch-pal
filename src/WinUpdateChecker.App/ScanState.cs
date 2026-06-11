using CommunityToolkit.Mvvm.ComponentModel;
using WinUpdateChecker.Core.Scanning;

namespace WinUpdateChecker.App;

/// <summary>Latest scan result, shared by the Updates and All apps pages.</summary>
public sealed partial class ScanState : ObservableObject
{
    [ObservableProperty]
    private ScanResult? _lastResult;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private DateTimeOffset? _lastScanTime;
}
