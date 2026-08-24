using DataMan.Core.Hosting;
using DataMan.Core.Ingestion;
using DataMan.Core.Search;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace DataMan;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
        Services = BuildServices();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Services.GetRequiredService<AppDatabase>().Initialize();
            var library = Services.GetRequiredService<LibraryRepository>();
            var monitor = Services.GetRequiredService<WatchedRootMonitor>();
            foreach (var (sourceId, rootPath) in library.ListWatchedRoots())
            {
                monitor.Watch(sourceId, rootPath);
            }

            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
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

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddDataManCore(AppPaths.DatabasePath, AppPaths.PluginsDirectory);
        services.AddSingleton<ITextEmbedder>(_ =>
            OnnxTextEmbedder.TryCreate() ?? (ITextEmbedder)new UnavailableEmbedder());
        return services.BuildServiceProvider();
    }
}
