using Avalonia.Controls;
using DataMan.Desktop.Views;

namespace DataMan.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Control> _views;

    public MainWindow()
    {
        InitializeComponent();
        _views = new Dictionary<string, Control>
        {
            ["dashboard"] = new DashboardView(),
            ["browser"] = new BrowserView(),
            ["settings"] = new SettingsView()
        };
        Show("dashboard");
        NavList.SelectedIndex = 0;
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        Show(tag);
    }

    private void Show(string tag)
    {
        ContentHost.Content = tag switch
        {
            "dashboard" => _views["dashboard"],
            "browser" => _views["browser"],
            "settings" => _views["settings"],
            _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, null)
        };
    }
}
