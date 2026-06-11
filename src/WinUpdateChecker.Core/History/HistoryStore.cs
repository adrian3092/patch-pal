using WinUpdateChecker.Core.Persistence;

namespace WinUpdateChecker.Core.History;

/// <summary>
/// Append-only upgrade log at %APPDATA%\WinUpdateChecker\history.json.
/// Pass a directory for tests. Thread-safe: batch upgrades append from parallel
/// per-source loops.
/// </summary>
public sealed class HistoryStore(string? directory = null)
{
    private readonly string _path = Path.Combine(
        directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinUpdateChecker"),
        "history.json");

    private readonly object _gate = new();

    public IReadOnlyList<HistoryEntry> Load()
    {
        lock (_gate) return JsonFileStore.Load<List<HistoryEntry>>(_path, () => []);
    }

    public void Append(HistoryEntry entry)
    {
        lock (_gate)
        {
            var all = JsonFileStore.Load<List<HistoryEntry>>(_path, () => []);
            all.Add(entry);
            JsonFileStore.Save(_path, all);
        }
    }

    public void Clear()
    {
        lock (_gate) JsonFileStore.Save(_path, new List<HistoryEntry>());
    }
}
