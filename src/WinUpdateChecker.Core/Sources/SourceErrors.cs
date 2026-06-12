namespace WinUpdateChecker.Core.Sources;

internal static class SourceErrors
{
    /// <summary>First non-empty stderr line, or null — keeps warning banners one line long.</summary>
    public static string? FirstLine(string? stdErr)
        => stdErr?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
}
