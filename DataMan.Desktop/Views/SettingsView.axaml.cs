using Avalonia.Controls;
using DataMan.Core.Plugins;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DataMan.Desktop.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Refresh();
        LibraryEvents.Changed += Refresh;
    }

    private void Refresh()
    {
        var stats = App.Services.GetRequiredService<LibraryRepository>().GetStats();
        DatabasePathText.Text = stats.DatabasePath;
        SchemaText.Text = $"Schema version {stats.SchemaVersion} · {stats.ContentCount} extracted documents";
        PluginList.ItemsSource = App.Services.GetRequiredService<PluginCatalog>().Listings
            .Select(plugin => $"{plugin.DisplayName} ({plugin.Id} {plugin.Version})")
            .ToArray();
    }
}
