using WinUpdateChecker.Core.History;

namespace WinUpdateChecker.Core.Upgrading;

/// <summary>One queued upgrade: where it goes and what versions it moves between.</summary>
public sealed record BatchItem(string Source, string PackageId, string Name, string FromVersion, string ToVersion);

/// <summary>
/// Runs a batch of upgrades: sequential within each source (a package manager cannot
/// run concurrently with itself), parallel across sources. Every attempt — success or
/// failure — is appended to History, and a failure never blocks the rest of the batch.
/// Callbacks fire on worker threads; UI callers must marshal to the dispatcher.
/// </summary>
public sealed class BatchUpgradeRunner(UpgradeRunner runner, HistoryStore history)
{
    public async Task<IReadOnlyList<UpgradeResult>> RunAsync(
        IReadOnlyList<BatchItem> items,
        Action<BatchItem>? onItemStarted = null,
        Action<BatchItem, UpgradeResult>? onItemFinished = null,
        CancellationToken ct = default)
    {
        var results = new List<UpgradeResult>();
        var gate = new object();

        await Task.WhenAll(items.GroupBy(i => i.Source).Select(async group =>
        {
            foreach (var item in group)
            {
                ct.ThrowIfCancellationRequested();
                onItemStarted?.Invoke(item);
                var result = await runner.UpgradeAsync(item.Source, item.PackageId, item.Name, ct);
                history.Append(new HistoryEntry(
                    DateTimeOffset.Now, item.Name, item.PackageId, item.Source,
                    item.FromVersion, item.ToVersion, result.Success, result.Message, result.Log));
                lock (gate) results.Add(result);
                onItemFinished?.Invoke(item, result);
            }
        }));

        return results;
    }
}
