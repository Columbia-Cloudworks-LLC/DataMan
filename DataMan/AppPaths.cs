using Windows.Storage;

namespace DataMan;

public static class AppPaths
{
    public static string DataRoot
    {
        get
        {
            try
            {
                return ApplicationData.Current.LocalFolder.Path;
            }
            catch (Exception)
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DataMan");
            }
        }
    }

    public static string DatabasePath => Path.Combine(DataRoot, "dataman.db");

    public static string PluginsDirectory => Path.Combine(DataRoot, "plugins");
}
