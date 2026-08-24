using DataMan.Contracts;

namespace DataMan.Core.Ingestion;

public sealed class PluginRegistry
{
    private readonly IReadOnlyList<IIngestionPlugin> _plugins;

    public PluginRegistry(IEnumerable<IIngestionPlugin> plugins)
    {
        _plugins = plugins.ToArray();
    }

    public IReadOnlyList<IIngestionPlugin> All => _plugins;

    public IIngestionPlugin? FindByExtension(string extension)
    {
        return _plugins.FirstOrDefault(plugin =>
            plugin.SupportedSchemesOrExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }
}
