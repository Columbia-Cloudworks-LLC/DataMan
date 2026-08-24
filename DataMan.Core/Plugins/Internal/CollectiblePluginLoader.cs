using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using DataMan.Contracts;

namespace DataMan.Core.Plugins.Internal;

internal abstract record PluginActivation;

internal sealed record PluginActivationOk(DiscoveredSlot Slot) : PluginActivation;

internal sealed record PluginActivationFail(string PluginId, CatalogIssue Issue) : PluginActivation;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;

    public PluginLoadContext(string name, string pluginDirectory)
        : base(name, isCollectible: true)
    {
        _pluginDirectory = pluginDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, "DataMan.Contracts", StringComparison.Ordinal))
        {
            return typeof(IIngestionPlugin).Assembly;
        }

        var local = Path.Combine(_pluginDirectory, assemblyName.Name + ".dll");
        if (File.Exists(local))
        {
            return LoadFromAssemblyPath(Path.GetFullPath(local));
        }

        return null;
    }
}

internal static class CollectiblePluginLoader
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static PluginActivation Activate(PluginUnit unit, string pluginsDirectory)
    {
        var pluginFolder = Path.GetDirectoryName(unit.ManifestPath);
        if (string.IsNullOrWhiteSpace(pluginFolder))
        {
            return new PluginActivationFail(
                unit.Id,
                new CatalogIssue(CatalogIssueKind.AssemblyLoadFailed, "Plugin folder is missing.", unit.ManifestPath));
        }

        var assemblyPath = Path.GetFullPath(Path.Combine(pluginFolder, unit.AssemblyRelativePath));
        var root = Path.GetFullPath(pluginsDirectory);
        if (!assemblyPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !assemblyPath.StartsWith(Path.GetFullPath(pluginFolder), StringComparison.OrdinalIgnoreCase))
        {
            return new PluginActivationFail(
                unit.Id,
                new CatalogIssue(CatalogIssueKind.PathEscape, "Assembly path escaped the plugin folder.", unit.ManifestPath));
        }

        if (!File.Exists(assemblyPath))
        {
            return new PluginActivationFail(
                unit.Id,
                new CatalogIssue(CatalogIssueKind.AssemblyLoadFailed, $"Assembly '{unit.AssemblyRelativePath}' was not found.", unit.ManifestPath));
        }

        PluginLoadContext? context = null;
        try
        {
            context = new PluginLoadContext($"dataman-plugin-{unit.Id}", pluginFolder);
            var contextWeak = new WeakReference(context, trackResurrection: false);
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(unit.EntryType, throwOnError: false);
            if (type is null || Activator.CreateInstance(type) is not IIngestionPlugin plugin)
            {
                context.Unload();
                return new PluginActivationFail(
                    unit.Id,
                    new CatalogIssue(CatalogIssueKind.EntryTypeInvalid, $"'{unit.EntryType}' is not an IIngestionPlugin.", unit.ManifestPath));
            }

            return new PluginActivationOk(new DiscoveredSlot
            {
                Listing = new PluginListing(
                    plugin.Id,
                    plugin.DisplayName,
                    plugin.Version,
                    [.. plugin.SupportedSchemesOrExtensions],
                    PluginOrigin.Discovered),
                Plugin = plugin,
                Context = context,
                ContextWeak = contextWeak
            });
        }
        catch (Exception ex)
        {
            context?.Unload();
            return new PluginActivationFail(
                unit.Id,
                new CatalogIssue(CatalogIssueKind.AssemblyLoadFailed, ex.Message, unit.ManifestPath));
        }
    }
}
