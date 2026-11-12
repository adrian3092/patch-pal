using System.Diagnostics;
using System.Text;

namespace PatchPal.Core.Sources;

public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>
    /// Held around every start that inherits handles. UserProcessLauncher makes its pipe ends
    /// inheritable for a moment; a redirected Process.Start on another thread in that moment would
    /// inherit them too, and the de-elevated run's output would never reach end-of-file.
    /// </summary>
    internal static readonly object StartLock = new();

    public bool CommandExists(string command)
    {
        // v1 used PowerShell's Get-Command, which resolves App Execution Aliases
        // (winget) and shims (scoop.cmd). File.Exists over PATH misses alias reparse
        // stubs, so ask where.exe; its exit code is authoritative.
        try
        {
            var psi = new ProcessStartInfo(WindowsTools.WherePath, command)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            ScrubLoaderVars(psi);
            using var proc = StartLocked(psi);
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

    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null)
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

        ScrubLoaderVars(psi);
        ApplyEnvironment(psi, environment);

        Process? proc;
        try
        {
            proc = StartLocked(psi);
        }
        catch (System.ComponentModel.Win32Exception) when (!fileName.Contains('\\') && !fileName.Contains('/'))
        {
            // App Execution Aliases (winget) are reparse stubs that direct CreateProcess
            // cannot always launch (observed: Explorer-launched GUI). cmd.exe resolves
            // them reliably. fileName/arguments are fixed strings + validated package ids,
            // so no untrusted text reaches this command line.
            var viaCmd = new ProcessStartInfo(WindowsTools.CmdPath, $"/d /s /c \"{fileName} {arguments}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            ScrubLoaderVars(viaCmd);
            ApplyEnvironment(viaCmd, environment);
            proc = StartLocked(viaCmd);
        }
        if (proc is null) throw new InvalidOperationException($"Failed to start {fileName}");
        return await CollectAsync(proc, proc.StandardOutput, proc.StandardError, ct);
    }

    public async Task<ProcessResult> RunAsUserAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        // Never falls back to a normal (elevated) start: UserProcessLauncher throws ProcessLaunchException.
        var started = UserProcessLauncher.Start(fileName, arguments, IsLoaderVar);
        using var stdOut = started.StdOut;
        using var stdErr = started.StdErr;
        return await CollectAsync(started.Process, stdOut, stdErr, ct);
    }

    private static async Task<ProcessResult> CollectAsync(Process proc, StreamReader stdOutReader, StreamReader stdErrReader, CancellationToken ct)
    {
        using var _ = proc;
        var stdOut = stdOutReader.ReadToEndAsync(ct);
        var stdErr = stdErrReader.ReadToEndAsync(ct);
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Cancelling only stops the wait; end the process (and the cmd.exe wrapper's child)
            // too, or the package manager keeps running after the user cancels the scan.
            try { proc.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // It already exited, or Windows refused; there's nothing more to do.
            }
            throw;
        }
        return new ProcessResult(proc.ExitCode, await stdOut, await stdErr);
    }

    public async Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        // Already elevated (the app manifest requests admin at launch): run directly,
        // which redirects output for the history log and opens no console window.
        if (IsElevated())
            return await RunAsync(fileName, arguments, ct, environment);

        // ShellExecute ignores ProcessStartInfo.Environment. A caller that needs a controlled
        // environment must not get an elevated child without it.
        if (environment is not null)
            throw new ProcessLaunchException(
                $"Couldn't start {Path.GetFileName(fileName)} with admin rights and a controlled environment; run Patch Pal as administrator.");

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true, // required for the UAC verb
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden, // ShellExecute cannot redirect; at least keep the console hidden
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");
        await proc.WaitForExitAsync(ct);
        return new ProcessResult(proc.ExitCode, "", "");
    }

    private static readonly string[] LoaderVarPrefixes = ["DOTNET_", "CORECLR_", "COR_"];

    // .NET / .NET Framework loader variables (DOTNET_STARTUP_HOOKS, COR_PROFILER, ...) can inject
    // code into a child process; never hand them to the elevated children we start.
    private static void ScrubLoaderVars(ProcessStartInfo psi)
    {
        var doomed = psi.Environment.Keys.Where(IsLoaderVar).ToList();
        foreach (var key in doomed)
            psi.Environment.Remove(key);
    }

    private static void ApplyEnvironment(ProcessStartInfo psi, IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is null) return;
        foreach (var (name, value) in environment)
        {
            if (value is null) psi.Environment.Remove(name);
            else psi.Environment[name] = value;
        }
    }

    private static Process? StartLocked(ProcessStartInfo psi)
    {
        lock (StartLock) return Process.Start(psi);
    }

    private static bool IsLoaderVar(string name)
        => LoaderVarPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
