namespace DataMan.Core.Plugins;

public readonly record struct CatalogRelease(
    bool ContextsCollected,
    int UnloadedContextCount,
    int AliveContextCount);
