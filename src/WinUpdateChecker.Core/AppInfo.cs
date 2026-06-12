using System.Reflection;

namespace WinUpdateChecker.Core;

public static class AppInfo
{
    public const string ProductName = "WinUpdateChecker";
    public const string RepoUrl = "https://github.com/adrian3092/win-update-checker";

    /// <summary>From the assembly's InformationalVersion (csproj &lt;Version&gt;, CI -p:Version),
    /// with any "+commit" build-metadata suffix stripped.</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? "0.0.0-dev";
}
