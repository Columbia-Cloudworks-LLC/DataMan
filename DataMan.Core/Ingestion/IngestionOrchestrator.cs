using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Extensions.Logging;

namespace DataMan.Core.Ingestion;

public sealed class IngestionOrchestrator
{
    private const long MaxFileBytes = 32L * 1024 * 1024;

    private readonly LibraryRepository _library;
    private readonly IItemWriter _writer;
    private readonly PluginRegistry _plugins;
    private readonly IServiceProvider _services;
    private readonly WatchedRootMonitor _monitor;
    private readonly ILogger<IngestionOrchestrator> _logger;

    public IngestionOrchestrator(
        LibraryRepository library,
        IItemWriter writer,
        PluginRegistry plugins,
        IServiceProvider services,
        WatchedRootMonitor monitor,
        ILogger<IngestionOrchestrator> logger)
    {
        _library = library;
        _writer = writer;
        _plugins = plugins;
        _services = services;
        _monitor = monitor;
        _logger = logger;
    }

    public async Task<BatchIngestionResult> IngestPathsAsync(
        IEnumerable<string> paths,
        IProgress<IngestionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = ExpandFiles(paths);
        var itemIds = new List<string>();
        var errors = new List<string>();
        var skipped = 0;
        var failed = 0;
        var completed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new IngestionProgress
            {
                CurrentPath = file.FullPath,
                Completed = completed,
                Total = files.Count
            });

            var plugin = _plugins.FindByExtension(file.Extension);
            if (plugin is null)
            {
                skipped++;
                completed++;
                continue;
            }

            if (file.SizeBytes > MaxFileBytes)
            {
                skipped++;
                completed++;
                errors.Add($"{file.FullPath}: larger than 32 MB.");
                continue;
            }

            try
            {
                var sourceId = _library.EnsureLocalSource(file.SourceDisplayName, file.SourceRoot);
                var parentId = await EnsureFolderChainAsync(sourceId, file.SourceRoot, file.FullPath, cancellationToken);
                var originalHash = ContentHasher.Sha256File(file.FullPath);
                var info = new FileInfo(file.FullPath);

                await using var stream = File.OpenRead(file.FullPath);
                var result = await plugin.IngestAsync(
                    new IngestionContext
                    {
                        SourceId = sourceId,
                        LocatorJson = new FileLocator { Path = file.FullPath }.ToJson(),
                        ContentStream = stream,
                        LocalPath = file.FullPath,
                        ParentItemId = parentId,
                        OriginalHash = originalHash,
                        SizeBytes = file.SizeBytes,
                        SourceCreatedAt = info.CreationTimeUtc.ToString("O"),
                        SourceUpdatedAt = info.LastWriteTimeUtc.ToString("O"),
                        Progress = progress,
                        Writer = _writer,
                        Services = _services
                    },
                    cancellationToken);

                if (result.Success)
                {
                    itemIds.AddRange(result.CreatedItemIds);
                }
                else
                {
                    failed++;
                    errors.Add($"{file.FullPath}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{file.FullPath}: {ex.Message}");
                _logger.LogError(ex, "Failed to ingest {Path}", file.FullPath);
            }

            completed++;
        }

        progress?.Report(new IngestionProgress
        {
            CurrentPath = string.Empty,
            Completed = completed,
            Total = files.Count
        });

        WatchFolderRoots(paths);

        return new BatchIngestionResult
        {
            Accepted = itemIds.Count,
            Skipped = skipped,
            Failed = failed,
            ItemIds = itemIds,
            Errors = errors
        };
    }

    private async Task<string?> EnsureFolderChainAsync(
        string sourceId,
        string? sourceRoot,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var root = Path.GetFullPath(sourceRoot);
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var folders = new List<string> { root };
        if (!string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
        {
            var current = root;
            foreach (var part in Path.GetRelativePath(root, directory)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                folders.Add(current);
            }
        }

        string? parentId = null;
        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            parentId = await _writer.UpsertItemAsync(
                new ItemDraft
                {
                    SourceId = sourceId,
                    ParentItemId = parentId,
                    Kind = "folder",
                    Title = string.IsNullOrEmpty(name) ? folder : name,
                    LocatorJson = new FileLocator { Path = folder, IsDirectory = true }.ToJson()
                },
                content: null,
                cancellationToken);
        }

        return parentId;
    }

    private void WatchFolderRoots(IEnumerable<string> paths)
    {
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var path = Path.GetFullPath(raw);
            if (!Directory.Exists(path))
            {
                continue;
            }

            var sourceId = _library.EnsureLocalSource(
                Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
                path);
            _library.MarkSourceWatched(sourceId);
            _monitor.Watch(sourceId, path);
        }
    }

    private static List<DiscoveredFile> ExpandFiles(IEnumerable<string> paths)
    {
        var files = new List<DiscoveredFile>();
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var path = Path.GetFullPath(raw);
            if (File.Exists(path))
            {
                files.Add(new DiscoveredFile(path, Path.GetDirectoryName(path), Path.GetFileName(Path.GetDirectoryName(path)) ?? "Dropped files"));
            }
            else if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    files.Add(new DiscoveredFile(file, path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))));
                }
            }
        }

        return files;
    }

    private sealed record DiscoveredFile(string FullPath, string? SourceRoot, string SourceDisplayName)
    {
        public string Extension => Path.GetExtension(FullPath);
        public long SizeBytes => new FileInfo(FullPath).Length;
    }
}
