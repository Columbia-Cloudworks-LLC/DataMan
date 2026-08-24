using DataMan.Contracts;
using Xunit;
using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataMan.Tests;

public sealed class MvpLoopTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly AppDatabase _database;
    private readonly LibraryRepository _library;
    private readonly IngestionOrchestrator _orchestrator;

    public MvpLoopTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dataman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "dataman.db");

        _services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath)
            .BuildServiceProvider();

        _database = _services.GetRequiredService<AppDatabase>();
        _database.Initialize();
        _library = _services.GetRequiredService<LibraryRepository>();
        _orchestrator = _services.GetRequiredService<IngestionOrchestrator>();
    }

    [Fact]
    public void Initialize_creates_schema_at_version_1()
    {
        Assert.Equal(1, _database.ReadSchemaVersion());
        Assert.True(File.Exists(_database.DatabasePath));

        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name IN ('items','sources','contents','contents_fts');";
        var names = new HashSet<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        using var fts = connection.CreateCommand();
        fts.CommandText = "SELECT name FROM sqlite_master WHERE name = 'contents_fts';";
        Assert.Equal("contents_fts", fts.ExecuteScalar() as string);
        Assert.Contains("items", names);
        Assert.Contains("sources", names);
        Assert.Contains("contents", names);
    }

    [Fact]
    public async Task Ingest_folder_persists_text_and_is_searchable()
    {
        var folder = Path.Combine(_root, "corpus");
        Directory.CreateDirectory(Path.Combine(folder, "notes"));
        await File.WriteAllTextAsync(Path.Combine(folder, "notes", "alpha.md"), "# Alpha\n\nThe quokka forages at dusk.");
        await File.WriteAllTextAsync(Path.Combine(folder, "readme.txt"), "Plain notes about the platypus.");
        await File.WriteAllTextAsync(Path.Combine(folder, "trace.log"), "2026-08-23 INFO wombat started");
        await File.WriteAllTextAsync(Path.Combine(folder, "ignore.bin"), "not-a-text-plugin");

        var result = await _orchestrator.IngestPathsAsync([folder]);

        Assert.Equal(3, result.Accepted);
        Assert.True(result.Skipped >= 1);
        Assert.Equal(0, result.Failed);
        Assert.Equal(3, _library.GetStats().ItemCount);

        var hits = _library.Search("quokka");
        Assert.Single(hits);
        Assert.Equal("alpha.md", hits[0].Item.Title);
        Assert.Equal("markdown", hits[0].Item.Subtype);
        Assert.False(string.IsNullOrWhiteSpace(hits[0].Item.OriginalHash));

        var platypus = _library.Search("platypus");
        Assert.Single(platypus);
        Assert.Equal("readme.txt", platypus[0].Item.Title);

        var wombat = _library.Search("wombat");
        Assert.Single(wombat);
        Assert.Equal("trace.log", wombat[0].Item.Title);

        var prefix = _library.Search("quok");
        Assert.Single(prefix);
        Assert.Equal("alpha.md", prefix[0].Item.Title);

        var detail = _library.GetItem(hits[0].Item.ItemId);
        Assert.NotNull(detail);
        Assert.Contains("quokka", detail!.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-text-plugin", detail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reingest_same_path_does_not_duplicate()
    {
        var file = Path.Combine(_root, "repeat.md");
        await File.WriteAllTextAsync(file, "first draft");

        var first = await _orchestrator.IngestPathsAsync([file]);
        await File.WriteAllTextAsync(file, "second draft with bandicoot");
        var second = await _orchestrator.IngestPathsAsync([file]);

        Assert.Equal(first.ItemIds, second.ItemIds);
        Assert.Equal(1, _library.GetStats().ItemCount);
        Assert.Single(_library.Search("bandicoot"));
        Assert.Empty(_library.Search("first"));
    }

    [Fact]
    public async Task Ingest_stores_locator_and_hash_without_embedding_blobs()
    {
        var file = Path.Combine(_root, "payload.txt");
        await File.WriteAllTextAsync(file, "visible text");

        await _orchestrator.IngestPathsAsync([file]);

        var items = _library.ListItems();
        Assert.Single(items);
        var locator = FileLocator.Parse(items[0].LocatorJson);
        Assert.Equal(Path.GetFullPath(file), locator.Path);
        Assert.False(string.IsNullOrWhiteSpace(items[0].OriginalHash));
        Assert.Equal(64, items[0].OriginalHash!.Length);
        Assert.Equal(0, _library.CountEmbeddings());
        Assert.Equal("visible text", _library.GetItem(items[0].ItemId)!.Body);
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
}
