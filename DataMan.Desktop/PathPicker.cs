using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace DataMan.Desktop;

public static class PathPicker
{
    private static readonly string[] TextPatterns = ["*.txt", "*.md", "*.markdown", "*.log"];

    public static async Task<IReadOnlyList<string>> PickFilesAsync(TopLevel owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Text files")
                {
                    Patterns = TextPatterns
                }
            ]
        });

        return ToLocalPaths(files);
    }

    public static async Task<string?> PickFolderAsync(TopLevel owner)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        return folders.Count == 0 ? null : LocalPath(folders[0]);
    }

    public static IReadOnlyList<string> PathsFromDrop(IDataObject data)
    {
        if (!data.Contains(DataFormats.Files))
        {
            return [];
        }

        var files = data.GetFiles();
        return files is null ? [] : ToLocalPaths(files);
    }

    private static IReadOnlyList<string> ToLocalPaths(IEnumerable<IStorageItem> items)
    {
        var paths = new List<string>();
        foreach (var item in items)
        {
            var path = LocalPath(item);
            if (path is not null)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static string? LocalPath(IStorageItem item)
    {
        var path = item.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return null;
        }

        return Path.GetFullPath(path);
    }
}
