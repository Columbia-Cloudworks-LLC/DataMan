using DataMan.Contracts;
using DataMan.Core.Ingestion;
using DataMan.Core.Plugins;
using DataMan.Core.Search;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DataMan.Core.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataManCore(
        this IServiceCollection services,
        string databasePath,
        string? pluginsDirectory = null)
    {
        services.AddSingleton(new AppDatabase(databasePath));
        services.AddSingleton<ITextChunker, FixedWindowChunker>();
        services.AddSingleton<ITextEmbedder, UnavailableEmbedder>();
        services.AddSingleton<SemanticCorpus>();
        services.AddSingleton<IItemWriter, SqliteItemWriter>();
        services.AddSingleton<LibraryRepository>();
        services.AddSingleton(_ => PluginCatalog.Load(pluginsDirectory, BuiltInIngestionPlugins.CreateAll()));
        services.AddSingleton<IngestionOrchestrator>();
        services.AddSingleton<SourceReconciler>();
        services.AddSingleton<WatchedRootMonitor>();
        return services;
    }
}
