using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Search;
using DataMan.Core.Storage;
using DataMan.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataMan.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                Services = BuildServices();
                Services.GetRequiredService<AppDatabase>().Initialize();
                var library = Services.GetRequiredService<LibraryRepository>();
                var monitor = Services.GetRequiredService<WatchedRootMonitor>();
                foreach (var (sourceId, rootPath) in library.ListWatchedRoots())
                {
                    monitor.Watch(sourceId, rootPath);
                }

                desktop.MainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddDataManCore(AppPaths.DatabasePath, AppPaths.PluginsDirectory);
        services.AddSingleton<ITextEmbedder>(_ =>
            OnnxTextEmbedder.TryCreate(AppPaths.EmbedderProbeDirectories)
            ?? (ITextEmbedder)new UnavailableEmbedder());
        return services.BuildServiceProvider();
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.AppendAllText(
                Path.Combine(AppPaths.DataRoot, "startup.log"),
                $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
