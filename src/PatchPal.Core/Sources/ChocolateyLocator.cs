using Microsoft.Win32;

namespace PatchPal.Core.Sources;

/// <summary>
/// Chocolatey upgrades run with Patch Pal's admin rights, so choco is taken only from the
/// machine-wide install folder, and only when everything it runs from that folder is under
/// admin-only control. Anything on PATH (a choco.cmd or choco.exe a normal account dropped there)
/// is never considered. ProgramData lets normal users create folders, so a
/// "C:\ProgramData\chocolatey" that merely exists isn't enough.
/// </summary>
public sealed class ChocolateyLocator(IDirectoryTrust trust, Func<string?> readMachineChocolateyInstall) : IChocolateyLocator
{
    private const string MachineEnvironmentKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    // Code choco loads and runs from these: every file must be admin-only.
    private static readonly string[] CheckedRecursively = ["helpers", "extensions", "hooks"];

    // Large or data-only: the folder itself must be admin-only (its files inherit from it).
    private static readonly string[] CheckedTopLevel = ["config", "lib", "redirects"];

    public ChocolateyLocator() : this(new DirectoryTrust(), ReadMachineChocolateyInstall) { }

    /// <summary>
    /// The machine-wide ChocolateyInstall value, or ProgramData\chocolatey when it's missing or
    /// unusable. ProgramData comes from the Known Folder API, not the overridable %ProgramData%.
    /// </summary>
    public string InstallDirectory
    {
        get
        {
            var value = readMachineChocolateyInstall()?.Trim();
            return IsUsable(value)
                ? value!
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey");
        }
    }

    /// <summary>bin\choco.exe, a shim that starts &lt;dir&gt;\choco.exe, when the install is trusted.</summary>
    public string? ChocoExePath
    {
        get
        {
            var dir = InstallDirectory;
            var bin = Path.Combine(dir, "bin");
            var shim = Path.Combine(bin, "choco.exe");
            var realExe = Path.Combine(dir, "choco.exe");
            if (!File.Exists(shim) || !File.Exists(realExe)) return null;
            try
            {
                return trust.HasFixedLocation(dir)
                    && new[] { dir, bin, shim, realExe }.All(trust.IsAdminOnly)
                    && CheckedRecursively.All(sub => SubtreeIsAdminOnly(Path.Combine(dir, sub)))
                    && CheckedTopLevel.All(sub => !Directory.Exists(Path.Combine(dir, sub)) || trust.IsAdminOnly(Path.Combine(dir, sub)))
                    ? shim
                    : null;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return null; // couldn't look everywhere → not trusted
            }
        }
    }

    private bool SubtreeIsAdminOnly(string folder)
        => !Directory.Exists(folder)
           || (trust.IsAdminOnly(folder)
               && Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.AllDirectories).All(trust.IsAdminOnly));

    /// <summary>
    /// Reads ChocolateyInstall from the machine environment in the registry. Not from the process
    /// environment, where a user variable of the same name would override it.
    /// </summary>
    public static string? ReadMachineChocolateyInstall()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MachineEnvironmentKey);
            // Unexpanded: expanding would substitute this process's (user-overridable) variables.
            return key?.GetValue("ChocolateyInstall", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A local, absolute path written out in full: no %variables%, no relative path, no UNC or
    // device path (a remote share's "Administrators" isn't this machine's).
    private static bool IsUsable(string? value)
        => !string.IsNullOrEmpty(value) && !value.Contains('%') && !value.StartsWith(@"\\", StringComparison.Ordinal)
           && Path.IsPathFullyQualified(value);
}
