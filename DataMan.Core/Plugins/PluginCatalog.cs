using System.Runtime.Loader;
using DataMan.Contracts;
using DataMan.Core.Plugins.Internal;

namespace DataMan.Core.Plugins;

public static class PluginCatalog
{
    public static CatalogSnapshot Load(string? pluginsDirectory, IEnumerable<IIngestionPlugin> builtIns)
    {
        ArgumentNullException.ThrowIfNull(builtIns);
        var builtInList = builtIns.ToArray();
        var (units, scanIssues) = ManifestDirectory.ReadAll(pluginsDirectory);
        var issues = new List<CatalogIssue>(scanIssues);
        var unique = Deduplicate(units, issues);
        var index = unique.ToDictionary(unit => unit.Id, StringComparer.Ordinal);

        foreach (var bundle in unique.OfType<BundleUnit>())
        {
            if (BundleFlattener.Flatten(bundle, index) is FlattenCycle cycle)
            {
                issues.Add(new CatalogIssue(
                    CatalogIssueKind.Cycle,
                    "Bundle graph contains a cycle.",
                    bundle.ManifestPath,
                    string.Join(" -> ", cycle.Path)));
            }
        }

        var contexts = new List<AssemblyLoadContext>();
        var discovered = new List<IIngestionPlugin>();
        if (!string.IsNullOrWhiteSpace(pluginsDirectory))
        {
            foreach (var plugin in unique.OfType<PluginUnit>().OrderBy(unit => unit.Id, StringComparer.Ordinal))
            {
                if (plugin.Dependencies.Length > 0)
                {
                    issues.Add(new CatalogIssue(
                        CatalogIssueKind.UnsupportedDependency,
                        "Plugin dependencies are not loaded in this slice.",
                        plugin.ManifestPath));
                    continue;
                }

                switch (CollectiblePluginLoader.Activate(plugin, pluginsDirectory))
                {
                    case PluginActivationOk ok:
                        discovered.Add(ok.Plugin);
                        contexts.Add(ok.Context);
                        break;
                    case PluginActivationFail fail:
                        issues.Add(fail.Issue);
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected plugin activation.");
                }
            }
        }

        var merged = new List<IIngestionPlugin>(builtInList.Length + discovered.Count);
        merged.AddRange(builtInList);
        merged.AddRange(discovered);
        return new CatalogSnapshot(merged, issues, contexts);
    }

    private static List<ManifestUnit> Deduplicate(IReadOnlyList<ManifestUnit> units, List<CatalogIssue> issues)
    {
        var kept = new List<ManifestUnit>();
        foreach (var group in units.GroupBy(unit => unit.Id, StringComparer.Ordinal))
        {
            var members = group.ToArray();
            if (members.Length == 1)
            {
                kept.Add(members[0]);
                continue;
            }

            issues.Add(new CatalogIssue(
                CatalogIssueKind.DuplicateId,
                $"Plugin id '{group.Key}' appears more than once.",
                members[0].ManifestPath,
                string.Join(", ", members.Select(unit => unit.ManifestPath))));
            kept.Add(members.OrderBy(unit => unit.ManifestPath, StringComparer.OrdinalIgnoreCase).First());
        }

        return kept;
    }
}
