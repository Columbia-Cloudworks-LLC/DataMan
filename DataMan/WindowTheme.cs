using DataMan.Core.Host;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace DataMan;

public static class WindowTheme
{
    public static IDisposable Bind(Window window, HostAppearance host)
    {
        if (window.Content is not FrameworkElement root)
        {
            throw new InvalidOperationException("Window content must be a FrameworkElement.");
        }

        Project(root, window.AppWindow, host.Current);
        Action<Appearance> handler = appearance => Project(root, window.AppWindow, appearance);
        host.Changed += handler;
        return new Binding(host, handler);
    }

    private sealed class Binding(HostAppearance host, Action<Appearance> handler) : IDisposable
    {
        public void Dispose() => host.Changed -= handler;
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
