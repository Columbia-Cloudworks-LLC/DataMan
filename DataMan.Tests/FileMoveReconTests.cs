using DataMan.Contracts;
using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DataMan.Tests;

public sealed class FileMoveReconTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly LibraryRepository _library;
    private readonly IngestionOrchestrator _orchestrator;
    private readonly SourceReconciler _reconciler;

    public FileMoveReconTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dataman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "dataman.db");

        _services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath)
            .BuildServiceProvider();

        _services.GetRequiredService<AppDatabase>().Initialize();
        _library = _services.GetRequiredService<LibraryRepository>();
        _orchestrator = _services.GetRequiredService<IngestionOrchestrator>();
        _reconciler = _services.GetRequiredService<SourceReconciler>();
    }

    [Fact]
    public async Task Rename_on_disk_keeps_item_id_and_updates_locator()
    {
        var folder = Path.Combine(_root, "rename");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "keep-id.md");
        await File.WriteAllTextAsync(original, "stable body token echidna");

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        Assert.Single(ingested.ItemIds);
        var itemId = ingested.ItemIds[0];
        var before = _library.GetItem(itemId)!;

        var renamed = Path.Combine(folder, "renamed.md");
        File.Move(original, renamed);

        await _reconciler.ReconcileAsync(before.Item.SourceId, folder);

        var after = _library.GetItem(itemId)!;
        Assert.Equal(itemId, after.Item.ItemId);
        Assert.Equal(Path.GetFullPath(renamed), FileLocator.Parse(after.Item.LocatorJson).Path);
        Assert.Equal(ItemStatus.Active, after.Item.Status);
        Assert.Equal("renamed.md", after.Item.Title);
        Assert.Contains("echidna", after.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_on_disk_marks_missing_and_keeps_content()
    {
        var folder = Path.Combine(_root, "delete");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "gone.md");
        await File.WriteAllTextAsync(path, "searchable numbat remains");

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        var itemId = Assert.Single(ingested.ItemIds);
        var before = _library.GetItem(itemId)!;

        File.Delete(path);
        await _reconciler.ReconcileAsync(before.Item.SourceId, folder);

        var after = _library.GetItem(itemId)!;
        Assert.Equal(itemId, after.Item.ItemId);
        Assert.Equal(ItemStatus.Missing, after.Item.Status);
        Assert.Equal(Path.GetFullPath(path), FileLocator.Parse(after.Item.LocatorJson).Path);
        Assert.Equal("searchable numbat remains", after.Body);
        Assert.Single(_library.Search("numbat"));
        Assert.Equal(1, _library.GetStats().ItemCount);
    }

    [Fact]
    public async Task Move_under_same_root_keeps_item_id_and_updates_locator()
    {
        var folder = Path.Combine(_root, "move");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "origin.md");
        await File.WriteAllTextAsync(original, "moved quoll");

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        var itemId = Assert.Single(ingested.ItemIds);
        var before = _library.GetItem(itemId)!;

        var nested = Path.Combine(folder, "nested");
        Directory.CreateDirectory(nested);
        var dest = Path.Combine(nested, "parked.md");
        File.Move(original, dest);

        await _reconciler.ReconcileAsync(before.Item.SourceId, folder);

        var after = _library.GetItem(itemId)!;
        Assert.Equal(itemId, after.Item.ItemId);
        Assert.Equal(Path.GetFullPath(dest), FileLocator.Parse(after.Item.LocatorJson).Path);
        Assert.Equal(ItemStatus.Active, after.Item.Status);
        Assert.Equal("parked.md", after.Item.Title);
    }

    [Fact]
    public async Task Periodic_reconcile_without_watcher_updates_moved_path()
    {
        var folder = Path.Combine(_root, "periodic");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "scan-me.md");
        await File.WriteAllTextAsync(original, "timer path dingo");

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        var itemId = Assert.Single(ingested.ItemIds);
        var sourceId = _library.GetItem(itemId)!.Item.SourceId;

        var dest = Path.Combine(folder, "scanned.md");
        File.Move(original, dest);

        await _reconciler.ReconcileAsync(sourceId, folder);
        await _reconciler.ReconcileAsync(sourceId, folder);

        var after = _library.GetItem(itemId)!;
        Assert.Equal(itemId, after.Item.ItemId);
        Assert.Equal(Path.GetFullPath(dest), FileLocator.Parse(after.Item.LocatorJson).Path);
        Assert.Equal(ItemStatus.Active, after.Item.Status);
        Assert.Equal(1, _library.GetStats().ItemCount);
    }

    [Fact]
    public async Task Duplicate_hashes_do_not_merge_and_ambiguous_rematch_is_skipped()
    {
        var folder = Path.Combine(_root, "dupes");
        Directory.CreateDirectory(folder);
        const string body = "identical bytes wallaby";
        var a = Path.Combine(folder, "a.md");
        var b = Path.Combine(folder, "b.md");
        await File.WriteAllTextAsync(a, body);
        await File.WriteAllTextAsync(b, body);

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        Assert.Equal(2, ingested.ItemIds.Count);
        var idA = ingested.ItemIds.Single(id => PathEquals(_library.GetItem(id)!.Item.LocatorJson, a));
        var idB = ingested.ItemIds.Single(id => PathEquals(_library.GetItem(id)!.Item.LocatorJson, b));
        Assert.NotEqual(idA, idB);
        var sourceId = _library.GetItem(idA)!.Item.SourceId;

        await _reconciler.ReconcileAsync(sourceId, folder);
        Assert.Equal(2, _library.GetStats().ItemCount);
        Assert.Equal(idA, _library.GetItem(idA)!.Item.ItemId);
        Assert.Equal(idB, _library.GetItem(idB)!.Item.ItemId);
        Assert.Equal(ItemStatus.Active, _library.GetItem(idA)!.Item.Status);
        Assert.Equal(ItemStatus.Active, _library.GetItem(idB)!.Item.Status);

        File.Delete(a);
        var c = Path.Combine(folder, "c.md");
        var d = Path.Combine(folder, "d.md");
        await File.WriteAllTextAsync(c, body);
        await File.WriteAllTextAsync(d, body);

        await _reconciler.ReconcileAsync(sourceId, folder);

        var missing = _library.GetItem(idA)!;
        Assert.Equal(ItemStatus.Missing, missing.Item.Status);
        Assert.True(PathEquals(missing.Item.LocatorJson, a));
        Assert.Equal(ItemStatus.Active, _library.GetItem(idB)!.Item.Status);
        Assert.Equal(2, _library.GetStats().ItemCount);
        Assert.DoesNotContain(_library.ListItems(), item => PathEquals(item.LocatorJson, c));
        Assert.DoesNotContain(_library.ListItems(), item => PathEquals(item.LocatorJson, d));
    }

    [Fact]
    public async Task Copy_of_active_file_is_not_treated_as_move()
    {
        var folder = Path.Combine(_root, "copy");
        Directory.CreateDirectory(folder);
        var original = Path.Combine(folder, "source.md");
        await File.WriteAllTextAsync(original, "copied bilby");

        var ingested = await _orchestrator.IngestPathsAsync([folder]);
        var itemId = Assert.Single(ingested.ItemIds);
        var sourceId = _library.GetItem(itemId)!.Item.SourceId;

        File.Copy(original, Path.Combine(folder, "clone.md"));
        await _reconciler.ReconcileAsync(sourceId, folder);

        var after = _library.GetItem(itemId)!;
        Assert.Equal(itemId, after.Item.ItemId);
        Assert.True(PathEquals(after.Item.LocatorJson, original));
        Assert.Equal(ItemStatus.Active, after.Item.Status);
        Assert.Equal(1, _library.GetStats().ItemCount);
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static bool PathEquals(string locatorJson, string path)
    {
        return string.Equals(
            FileLocator.Parse(locatorJson).Path,
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase);
    }
}
