namespace PatchPal.Core.Sources;

/// <summary>
/// Absolute paths to the Windows tools Patch Pal starts. Never launch these by bare name:
/// CreateProcess searches the app and working folders first, and the app runs elevated.
/// </summary>
public static class WindowsTools
{
    public static string WherePath => Path.Combine(Environment.SystemDirectory, "where.exe");

    public static string CmdPath => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    public static string PowerShellPath =>
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    public static string ExplorerPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
}
