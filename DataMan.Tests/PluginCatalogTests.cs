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
        var first = PluginCatalog.Load(missing, BuiltInIngestionPlugins.CreateAll());
        var second = PluginCatalog.Load(missing, BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, first.Plugins.Count);
        Assert.Empty(first.Issues);
        Assert.NotNull(new PluginRegistry(first.Plugins).FindByExtension(".md"));
        Assert.Null(new PluginRegistry(first.Plugins).FindByExtension(".csv"));
        Assert.Equal(first.Plugins.Select(plugin => plugin.Id), second.Plugins.Select(plugin => plugin.Id));
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
        var snapshot = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, snapshot.Plugins.Count);
        Assert.Contains(
            snapshot.Issues,
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

        var snapshot = PluginCatalog.Load(Path.Combine(_root, "plugins"), BuiltInIngestionPlugins.CreateAll());

        Assert.Equal(3, snapshot.Plugins.Count);
        var cycles = snapshot.Issues.Where(issue => issue.Kind == CatalogIssueKind.Cycle).ToArray();
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

        var snapshot = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.Contains(snapshot.Plugins, plugin => plugin.Id == "dataman.plugin.samplecsv");
        Assert.DoesNotContain(snapshot.Issues, issue => issue.Kind == CatalogIssueKind.Cycle);
        Assert.NotNull(new PluginRegistry(snapshot.Plugins).FindByExtension(".csv"));
    }

    [Fact]
    public void Cyclic_bundle_does_not_activate_its_member_plugin()
    {
        var plugins = Path.Combine(_root, "cyclic-member");
        CopySampleCsv(Path.Combine(plugins, "samplecsv"));
        WriteBundle(Path.Combine(plugins, "a"), "a", ["dataman.plugin.samplecsv"], ["b"]);
        WriteBundle(Path.Combine(plugins, "b"), "b", [], ["a"]);

        var snapshot = PluginCatalog.Load(plugins, BuiltInIngestionPlugins.CreateAll());

        Assert.DoesNotContain(snapshot.Plugins, plugin => plugin.Id == "dataman.plugin.samplecsv");
        Assert.Null(new PluginRegistry(snapshot.Plugins).FindByExtension(".csv"));
        Assert.Contains(snapshot.Issues, issue => issue.Kind == CatalogIssueKind.Cycle);
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
        var snapshot = services.GetRequiredService<CatalogSnapshot>();
        Assert.True(snapshot.RetainedContextCount >= 1);
        var registry = services.GetRequiredService<PluginRegistry>();
        Assert.NotNull(registry.FindByExtension(".csv"));
        Assert.Contains(registry.All, plugin => plugin.Id == "dataman.plugin.samplecsv");

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
        Assert.Null(services.GetRequiredService<PluginRegistry>().FindByExtension(".csv"));
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
