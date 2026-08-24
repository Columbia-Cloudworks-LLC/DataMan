using DataMan.Core.Host;
using DataMan.Core.Plugins;
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
            PluginList.ItemsSource = App.Services.GetRequiredService<PluginCatalog>().Listings
                .Select(plugin => $"{plugin.DisplayName} ({plugin.Id} {plugin.Version})")
                .ToArray();
            ShowCurrentAppearance();
            AppearanceSystem.Checked += (_, _) => SelectIfChecked(AppearanceSystem, new Appearance.System());
            AppearanceLight.Checked += (_, _) => SelectIfChecked(AppearanceLight, new Appearance.Light());
            AppearanceDark.Checked += (_, _) => SelectIfChecked(AppearanceDark, new Appearance.Dark());
        };
    }

    private void ShowCurrentAppearance()
    {
        var current = App.Appearance.Current;
        AppearanceSystem.IsChecked = current is Appearance.System;
        AppearanceLight.IsChecked = current is Appearance.Light;
        AppearanceDark.IsChecked = current is Appearance.Dark;
    }

    private static void SelectIfChecked(RadioButton radio, Appearance appearance)
    {
        if (radio.IsChecked == true)
        {
            App.Appearance.Select(appearance);
        }
    }
}
