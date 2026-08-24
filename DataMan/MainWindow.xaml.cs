using DataMan.Brand;
using DataMan.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DataMan;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BrandInstall.Attach(this);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "browser":
                    ContentFrame.Navigate(typeof(BrowserPage));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tag), tag, null);
            }
        }
    }
}
