using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataMan.Core.Ingestion;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DataMan.Desktop.Views;

public sealed partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        RefreshStats();
        LibraryEvents.Changed += RefreshStats;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void RefreshStats()
    {
        var stats = App.Services.GetRequiredService<LibraryRepository>().GetStats();
        ItemCountText.Text = stats.ItemCount.ToString();
        SourceCountText.Text = stats.SourceCount.ToString();
        DatabaseSizeText.Text = FormatBytes(stats.DatabaseBytes);
    }

    private async void IngestFiles_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var paths = await PathPicker.PickFilesAsync(top);
        await IngestAsync(paths);
    }

    private async void IngestFolder_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var folder = await PathPicker.PickFolderAsync(top);
        if (folder is not null)
        {
            await IngestAsync([folder]);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
        => _ = IngestAsync(PathPicker.PathsFromDrop(e.Data));

    private async Task IngestAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        IngestProgress.IsVisible = true;
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
            IngestProgress.IsVisible = false;
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
