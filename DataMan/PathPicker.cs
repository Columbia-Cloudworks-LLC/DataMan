using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DataMan;

public static class PathPicker
{
    public static async Task<IReadOnlyList<string>> PickFilesAsync(Window window)
    {
        var picker = new FileOpenPicker();
        Initialize(picker, window);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".log");

        var files = await picker.PickMultipleFilesAsync();
        return files.Select(file => file.Path).ToArray();
    }

    public static async Task<string?> PickFolderAsync(Window window)
    {
        var picker = new FolderPicker();
        Initialize(picker, window);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public static async Task<IReadOnlyList<string>> PathsFromDropAsync(IReadOnlyList<IStorageItem> items)
    {
        return await Task.FromResult(items.Select(item => item.Path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray());
    }

    private static void Initialize(object picker, Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
