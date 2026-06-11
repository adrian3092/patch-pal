using System.Diagnostics;
using System.Text;

namespace WinUpdateChecker.Core.Sources;

public sealed class ProcessRunner : IProcessRunner
{
    public bool CommandExists(string command)
    {
        // v1 used PowerShell's Get-Command, which resolves App Execution Aliases
        // (winget) and shims (scoop.cmd). File.Exists over PATH misses alias reparse
        // stubs, so ask where.exe — its exit code is authoritative.
        try
        {
            var psi = new ProcessStartInfo("where.exe", command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdOut = proc.StandardOutput.ReadToEndAsync(ct);
        var stdErr = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return new ProcessResult(proc.ExitCode, await stdOut, await stdErr);
    }

    public async Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true, // required for the UAC verb
            Verb = "runas",
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        await proc.WaitForExitAsync(ct);
        return new ProcessResult(proc.ExitCode, "", "");
    }
}
