using System.Runtime.CompilerServices;
using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Plugins;
using DataMan.Core.Plugins.Internal;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DataMan.Tests;

public sealed class PluginCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dataman-catalog", Guid.NewGuid().ToString("N"));

    public PluginCatalogTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Missing_directory_yields_built_ins_only()
    {
        var missing = Path.Combine(_root, "does-not-exist");
        using var first = PluginCatalog.Load(missing, BuiltInIngestionPlugins.CreateAll());
        using var second = PluginCatalog.Load(missing, BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, first.Listings.Count);
        Assert.Empty(first.Issues);
        Assert.NotNull(first.FindByExtension(".md"));
        Assert.Null(first.FindByExtension(".csv"));
        Assert.Equal(first.Listings.Select(plugin => plugin.Id), second.Listings.Select(plugin => plugin.Id));
    }

    [Fact]
    public void Unreadable_manifest_is_an_issue_and_built_ins_remain()
    {
        var plugins = Path.Combine(_root, "locked");
        var folder = Path.Combine(plugins, "bad");
        Directory.CreateDirectory(folder);
        var manifest = Path.Combine(folder, "plugin.json");
        File.WriteAllText(
            manifest,
            """
            {
              "id": "locked",
              "version": "1.0.0",
              "assembly": "locked.dll",
              "entryType": "Locked"
            }
            """);

        using var hold = new FileStream(manifest, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var catalog = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, catalog.Listings.Count);
        Assert.Contains(
            catalog.Issues,
            issue => issue.Kind == CatalogIssueKind.InvalidManifest && issue.Path == manifest);
    }

    [Fact]
    public void Flatten_expands_nested_bundles_and_rejects_cycles()
    {
        var csv = new PluginUnit(
            "csv",
            "csv/plugin.json",
            "1.0.0",
            "csv.dll",
            "Csv",
            [".csv"],
            []);
        var inner = new BundleUnit("inner", "inner/bundle.json", "Inner", ["csv"], []);
        var outer = new BundleUnit("outer", "outer/bundle.json", "Outer", [], ["inner"]);
        var index = new Dictionary<string, ManifestUnit>
        {
            [csv.Id] = csv,
            [inner.Id] = inner,
            [outer.Id] = outer
        };

        var flat = Assert.IsType<FlattenOk>(BundleFlattener.Flatten(outer, index));
        Assert.Equal(["csv"], flat.Plugins.Select(plugin => plugin.Id));

        var a = new BundleUnit("a", "a/bundle.json", "A", [], ["b"]);
        var b = new BundleUnit("b", "b/bundle.json", "B", [], ["a"]);
        var cyclicIndex = new Dictionary<string, ManifestUnit>
        {
            [a.Id] = a,
            [b.Id] = b
        };
        var cycle = Assert.IsType<FlattenCycle>(BundleFlattener.Flatten(a, cyclicIndex));
        Assert.Contains("a", cycle.Path);
        Assert.Contains("b", cycle.Path);
    }

    [Fact]
    public void Cyclic_bundle_is_an_issue_and_built_ins_remain()
    {
        WriteBundle(Path.Combine(_root, "plugins", "a"), "a", [], ["b"]);
        WriteBundle(Path.Combine(_root, "plugins", "b"), "b", [], ["a"]);

        using var catalog = PluginCatalog.Load(Path.Combine(_root, "plugins"), BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, catalog.Listings.Count);
        var cycles = catalog.Issues.Where(issue => issue.Kind == CatalogIssueKind.Cycle).ToArray();
        Assert.NotEmpty(cycles);
        Assert.All(cycles, issue => Assert.False(string.IsNullOrWhiteSpace(issue.Detail)));
    }

    [Fact]
    public void Nested_bundle_activates_the_resolved_plugin()
    {
        var plugins = Path.Combine(_root, "nested");
        CopySampleCsv(Path.Combine(plugins, "samplecsv"));
        WriteBundle(Path.Combine(plugins, "inner"), "inner", ["dataman.plugin.samplecsv"], []);
        WriteBundle(Path.Combine(plugins, "outer"), "outer", [], ["inner"]);

        using var catalog = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.Contains(catalog.Listings, plugin => plugin.Id == "dataman.plugin.samplecsv");
        Assert.DoesNotContain(catalog.Issues, issue => issue.Kind == CatalogIssueKind.Cycle);
        Assert.NotNull(catalog.FindByExtension(".csv"));
    }

    [Fact]
    public void Cyclic_bundle_does_not_activate_its_member_plugin()
    {
        var plugins = Path.Combine(_root, "cyclic-member");
        CopySampleCsv(Path.Combine(plugins, "samplecsv"));
        WriteBundle(Path.Combine(plugins, "a"), "a", ["dataman.plugin.samplecsv"], ["b"]);
        WriteBundle(Path.Combine(plugins, "b"), "b", [], ["a"]);

        using var catalog = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.DoesNotContain(catalog.Listings, plugin => plugin.Id == "dataman.plugin.samplecsv");
        Assert.Null(catalog.FindByExtension(".csv"));
        Assert.Contains(catalog.Issues, issue => issue.Kind == CatalogIssueKind.Cycle);
    }

    [Fact]
    public async Task Contracts_only_csv_plugin_ingests_through_the_orchestrator()
    {
        var plugins = Path.Combine(_root, "plugins");
        CopySampleCsv(Path.Combine(plugins, "samplecsv"));

        var dbPath = Path.Combine(_root, "dataman.db");
        await using var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath, plugins)
            .BuildServiceProvider();

        services.GetRequiredService<AppDatabase>().Initialize();
        var catalog = services.GetRequiredService<PluginCatalog>();
        Assert.True(catalog.RetainedContextCount >= 1);
        Assert.NotNull(catalog.FindByExtension(".csv"));
        Assert.Contains(catalog.Listings, plugin => plugin.Id == "dataman.plugin.samplecsv");

        var csv = Path.Combine(_root, "rows.csv");
        await File.WriteAllTextAsync(csv, "animal,note\nquokka,forages at dusk\n");
        var result = await services.GetRequiredService<IngestionOrchestrator>().IngestPathsAsync([csv]);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Failed);
        var hits = services.GetRequiredService<LibraryRepository>().Search("quokka");
        Assert.Single(hits);
        Assert.Equal("rows.csv", hits[0].Item.Title);
        Assert.Equal("csv", hits[0].Item.Subtype);
    }

    [Fact]
    public async Task Csv_is_skipped_when_the_sample_plugin_is_absent()
    {
        var dbPath = Path.Combine(_root, "plain.db");
        await using var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath)
            .BuildServiceProvider();

        services.GetRequiredService<AppDatabase>().Initialize();
        var csv = Path.Combine(_root, "ignored.csv");
        await File.WriteAllTextAsync(csv, "x,y\n1,2\n");
        var result = await services.GetRequiredService<IngestionOrchestrator>().IngestPathsAsync([csv]);

        Assert.Equal(0, result.Accepted);
        Assert.True(result.Skipped >= 1);
        Assert.Null(services.GetRequiredService<PluginCatalog>().FindByExtension(".csv"));
    }

    [Fact]
    public async Task Discovered_plugin_unloads_and_stops_resolving()
    {
        var plugins = Path.Combine(_root, "plugins");
        CopySampleCsv(Path.Combine(plugins, "samplecsv"));
        var dbPath = Path.Combine(_root, "unload.db");

        await using var services = new ServiceCollection()
            .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning))
            .AddDataManCore(dbPath, plugins)
            .BuildServiceProvider();

        services.GetRequiredService<AppDatabase>().Initialize();
        var catalog = services.GetRequiredService<PluginCatalog>();

        AssertDiscoveredCsvIsLive(catalog);

        var csv = Path.Combine(_root, "rows.csv");
        await File.WriteAllTextAsync(csv, "animal,note\nquokka,forages at dusk\n");
        Assert.Equal(1, IngestAccepted(services, csv));

        var released = catalog.Release();
        Assert.True(released.ContextsCollected);
        Assert.Equal(0, released.AliveContextCount);
        Assert.True(released.UnloadedContextCount >= 1);
        Assert.Equal(0, catalog.RetainedContextCount);
        Assert.Null(catalog.FindByExtension(".csv"));
        Assert.NotNull(catalog.FindByExtension(".md"));
        Assert.DoesNotContain(catalog.Listings, row => row.Origin == PluginOrigin.Discovered);

        var again = catalog.Release();
        Assert.True(again.ContextsCollected);
        Assert.Equal(released.UnloadedContextCount, again.UnloadedContextCount);
    }

    [Fact]
    public void Built_ins_survive_release_when_nothing_was_discovered()
    {
        var missing = Path.Combine(_root, "does-not-exist");
        using var catalog = PluginCatalog.Load(missing, BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, catalog.Listings.Count);
        Assert.All(catalog.Listings, row => Assert.Equal(PluginOrigin.BuiltIn, row.Origin));
        Assert.Equal(0, catalog.RetainedContextCount);
        Assert.NotNull(catalog.FindByExtension(".md"));
        Assert.Null(catalog.FindByExtension(".csv"));

        var released = catalog.Release();
        Assert.True(released.ContextsCollected);
        Assert.Equal(0, released.UnloadedContextCount);
        Assert.NotNull(catalog.FindByExtension(".md"));
        Assert.Equal(3, catalog.Listings.Count);

        Assert.True(catalog.Release().ContextsCollected);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception)
        {
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertDiscoveredCsvIsLive(PluginCatalog catalog)
    {
        Assert.Contains(catalog.Listings, row => row.Id == "dataman.plugin.samplecsv");
        Assert.Equal(PluginOrigin.Discovered, catalog.Listings.Single(row => row.Id == "dataman.plugin.samplecsv").Origin);
        Assert.True(catalog.RetainedContextCount >= 1);
        Assert.NotNull(catalog.FindByExtension(".csv"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int IngestAccepted(ServiceProvider services, string csv)
    {
        return services.GetRequiredService<IngestionOrchestrator>().IngestPathsAsync([csv]).GetAwaiter().GetResult().Accepted;
    }

    private static void WriteBundle(string folder, string id, string[] plugins, string[] bundles)
    {
        Directory.CreateDirectory(folder);
        var pluginList = string.Join(", ", plugins.Select(name => $"\"{name}\""));
        var bundleList = string.Join(", ", bundles.Select(name => $"\"{name}\""));
        File.WriteAllText(
            Path.Combine(folder, "bundle.json"),
            $$"""
            {
              "id": "{{id}}",
              "displayName": "{{id}}",
              "plugins": [{{pluginList}}],
              "bundles": [{{bundleList}}]
            }
            """);
    }

    private static void CopySampleCsv(string destination)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "sample-plugins", "samplecsv");
        Assert.True(Directory.Exists(source), $"Sample plugin output is missing at {source}.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
    }
}
