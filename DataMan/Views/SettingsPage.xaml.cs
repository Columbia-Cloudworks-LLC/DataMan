using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DataMan.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var stats = App.Services.GetRequiredService<LibraryRepository>().GetStats();
            DatabasePathText.Text = stats.DatabasePath;
            SchemaText.Text = $"Schema version {stats.SchemaVersion} · {stats.ContentCount} extracted documents";
            PluginList.ItemsSource = App.Services.GetRequiredService<PluginRegistry>().All
                .Select(plugin => $"{plugin.DisplayName} ({plugin.Id} {plugin.Version})")
                .ToArray();
        };
    }
}
