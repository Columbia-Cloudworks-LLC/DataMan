using System.Collections.Immutable;

namespace DataMan.Core.Plugins.Internal;

internal abstract record ManifestUnit(string Id, string ManifestPath);

internal sealed record PluginUnit(
    string Id,
    string ManifestPath,
    string Version,
    string AssemblyRelativePath,
    string EntryType,
    ImmutableArray<string> Supported,
    ImmutableArray<string> Dependencies) : ManifestUnit(Id, ManifestPath);

internal sealed record BundleUnit(
    string Id,
    string ManifestPath,
    string DisplayName,
    ImmutableArray<string> PluginRefs,
    ImmutableArray<string> BundleRefs) : ManifestUnit(Id, ManifestPath);

internal abstract record FlattenResult;

internal sealed record FlattenOk(ImmutableArray<PluginUnit> Plugins) : FlattenResult;

internal sealed record FlattenCycle(ImmutableArray<string> Path) : FlattenResult;
