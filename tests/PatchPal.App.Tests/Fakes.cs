using PatchPal.Core.Models;
using PatchPal.Core.Scanning;
using PatchPal.Core.Sources;

namespace PatchPal.App.Tests;

public sealed class FakePrograms(params InstalledProgram[] programs) : IInstalledProgramProvider
{
    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents) => programs;
}

/// <summary>A clock that only moves when told to; local time is UTC so times read predictably.</summary>
public sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed class FakeRunner : IProcessRunner
{
    public int ExitCode { get; set; }
    public string ListOutput { get; set; } = "";
    public List<(string FileName, string Arguments, bool Elevated)> Calls { get; } = [];

    /// <summary>When set, upgrades wait for it, so a test can look at the app mid-update.</summary>
    public TaskCompletionSource? UpgradeGate { get; set; }

    /// <summary>When set, scans wait for it (or for cancellation), so a test can act mid-scan.</summary>
    public TaskCompletionSource? ListGate { get; set; }

    public bool CommandExists(string command) => true;

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, false));
        if (ListGate is not null) await ListGate.Task.WaitAsync(ct);
        return new ProcessResult(ExitCode, ListOutput, "");
    }

    public async Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        Calls.Add((fileName, arguments, true));
        if (UpgradeGate is not null) await UpgradeGate.Task;
        return new ProcessResult(ExitCode, "", "");
    }
}
