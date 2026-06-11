namespace WinUpdateChecker.Core.Sources;

public sealed record ProcessResult(int? ExitCode, string StdOut, string StdErr);

/// <summary>Seam for invoking package-manager CLIs; mocked in tests.</summary>
public interface IProcessRunner
{
    bool CommandExists(string command);

    /// <summary>Run hidden with stdout/stderr captured.</summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);

    /// <summary>Run elevated via UAC. ShellExecute cannot redirect output, so only the exit code is captured.</summary>
    Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default);
}
