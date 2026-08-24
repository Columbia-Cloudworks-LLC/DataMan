using System.Runtime.CompilerServices;
using DataMan.Contracts;
using DataMan.Core.Plugins.Internal;

namespace DataMan.Core.Plugins;

public sealed class PluginCatalog : IDisposable
{
    private readonly IIngestionPlugin[] _builtIns;
    private readonly PluginListing[] _builtInListings;
    private readonly IReadOnlyList<CatalogIssue> _issues;
    private DiscoveredSlot[]? _live;

    private CatalogRelease _release;
    private bool _released;

    internal PluginCatalog(
        IIngestionPlugin[] builtIns,
        DiscoveredSlot[] discovered,
        IReadOnlyList<CatalogIssue> issues)
    {
        _builtIns = builtIns;
        _builtInListings = [.. builtIns.Select(ToBuiltInListing)];
        _live = discovered;
        _issues = [.. issues];
    }

    // NoInlining so activation locals cannot remain on a caller that later Releases.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static PluginCatalog Load(string? pluginsDirectory, IEnumerable<IIngestionPlugin> builtIns)
    {
        var builtInList = builtIns.ToArray();
        var (units, scanIssues) = ManifestDirectory.ReadAll(pluginsDirectory);
        var issues = new List<CatalogIssue>(scanIssues);
        var unique = Deduplicate(units, issues);
        var index = unique.ToDictionary(unit => unit.Id, StringComparer.Ordinal);

        var referencedByBundle = unique.OfType<BundleUnit>()
            .SelectMany(bundle => bundle.PluginRefs)
            .ToHashSet(StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);

        foreach (var bundle in unique.OfType<BundleUnit>())
        {
            switch (BundleFlattener.Flatten(bundle, index))
            {
                case FlattenCycle cycle:
                    issues.Add(new CatalogIssue(
                        CatalogIssueKind.Cycle,
                        "Bundle graph contains a cycle.",
                        bundle.ManifestPath,
                        string.Join(" -> ", cycle.Path)));
                    break;
                case FlattenOk flat:
                    foreach (var plugin in flat.Plugins)
                    {
                        resolved.Add(plugin.Id);
                    }

                    break;
                default:
                    throw new InvalidOperationException("Unexpected flatten result.");
            }
        }

        var slots = new List<DiscoveredSlot>();
        if (!string.IsNullOrWhiteSpace(pluginsDirectory))
        {
            foreach (var plugin in unique.OfType<PluginUnit>().OrderBy(unit => unit.Id, StringComparer.Ordinal))
            {
                if (referencedByBundle.Contains(plugin.Id) && !resolved.Contains(plugin.Id))
                {
                    continue;
                }

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
                        slots.Add(ok.Slot);
                        break;
                    case PluginActivationFail fail:
                        issues.Add(fail.Issue);
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected plugin activation.");
                }
            }
        }

        return new PluginCatalog(builtInList, [.. slots], issues);
    }

    public IReadOnlyList<PluginListing> Listings
    {
        get
        {
            var live = Volatile.Read(ref _live);
            if (live is null || live.Length == 0)
            {
                return [.. _builtInListings];
            }

            var rows = new PluginListing[_builtInListings.Length + live.Length];
            _builtInListings.CopyTo(rows, 0);
            for (var i = 0; i < live.Length; i++)
            {
                rows[_builtInListings.Length + i] = live[i].Listing;
            }

            return rows;
        }
    }

    public IReadOnlyList<CatalogIssue> Issues => [.. _issues];

    internal int RetainedContextCount => Volatile.Read(ref _live)?.Length ?? 0;

    public IIngestionPlugin? FindByExtension(string extension)
    {
        foreach (var plugin in _builtIns)
        {
            if (MatchesExtension(plugin, extension))
            {
                return plugin;
            }
        }

        var live = Volatile.Read(ref _live);
        if (live is null)
        {
            return null;
        }

        foreach (var slot in live)
        {
            if (MatchesExtension(slot.Plugin, extension))
            {
                return slot.Plugin;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public CatalogRelease Release()
    {
        return ProveCollection(DetachLive());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference[]? DetachLive()
    {
        return CollectibleContextUnloader.UnloadAndDrop(Interlocked.Exchange(ref _live, null));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CatalogRelease ProveCollection(WeakReference[]? weaks)
    {
        if (weaks is null)
        {
            return _released ? _release : new CatalogRelease(true, 0, 0);
        }

        if (weaks.Length == 0)
        {
            _release = new CatalogRelease(true, 0, 0);
            _released = true;
            return _release;
        }

        var collected = CollectibleContextUnloader.CollectUntilDead(weaks);
        var alive = 0;
        foreach (var weak in weaks)
        {
            if (weak.IsAlive)
            {
                alive++;
            }
        }

        _release = new CatalogRelease(collected, weaks.Length, alive);
        _released = true;
        return _release;
    }

    public void Dispose()
    {
        Release();
    }

    private static PluginListing ToBuiltInListing(IIngestionPlugin plugin) =>
        new(plugin.Id, plugin.DisplayName, plugin.Version, [.. plugin.SupportedSchemesOrExtensions], PluginOrigin.BuiltIn);

    private static bool MatchesExtension(IIngestionPlugin plugin, string extension) =>
        plugin.SupportedSchemesOrExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

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
