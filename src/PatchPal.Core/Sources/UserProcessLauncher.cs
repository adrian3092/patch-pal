using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static PatchPal.Core.Sources.UserProcessNative;

namespace PatchPal.Core.Sources;

/// <summary>
/// Starts a hidden process with the user's normal rights and redirected output.
/// Approach (spike, 2026-11-11, owner's PC): a restricted copy of this process's own token
/// (<see cref="RestrictedUserToken"/>) passed to CreateProcessAsUser. A restricted copy of the
/// caller's token needs no SeAssignPrimaryTokenPrivilege and no desktop, so it also works from a
/// scheduled task or --no-gui. It runs as the account Patch Pal runs as: after an over-the-shoulder
/// UAC prompt that is the admin who typed the password, not the signed-in user.
/// Verified: Medium IL, Administrators deny-only, normal privileges, user-owned files,
/// exit code/output/tree-kill, and scoop status/update (including its powershell child).
/// </summary>
internal static class UserProcessLauncher
{
    internal sealed record Started(Process Process, StreamReader StdOut, StreamReader StdErr);

    public static Started Start(string fileName, string arguments, Func<string, bool> dropEnvironmentVariable)
    {
        try
        {
            return StartCore(fileName, arguments, dropEnvironmentVariable);
        }
        catch (Exception ex) when (ex is not ProcessLaunchException)
        {
            throw new ProcessLaunchException(
                $"Couldn't start {Path.GetFileName(fileName)} without admin rights: {ex.Message}", ex);
        }
    }

    private static Started StartCore(string fileName, string arguments, Func<string, bool> dropEnvironmentVariable)
    {
        using var token = RestrictedUserToken.Create();
        var pipes = new List<SafeFileHandle>();
        Process? process = null;
        try
        {
            var (inRead, _) = CreatePipePair(pipes);
            var (outRead, outWrite) = CreatePipePair(pipes);
            var (errRead, errWrite) = CreatePipePair(pipes);
            process = Launch(token, $"\"{fileName}\" {arguments}", [inRead, outWrite, errWrite], BuildEnvironment(dropEnvironmentVariable));
            var started = new Started(process, Reader(outRead), Reader(errRead));
            pipes.Remove(outRead);
            pipes.Remove(errRead);
            return started;
        }
        catch
        {
            if (process is not null)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
            throw;
        }
        finally
        {
            // Everything but the two read ends handed to the caller closes here. The parent's copies
            // of the child's ends must close or the reads never see end-of-file; closing the stdin
            // write end gives the child an immediate EOF on stdin.
            foreach (var h in pipes) h.Dispose();
        }
    }

    private static (SafeFileHandle Read, SafeFileHandle Write) CreatePipePair(List<SafeFileHandle> owned)
    {
        var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };
        Check(CreatePipe(out var read, out var write, ref sa, 0));
        owned.Add(read);
        owned.Add(write);
        return (read, write);
    }

    private static Process Launch(SafeAccessTokenHandle token, string commandLine, SafeFileHandle[] childEnds, string environment)
    {
        var handleList = Marshal.AllocHGlobal(IntPtr.Size * childEnds.Length);
        var attributeList = IntPtr.Zero;
        var environmentBlock = Marshal.StringToHGlobalUni(environment);
        try
        {
            for (var i = 0; i < childEnds.Length; i++)
                Marshal.WriteIntPtr(handleList, i * IntPtr.Size, childEnds[i].DangerousGetHandle());

            var size = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributeList = Marshal.AllocHGlobal(size);
            Check(InitializeProcThreadAttributeList(attributeList, 1, 0, ref size));
            Check(UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                handleList, IntPtr.Size * childEnds.Length, IntPtr.Zero, IntPtr.Zero));

            var si = new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO
                {
                    cb = Marshal.SizeOf<STARTUPINFOEX>(),
                    dwFlags = STARTF_USESTDHANDLES,
                    hStdInput = childEnds[0].DangerousGetHandle(),
                    hStdOutput = childEnds[1].DangerousGetHandle(),
                    hStdError = childEnds[2].DangerousGetHandle(),
                },
                lpAttributeList = attributeList,
            };
            const uint flags = CREATE_SUSPENDED | CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT;

            PROCESS_INFORMATION pi;
            // The ends are inheritable only for this call, under the lock every other start takes,
            // and the handle list limits the child to exactly these three.
            lock (ProcessRunner.StartLock)
            {
                try
                {
                    foreach (var h in childEnds) Check(SetHandleInformation(h, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT));
                    Check(CreateProcessAsUser(token, null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero,
                        true, flags, environmentBlock, null, ref si, out pi));
                }
                finally
                {
                    foreach (var h in childEnds) SetHandleInformation(h, HANDLE_FLAG_INHERIT, 0);
                }
            }
            return AttachAndResume(pi);
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            Marshal.FreeHGlobal(handleList);
            Marshal.FreeHGlobal(environmentBlock);
        }
    }

    // The child starts suspended so Process can open its handle before it could possibly exit.
    private static Process AttachAndResume(PROCESS_INFORMATION pi)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(pi.dwProcessId);
            _ = process.Handle; // open and cache the handle now, so ExitCode works after it exits
            if (ResumeThread(pi.hThread) == uint.MaxValue)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return process;
        }
        catch
        {
            TerminateProcess(pi.hProcess, 1);
            process?.Dispose();
            throw;
        }
        finally
        {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }
    }

    private static string BuildEnvironment(Func<string, bool> drop)
    {
        var block = new StringBuilder();
        foreach (var (key, value) in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                     .Select(e => ((string)e.Key, (string?)e.Value ?? ""))
                     .Where(e => !drop(e.Item1))
                     .OrderBy(e => e.Item1, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(key).Append('=').Append(value).Append('\0');
        }
        return block.Append('\0').ToString();
    }

    private static StreamReader Reader(SafeFileHandle handle)
        => new(new FileStream(handle, FileAccess.Read, 4096, isAsync: false), Encoding.UTF8);
}
