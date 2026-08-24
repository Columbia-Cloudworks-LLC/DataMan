using System.Runtime.Loader;
using DataMan.Contracts;

namespace DataMan.Core.Plugins;

public sealed class CatalogSnapshot
{
    private readonly IReadOnlyList<AssemblyLoadContext> _contexts;

    internal CatalogSnapshot(
        IReadOnlyList<IIngestionPlugin> plugins,
        IReadOnlyList<CatalogIssue> issues,
        IReadOnlyList<AssemblyLoadContext> contexts)
    {
        Plugins = plugins;
        Issues = issues;
        _contexts = contexts;
    }

    public IReadOnlyList<IIngestionPlugin> Plugins { get; }
    public IReadOnlyList<CatalogIssue> Issues { get; }

    internal int RetainedContextCount => _contexts.Count;
}
