namespace PatchPal.Core.Persistence;

/// <summary>
/// Resolves the app-data root (%APPDATA%\PatchPal). The app shipped as
/// "WinUpdateChecker" before the 2.0.0 rename, so an existing legacy folder is
/// moved once to carry the user's settings and update history across.
/// </summary>
public static class AppDataPaths
{
    public const string FolderName = "PatchPal";
    public const string LegacyFolderName = "WinUpdateChecker";

    public static string ResolveRoot() =>
        ResolveRoot(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string ResolveRoot(string appDataBase)
    {
        var root = Path.Combine(appDataBase, FolderName);
        var legacy = Path.Combine(appDataBase, LegacyFolderName);
        if (!Directory.Exists(root) && Directory.Exists(legacy))
        {
            try
            {
                Directory.Move(legacy, root);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A locked or inaccessible legacy folder must not block startup;
                // the app starts fresh and the old data stays where it was.
            }
        }
        return root;
    }
}
