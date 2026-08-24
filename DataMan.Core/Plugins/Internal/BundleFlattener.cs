using System.Collections.Immutable;

namespace DataMan.Core.Plugins.Internal;

internal static class BundleFlattener
{
    public static FlattenResult Flatten(
        BundleUnit root,
        IReadOnlyDictionary<string, ManifestUnit> index)
    {
        var plugins = new List<PluginUnit>();
        var seenPlugins = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var stack = new List<string>();

        FlattenResult WalkBundle(BundleUnit bundle)
        {
            if (visiting.Contains(bundle.Id))
            {
                var cycleStart = stack.IndexOf(bundle.Id);
                var cycle = stack.Skip(cycleStart).Append(bundle.Id).ToImmutableArray();
                return new FlattenCycle(cycle);
            }

            visiting.Add(bundle.Id);
            stack.Add(bundle.Id);

            foreach (var pluginId in bundle.PluginRefs)
            {
                if (!index.TryGetValue(pluginId, out var unit) || unit is not PluginUnit plugin)
                {
                    continue;
                }

                if (seenPlugins.Add(plugin.Id))
                {
                    plugins.Add(plugin);
                }
            }

            foreach (var bundleId in bundle.BundleRefs)
            {
                if (!index.TryGetValue(bundleId, out var unit) || unit is not BundleUnit nested)
                {
                    continue;
                }

                var nestedResult = WalkBundle(nested);
                if (nestedResult is FlattenCycle)
                {
                    return nestedResult;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            visiting.Remove(bundle.Id);
            return new FlattenOk(plugins.ToImmutableArray());
        }

        return WalkBundle(root);
    }
}
