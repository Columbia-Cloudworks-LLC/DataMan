using System.Collections.Immutable;
using System.Text.Json;

namespace DataMan.Core.Plugins.Internal;

internal static class ManifestDirectory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static (ImmutableArray<ManifestUnit> Units, ImmutableArray<CatalogIssue> Issues) ReadAll(
        string? pluginsDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
        {
            return (ImmutableArray<ManifestUnit>.Empty, ImmutableArray<CatalogIssue>.Empty);
        }

        var units = new List<ManifestUnit>();
        var issues = new List<CatalogIssue>();

        string[] children;
        try
        {
            children = Directory.EnumerateDirectories(pluginsDirectory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ([], [new CatalogIssue(CatalogIssueKind.InvalidManifest, ex.Message, pluginsDirectory)]);
        }

        foreach (var child in children)
        {
            var pluginManifest = Path.Combine(child, "plugin.json");
            var bundleManifest = Path.Combine(child, "bundle.json");
            var hasPlugin = File.Exists(pluginManifest);
            var hasBundle = File.Exists(bundleManifest);

            if (hasPlugin == hasBundle)
            {
                if (hasPlugin)
                {
                    issues.Add(new CatalogIssue(
                        CatalogIssueKind.InvalidManifest,
                        "A plugin folder must contain plugin.json or bundle.json, not both.",
                        child));
                }

                continue;
            }

            if (hasPlugin)
            {
                if (TryReadPlugin(pluginManifest, out var plugin, out var issue))
                {
                    units.Add(plugin);
                }
                else if (issue is not null)
                {
                    issues.Add(issue);
                }
            }
            else if (TryReadBundle(bundleManifest, out var bundle, out var issue))
            {
                units.Add(bundle);
            }
            else if (issue is not null)
            {
                issues.Add(issue);
            }
        }

        return ([.. units], [.. issues]);
    }

    private static bool TryReadPlugin(string path, out PluginUnit plugin, out CatalogIssue? issue)
    {
        plugin = null!;
        issue = null;

        if (!TryReadJson(path, out PluginManifestJson? dto, out issue))
        {
            return false;
        }

        if (dto is null
            || string.IsNullOrWhiteSpace(dto.Id)
            || string.IsNullOrWhiteSpace(dto.Version)
            || string.IsNullOrWhiteSpace(dto.Assembly)
            || string.IsNullOrWhiteSpace(dto.EntryType))
        {
            issue = new CatalogIssue(CatalogIssueKind.InvalidManifest, "Plugin manifest is missing required fields.", path);
            return false;
        }

        if (dto.Assembly.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(dto.Assembly))
        {
            issue = new CatalogIssue(CatalogIssueKind.PathEscape, "Assembly path must stay under the plugin folder.", path);
            return false;
        }

        plugin = new PluginUnit(
            dto.Id.Trim(),
            path,
            dto.Version.Trim(),
            dto.Assembly.Trim(),
            dto.EntryType.Trim(),
            [.. dto.Supported ?? []],
            [.. dto.Dependencies ?? []]);
        return true;
    }

    private static bool TryReadBundle(string path, out BundleUnit bundle, out CatalogIssue? issue)
    {
        bundle = null!;
        issue = null;

        if (!TryReadJson(path, out BundleManifestJson? dto, out issue))
        {
            return false;
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Id))
        {
            issue = new CatalogIssue(CatalogIssueKind.InvalidManifest, "Bundle manifest is missing an id.", path);
            return false;
        }

        bundle = new BundleUnit(
            dto.Id.Trim(),
            path,
            dto.DisplayName?.Trim() ?? dto.Id.Trim(),
            [.. dto.Plugins ?? []],
            [.. dto.Bundles ?? []]);
        return true;
    }

    private static bool TryReadJson<T>(string path, out T? dto, out CatalogIssue? issue)
    {
        dto = default;
        issue = null;
        try
        {
            dto = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            issue = new CatalogIssue(CatalogIssueKind.InvalidManifest, ex.Message, path);
            return false;
        }
    }

    private sealed class PluginManifestJson
    {
        public string? Id { get; set; }
        public string? Version { get; set; }
        public string? Assembly { get; set; }
        public string? EntryType { get; set; }
        public List<string>? Supported { get; set; }
        public List<string>? Dependencies { get; set; }
    }

    private sealed class BundleManifestJson
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? Plugins { get; set; }
        public List<string>? Bundles { get; set; }
    }
}
