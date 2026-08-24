using DataMan.Core.Host;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace DataMan;

public static class WindowTheme
{
    public static void Bind(Window window, HostAppearance host)
    {
        if (window.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException("Window content must be a FrameworkElement.");
        }

        Project(root, window.AppWindow, host.Current);
        host.Changed += appearance => Project(root, window.AppWindow, appearance);
    }

    private static void Project(FrameworkElement root, AppWindow appWindow, Appearance appearance)
    {
        root.RequestedTheme = appearance.Match(
            _ => ElementTheme.Default,
            _ => ElementTheme.Light,
            _ => ElementTheme.Dark);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        appWindow.TitleBar.PreferredTheme = appearance.Match(
            _ => TitleBarTheme.UseDefaultAppMode,
            _ => TitleBarTheme.Light,
            _ => TitleBarTheme.Dark);
    }
}
