using DataMan.Contracts;
using DataMan.Core.Hosting;
using DataMan.Core.Search;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DataMan.Tests;

public sealed class SemanticUnavailableTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly LibraryRepository _library;

    public SemanticUnavailableTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dataman-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var dbPath = Path.Combine(_root, "dataman.db");

        _services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath)
            .AddSingleton<ITextEmbedder, UnavailableEmbedder>()
            .BuildServiceProvider();

        _services.GetRequiredService<AppDatabase>().Initialize();
        _library = _services.GetRequiredService<LibraryRepository>();
    }

    [Fact]
    public void Semantic_without_embedder_is_unavailable_not_empty_hits()
    {
        var outcome = _library.Search(
            new LibraryQuery.Semantic(QueryText.Parse("anything")));
        var missing = Assert.IsType<SearchOutcome.SemanticUnavailable>(outcome);
        Assert.Equal(SemanticGap.EmbedderMissing, missing.Gap);
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
