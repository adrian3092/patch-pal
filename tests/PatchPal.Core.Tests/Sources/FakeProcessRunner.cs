using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments, bool Elevated, bool AsUser)> Calls { get; } = [];
    public string StdOut { get; set; } = "";
    public string StdErr { get; set; } = "";
    public int ExitCode { get; set; }
    public bool Exists { get; set; } = true;
    public Exception? ThrowOnRun { get; set; }
    public Exception? ThrowOnRunAsUser { get; set; }

    /// <summary>The environment overrides passed with each call, in the same order as <see cref="Calls"/>.</summary>
    public List<IReadOnlyDictionary<string, string?>?> Environments { get; } = [];

    public bool CommandExists(string command) => Exists;

    public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, false, false));
        Environments.Add(environment);
        return Task.FromResult(new ProcessResult(ExitCode, StdOut, StdErr));
    }

    public Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        if (ThrowOnRun is not null) throw ThrowOnRun;
        Calls.Add((fileName, arguments, true, false));
        Environments.Add(environment);
        return Task.FromResult(new ProcessResult(ExitCode, "", ""));
    }

    public Task<ProcessResult> RunAsUserAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        if (ThrowOnRunAsUser is not null) throw ThrowOnRunAsUser;
        Calls.Add((fileName, arguments, false, true));
        Environments.Add(null);
        return Task.FromResult(new ProcessResult(ExitCode, StdOut, StdErr));
    }
}
