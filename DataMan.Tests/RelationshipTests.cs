using DataMan.Contracts;
using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Search;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DataMan.Tests;

public sealed class RelationshipTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly AppDatabase _database;
    private readonly LibraryRepository _library;
    private readonly IngestionOrchestrator _orchestrator;
    private readonly IItemWriter _writer;

    public RelationshipTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dataman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "dataman.db");

        _services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath)
            .AddSingleton<ITextEmbedder, DeterministicEmbedder>()
            .BuildServiceProvider();

        _database = _services.GetRequiredService<AppDatabase>();
        _database.Initialize();
        _library = _services.GetRequiredService<LibraryRepository>();
        _orchestrator = _services.GetRequiredService<IngestionOrchestrator>();
        _writer = _services.GetRequiredService<IItemWriter>();
    }

    [Fact]
    public async Task Nested_ingest_mirrors_contains_from_notes_and_corpus()
    {
        var folder = Path.Combine(_root, "corpus");
        Directory.CreateDirectory(Path.Combine(folder, "notes"));
        await File.WriteAllTextAsync(Path.Combine(folder, "notes", "alpha.md"), "quokka");

        await _orchestrator.IngestPathsAsync([folder]);
        var file = Assert.Single(_library.ListItems());
        var notes = _library.GetItem(file.ParentItemId!)!.Item;
        var corpus = _library.GetItem(notes.ParentItemId!)!.Item;

        Assert.Equal(file.ItemId, Assert.Single(_library.GetRelatedItems(notes.ItemId, LibraryRepository.ContainsRelation)).ItemId);
        Assert.Equal(notes.ItemId, Assert.Single(_library.GetRelatedItems(corpus.ItemId, LibraryRepository.ContainsRelation)).ItemId);
        Assert.Empty(_library.GetRelatedItems(file.ItemId, LibraryRepository.ContainsRelation));
    }

    [Fact]
    public async Task Reingest_same_tree_does_not_grow_contains_rows()
    {
        var folder = Path.Combine(_root, "corpus");
        Directory.CreateDirectory(Path.Combine(folder, "notes"));
        await File.WriteAllTextAsync(Path.Combine(folder, "notes", "alpha.md"), "quokka");

        await _orchestrator.IngestPathsAsync([folder]);
        var afterFirst = CountContains();
        Assert.True(afterFirst > 0);

        await _orchestrator.IngestPathsAsync([folder]);
        Assert.Equal(afterFirst, CountContains());
    }

    [Fact]
    public async Task Upsert_new_parent_replaces_inbound_contains()
    {
        var folder = Path.Combine(_root, "corpus");
        Directory.CreateDirectory(Path.Combine(folder, "a"));
        Directory.CreateDirectory(Path.Combine(folder, "b"));
        await File.WriteAllTextAsync(Path.Combine(folder, "a", "doc.md"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(folder, "b", "other.md"), "beta");

        await _orchestrator.IngestPathsAsync([folder]);
        var files = _library.ListItems();
        var doc = Assert.Single(files, item => item.Title == "doc.md");
        var other = Assert.Single(files, item => item.Title == "other.md");
        var folderA = _library.GetItem(doc.ParentItemId!)!.Item;
        var folderB = _library.GetItem(other.ParentItemId!)!.Item;

        var itemId = await _writer.UpsertItemAsync(
            new ItemDraft
            {
                SourceId = doc.SourceId,
                ParentItemId = folderB.ItemId,
                Kind = doc.Kind,
                Subtype = doc.Subtype,
                Title = doc.Title,
                ContentHash = doc.ContentHash,
                OriginalHash = doc.OriginalHash,
                LocatorJson = doc.LocatorJson,
                MimeType = doc.MimeType,
                SizeBytes = doc.SizeBytes,
                Status = doc.Status
            },
            content: null,
            CancellationToken.None);

        Assert.Equal(doc.ItemId, itemId);
        Assert.DoesNotContain(
            _library.GetRelatedItems(folderA.ItemId, LibraryRepository.ContainsRelation),
            item => item.ItemId == doc.ItemId);
        Assert.Contains(
            _library.GetRelatedItems(folderB.ItemId, LibraryRepository.ContainsRelation),
            item => item.ItemId == doc.ItemId);
    }

    [Fact]
    public async Task GetRelatedItems_unknown_type_is_empty()
    {
        var folder = Path.Combine(_root, "corpus");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "alpha.md"), "quokka");

        await _orchestrator.IngestPathsAsync([folder]);
        var file = Assert.Single(_library.ListItems());

        Assert.Empty(_library.GetRelatedItems(file.ItemId, "derived-from"));
        Assert.Empty(_library.GetRelatedItems(Guid.NewGuid().ToString("D")));
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

    private int CountContains()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM relationships
            WHERE relation_type = $type;
            """;
        command.Parameters.AddWithValue("$type", LibraryRepository.ContainsRelation);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
