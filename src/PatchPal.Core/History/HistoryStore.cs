using System.Security.Cryptography;
using System.Text;
using PatchPal.Core.Persistence;

namespace PatchPal.Core.History;

/// <summary>
/// Append-only upgrade log at %APPDATA%\PatchPal\history.json. Pass a directory for tests.
/// Each append reads, adds and saves under a lock named for the file, so appends are safe from
/// parallel per-source batch loops and from a second Patch Pal window alike. Reads throw when the
/// file exists but can't be read, rather than passing it off as an empty history.
/// </summary>
public sealed class HistoryStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    private readonly string _path;
    private readonly string _lockName;

    public HistoryStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppDataPaths.ResolveRoot(), "history.json");
        _lockName = LockNameFor(_path);
    }

    public IReadOnlyList<HistoryEntry> Load() => JsonFileStore.Load<List<HistoryEntry>>(_path, () => []);

    public void Append(HistoryEntry entry)
    {
        using var _ = CrossProcessLock.Acquire(_lockName, LockTimeout);
        var all = JsonFileStore.Load<List<HistoryEntry>>(_path, () => []);
        all.Add(entry);
        JsonFileStore.Save(_path, all);
    }

    public void Clear()
    {
        using var _ = CrossProcessLock.Acquire(_lockName, LockTimeout);
        JsonFileStore.Save(_path, new List<HistoryEntry>());
    }

    // Every process must derive the same name, and a mutex name can't contain the path's backslashes.
    private static string LockNameFor(string path)
    {
        var key = Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant());
        return @"Local\PatchPal-history-" + Convert.ToHexString(SHA256.HashData(key))[..32];
    }
}
