using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DataMan.Core.Ingestion;

public sealed class WatchedRootMonitor : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(60);

    private readonly SourceReconciler _reconciler;
    private readonly ILogger<WatchedRootMonitor> _logger;
    private readonly ConcurrentDictionary<string, WatchedRoot> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _debounceGenerations = new(StringComparer.Ordinal);
    private bool _disposed;

    public WatchedRootMonitor(SourceReconciler reconciler, ILogger<WatchedRootMonitor> logger)
    {
        _reconciler = reconciler;
        _logger = logger;
    }

    public void Watch(string sourceId, string rootPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            return;
        }

        _roots.GetOrAdd(root, _ => Start(sourceId, root));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var watched in _roots.Values)
        {
            watched.Dispose();
        }

        _roots.Clear();
    }

    private WatchedRoot Start(string sourceId, string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime
        };
        watcher.Changed += (_, _) => Schedule(sourceId, root);
        watcher.Created += (_, _) => Schedule(sourceId, root);
        watcher.Deleted += (_, _) => Schedule(sourceId, root);
        watcher.Renamed += (_, _) => Schedule(sourceId, root);
        watcher.Error += (_, args) =>
            _logger.LogError(args.GetException(), "Watcher error for {Root}.", root);
        watcher.EnableRaisingEvents = true;

        var cts = new CancellationTokenSource();
        var timer = new PeriodicTimer(Period);
        _ = RunPeriodicAsync(sourceId, root, timer, cts.Token);
        return new WatchedRoot(watcher, timer, cts);
    }

    private async void Schedule(string sourceId, string root)
    {
        var generation = _debounceGenerations.AddOrUpdate(sourceId, 1, static (_, value) => value + 1);
        try
        {
            await Task.Delay(Debounce).ConfigureAwait(false);
            if (_disposed
                || !_debounceGenerations.TryGetValue(sourceId, out var current)
                || current != generation)
            {
                return;
            }

            await _reconciler.ReconcileAsync(sourceId, root).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconcile failed for {Root}.", root);
        }
    }

    private async Task RunPeriodicAsync(
        string sourceId,
        string root,
        PeriodicTimer timer,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await _reconciler.ReconcileAsync(sourceId, root, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Periodic reconcile failed for {Root}.", root);
        }
    }

    private sealed class WatchedRoot : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly PeriodicTimer _timer;
        private readonly CancellationTokenSource _cts;

        public WatchedRoot(FileSystemWatcher watcher, PeriodicTimer timer, CancellationTokenSource cts)
        {
            _watcher = watcher;
            _timer = timer;
            _cts = cts;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _timer.Dispose();
            _cts.Dispose();
        }
    }
}
