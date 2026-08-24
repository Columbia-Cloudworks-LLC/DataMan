using System.Collections.Concurrent;
using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DataMan.Core.Ingestion;

public sealed class SourceReconciler
{
    private readonly LibraryRepository _library;
    private readonly ILogger<SourceReconciler> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public SourceReconciler(LibraryRepository library, ILogger<SourceReconciler> logger)
    {
        _library = library;
        _logger = logger;
    }

    public async Task ReconcileAsync(string sourceId, string rootPath, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(sourceId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Reconcile(sourceId, rootPath, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private void Reconcile(string sourceId, string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diskPaths = ListDiskFiles(Path.GetFullPath(rootPath));
        var libraryByPath = new Dictionary<string, ItemRecord>(StringComparer.OrdinalIgnoreCase);
        var liveHashes = new HashSet<string>(StringComparer.Ordinal);
        var unmatchedLibrary = new List<ItemRecord>();

        foreach (var item in _library.ListFileItemsBySource(sourceId))
        {
            if (!TryLocatorPath(item.LocatorJson, out var path))
            {
                _logger.LogError("Skipping item {ItemId}: locator is not a file path.", item.ItemId);
                continue;
            }

            libraryByPath[path] = item;
            if (diskPaths.Contains(path))
            {
                if (item.Status != ItemStatus.Active)
                {
                    _library.UpdateItemStatus(item.ItemId, ItemStatus.Active);
                }

                if (!string.IsNullOrWhiteSpace(item.OriginalHash))
                {
                    liveHashes.Add(item.OriginalHash);
                }

                continue;
            }

            unmatchedLibrary.Add(item);
        }

        var unmatchedDisk = new List<string>();
        foreach (var path in diskPaths)
        {
            if (!libraryByPath.ContainsKey(path))
            {
                unmatchedDisk.Add(path);
            }
        }

        var rematched = ApplyUniqueHashMatches(
            sourceId,
            unmatchedDisk,
            unmatchedLibrary,
            liveHashes,
            cancellationToken);

        foreach (var item in unmatchedLibrary)
        {
            if (rematched.Contains(item.ItemId) || item.Status == ItemStatus.Missing)
            {
                continue;
            }

            _library.UpdateItemStatus(item.ItemId, ItemStatus.Missing);
        }

        _library.TouchLastScanAt(sourceId);
    }

    private HashSet<string> ApplyUniqueHashMatches(
        string sourceId,
        IReadOnlyList<string> unmatchedDisk,
        IReadOnlyList<ItemRecord> unmatchedLibrary,
        HashSet<string> liveHashes,
        CancellationToken cancellationToken)
    {
        var libraryByHash = new Dictionary<string, List<ItemRecord>>(StringComparer.Ordinal);
        foreach (var item in unmatchedLibrary)
        {
            if (string.IsNullOrWhiteSpace(item.OriginalHash))
            {
                continue;
            }

            if (!libraryByHash.TryGetValue(item.OriginalHash, out var items))
            {
                items = [];
                libraryByHash[item.OriginalHash] = items;
            }

            items.Add(item);
        }

        if (libraryByHash.Count == 0)
        {
            return [];
        }

        var expectedSizes = unmatchedLibrary
            .Where(item => !string.IsNullOrWhiteSpace(item.OriginalHash))
            .Where(item => item.SizeBytes.HasValue)
            .Select(item => item.SizeBytes!.Value)
            .ToHashSet();
        var diskByHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in unmatchedDisk)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!expectedSizes.Contains(new FileInfo(path).Length))
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Unable to inspect {Path} during reconcile.", path);
                continue;
            }

            string hash;
            try
            {
                hash = ContentHasher.Sha256File(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hash {Path} during reconcile.", path);
                continue;
            }

            if (!diskByHash.TryGetValue(hash, out var paths))
            {
                paths = [];
                diskByHash[hash] = paths;
            }

            paths.Add(path);
        }

        var rematched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (hash, diskPaths) in diskByHash)
        {
            if (liveHashes.Contains(hash))
            {
                continue;
            }

            if (!libraryByHash.TryGetValue(hash, out var items))
            {
                continue;
            }

            if (diskPaths.Count == 1 && items.Count == 1)
            {
                var item = items[0];
                var newPath = diskPaths[0];
                _library.UpdateItemLocation(
                    item.ItemId,
                    new FileLocator { Path = newPath }.ToJson(),
                    Path.GetFileName(newPath),
                    ItemStatus.Active);
                rematched.Add(item.ItemId);
                continue;
            }

            _logger.LogError(
                "Ambiguous file identity for hash {Hash} in source {SourceId}: {DiskCount} disk path(s), {LibraryCount} unmatched item(s).",
                hash,
                sourceId,
                diskPaths.Count,
                items.Count);
        }

        return rematched;
    }

    private static HashSet<string> ListDiskFiles(string root)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
        {
            return paths;
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };
            foreach (var file in Directory.EnumerateFiles(root, "*", options))
            {
                paths.Add(Path.GetFullPath(file));
            }
        }
        catch (IOException)
        {
            // Preserve files discovered before a transient or invalid subtree failed.
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible portions of the root.
        }

        return paths;
    }

    private static bool TryLocatorPath(string locatorJson, out string path)
    {
        try
        {
            path = Path.GetFullPath(FileLocator.Parse(locatorJson).Path);
            return true;
        }
        catch (Exception)
        {
            path = string.Empty;
            return false;
        }
    }
}
