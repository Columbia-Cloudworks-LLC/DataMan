using System.Runtime.Loader;
using DataMan.Contracts;

namespace DataMan.Core.Plugins.Internal;

internal sealed class DiscoveredSlot
{
    public required PluginListing Listing { get; init; }
    public required IIngestionPlugin Plugin { get; init; }
    public required AssemblyLoadContext Context { get; init; }
    public required WeakReference ContextWeak { get; init; }
}
