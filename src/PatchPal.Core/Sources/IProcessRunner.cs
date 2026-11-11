namespace PatchPal.Core.Sources;

public sealed record ProcessResult(int? ExitCode, string StdOut, string StdErr);

/// <summary>Seam for invoking package-manager CLIs; mocked in tests.</summary>
public interface IProcessRunner
{
    bool CommandExists(string command);

    /// <summary>
    /// Run hidden with stdout/stderr captured. <paramref name="environment"/> overrides the child's
    /// environment: a value sets the variable, null removes it.
    /// </summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null);

    /// <summary>
    /// Run elevated. Patch Pal normally already is, so this runs directly with output captured and
    /// <paramref name="environment"/> applied. Otherwise it goes through a UAC prompt (ShellExecute),
    /// which can neither redirect output nor control the environment: with an environment given it
    /// throws <see cref="ProcessLaunchException"/> instead of starting without it.
    /// </summary>
    Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null);

    /// <summary>
    /// Run hidden with stdout/stderr captured, with the user's normal rights: Medium integrity,
    /// Administrators deny-only, admin privileges removed. Cancelling kills the whole process tree.
    /// Throws <see cref="ProcessLaunchException"/> when the de-elevated process can't be created;
    /// it never falls back to an admin start.
    /// </summary>
    Task<ProcessResult> RunAsUserAsync(string fileName, string arguments, CancellationToken ct = default);
}
