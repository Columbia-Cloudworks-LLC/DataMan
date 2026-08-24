using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace DataMan.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshStats();
        LibraryEvents.Changed += RefreshStats;
        Unloaded += (_, _) => LibraryEvents.Changed -= RefreshStats;
    }

    private void RefreshStats()
    {
        var stats = App.Services.GetRequiredService<LibraryRepository>().GetStats();
        ItemCountText.Text = stats.ItemCount.ToString();
        SourceCountText.Text = stats.SourceCount.ToString();
        DatabaseSizeText.Text = FormatBytes(stats.DatabaseBytes);
    }

    private async void IngestFiles_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var paths = await PathPicker.PickFilesAsync(App.MainWindow);
        await IngestAsync(paths);
    }

    private async void IngestFolder_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var folder = await PathPicker.PickFolderAsync(App.MainWindow);
        if (folder is not null)
        {
            await IngestAsync([folder]);
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Ingest into DataMan";
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = await PathPicker.PathsFromDropAsync(items);
        await IngestAsync(paths);
    }

    private async Task IngestAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        IngestProgress.Visibility = Visibility.Visible;
        IngestFilesButton.IsEnabled = false;
        IngestFolderButton.IsEnabled = false;
        StatusText.Text = "Ingesting…";

        try
        {
            var orchestrator = App.Services.GetRequiredService<IngestionOrchestrator>();
            var result = await orchestrator.IngestPathsAsync(paths);
            StatusText.Text = $"{result.Accepted} ingested, {result.Skipped} skipped, {result.Failed} failed.";
            if (result.Errors.Count > 0)
            {
                StatusText.Text += " " + result.Errors[0];
            }

            LibraryEvents.NotifyChanged();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            IngestProgress.Visibility = Visibility.Collapsed;
            IngestFilesButton.IsEnabled = true;
            IngestFolderButton.IsEnabled = true;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.0} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }
}
