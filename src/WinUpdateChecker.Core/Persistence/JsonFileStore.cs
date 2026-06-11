using System.Text.Json;

namespace WinUpdateChecker.Core.Persistence;

/// <summary>
/// Loads/saves a JSON state file. A corrupt file is backed up to *.bak and replaced
/// with defaults — per the spec, bad state files must never crash the app.
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T Load<T>(string path, Func<T> createDefault)
    {
        if (!File.Exists(path)) return createDefault();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? createDefault();
        }
        catch (JsonException)
        {
            BackupCorruptFile(path);
            return createDefault();
        }
        catch (IOException)
        {
            return createDefault();
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }

    private static void BackupCorruptFile(string path)
    {
        try { File.Copy(path, path + ".bak", overwrite: true); }
        catch (IOException) { /* backup is best-effort */ }
    }
}
