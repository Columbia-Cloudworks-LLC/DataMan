namespace DataMan.Desktop;

public static class AppPaths
{
    // LocalApplicationData is $XDG_DATA_HOME ?? ~/.local/share on Linux.
    public static string DataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataMan");

    public static string DatabasePath => Path.Combine(DataRoot, "dataman.db");

    public static string PluginsDirectory => Path.Combine(DataRoot, "plugins");

    public static string ModelsDirectory => Path.Combine(DataRoot, "models");

    public static IReadOnlyList<string> EmbedderProbeDirectories =>
        [ModelsDirectory, Path.Combine(AppContext.BaseDirectory, "models")];
}
