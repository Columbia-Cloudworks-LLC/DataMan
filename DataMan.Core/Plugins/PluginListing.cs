namespace DataMan.Core.Plugins;

public enum PluginOrigin
{
    BuiltIn,
    Discovered
}

public readonly record struct PluginListing(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Extensions,
    PluginOrigin Origin);
