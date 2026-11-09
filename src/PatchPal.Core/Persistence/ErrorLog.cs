namespace PatchPal.Core.Persistence;

/// <summary>
/// The record of errors nothing else handled: %APPDATA%\PatchPal\error.log, with the time, the
/// app version, what was going on, and the full exception. A log that outgrows its limit is
/// moved to error.log.old and a new one is started.
/// </summary>
public static class ErrorLog
{
    public const string FileName = "error.log";
    private const long DefaultMaxBytes = 1024 * 1024;

    /// <summary>Records the error in %APPDATA%\PatchPal. Returns the log's path, or null when it couldn't be written.</summary>
    public static string? Write(Exception error, string context)
        => Write(AppDataPaths.ResolveRoot(), error, context, DateTimeOffset.Now);

    public static string? Write(string directory, Exception error, string context, DateTimeOffset now, long maxBytes = DefaultMaxBytes)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            if (File.Exists(path) && new FileInfo(path).Length > maxBytes)
                File.Move(path, path + ".old", overwrite: true);

            var newLine = Environment.NewLine;
            File.AppendAllText(path,
                $"[{now:yyyy-MM-dd HH:mm:ss zzz}] {AppInfo.ProductName} {AppInfo.Version} - {context}{newLine}{error}{newLine}{newLine}");
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // This runs where nothing else can handle a second error, so report "not written" instead.
            return null;
        }
    }
}
