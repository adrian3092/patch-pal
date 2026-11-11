using System.Diagnostics;

namespace PatchPal.Core.Sources;

/// <summary>
/// Opens links from the elevated app with the user's normal rights. A ShellExecute of a URL from
/// an admin process starts the URL's handler as admin, and that handler is looked up in a registry
/// branch (HKCU\Software\Classes) the user's normal account can change. Handing the URL to
/// explorer.exe makes the running desktop shell open it, at the user's integrity.
/// </summary>
public static class ShellLauncher
{
    public static void OpenUrl(string url, Action<string, string>? start = null)
    {
        // explorer.exe also opens files, folders and switches, so only a plain web link may pass,
        // and nothing in it may close the quotes around the argument.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || url.Any(c => c == '"' || char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new ArgumentException($"Not a web link: {url}", nameof(url));
        }

        (start ?? StartDirect)(WindowsTools.ExplorerPath, $"\"{url}\"");
    }

    // Under the shared start lock: a direct start inherits handles, see ProcessRunner.StartLock.
    private static void StartDirect(string fileName, string arguments)
    {
        lock (ProcessRunner.StartLock)
        {
            using var _ = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false });
        }
    }
}
