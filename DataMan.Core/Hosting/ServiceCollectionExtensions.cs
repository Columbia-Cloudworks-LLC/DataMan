using DataMan.Contracts;
using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DataMan.Core.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataManCore(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(new AppDatabase(databasePath));
        services.AddSingleton<IItemWriter, SqliteItemWriter>();
        services.AddSingleton<LibraryRepository>();
        services.AddSingleton<IIngestionPlugin, PlainTextPlugin>();
        services.AddSingleton<IIngestionPlugin, MarkdownPlugin>();
        services.AddSingleton<IIngestionPlugin, LogFilePlugin>();
        services.AddSingleton<PluginRegistry>();
        services.AddSingleton<IngestionOrchestrator>();
        return services;
    }
}
