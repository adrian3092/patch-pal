using System.Diagnostics;
using System.Text.Json;

namespace PatchPal.Core.Persistence;

/// <summary>
/// Loads and saves a JSON state file. A missing file reads as the default, and so does a corrupt
/// one, after it is set aside as a timestamped *.bak: bad state must never stop the app. A file
/// that exists but can't be read (locked by another program, or access denied) throws instead,
/// because reading it as empty and saving over it would destroy it. Saves replace the file whole,
/// so a crash in the middle of one can't leave half a file behind.
/// </summary>
public static class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static readonly TimeSpan ReplaceTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReplaceRetryDelay = TimeSpan.FromMilliseconds(100);

    public static T Load<T>(string path, Func<T> createDefault)
    {
        if (!File.Exists(path)) return createDefault();
        var json = File.ReadAllText(path);
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? createDefault();
        }
        catch (JsonException)
        {
            BackupCorruptFile(path);
            return createDefault();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // Write the new version beside the old one, flush it to disk, then swap it in with a rename.
        var temp = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(flushToDisk: true);
            }
            Replace(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    // Antivirus, search indexers and backup tools open a freshly written file for a moment, and the
    // swap fails while one has it; wait them out for a couple of seconds before giving up.
    private static void Replace(string source, string destination)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && waited.Elapsed < ReplaceTimeout)
            {
                Thread.Sleep(ReplaceRetryDelay);
            }
        }
    }

    private static void BackupCorruptFile(string path)
    {
        try
        {
            File.Copy(path, UnusedBackupPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The backup is best-effort: the app starts fresh either way.
        }
    }

    // history.json -> history.json.20261109-153000.bak, or ...-2.bak, -3.bak when that one exists.
    private static string UnusedBackupPath(string path)
    {
        var stem = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}";
        var candidate = $"{stem}.bak";
        for (var n = 2; File.Exists(candidate); n++)
            candidate = $"{stem}-{n}.bak";
        return candidate;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover *.tmp is harmless; the original error is the one worth reporting.
        }
    }
}
