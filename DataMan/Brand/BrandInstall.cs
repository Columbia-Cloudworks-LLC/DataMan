using Microsoft.UI.Xaml;

namespace DataMan.Brand;

public static class BrandInstall
{
    public static void Attach(Window window)
    {
        var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "DataMan.ico");
        if (File.Exists(ico))
        {
            window.AppWindow.SetIcon(ico);
        }
    }
}
