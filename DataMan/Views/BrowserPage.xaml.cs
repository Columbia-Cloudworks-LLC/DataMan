using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DataMan.Views;

public sealed partial class BrowserPage : Page
{
    public BrowserPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload(SearchBox.Text);
        LibraryEvents.Changed += OnLibraryChanged;
        Unloaded += (_, _) => LibraryEvents.Changed -= OnLibraryChanged;
    }

    private void OnLibraryChanged() => Reload(SearchBox.Text);

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        Reload(args.QueryText);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            Reload(sender.Text);
        }
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemList.SelectedItem is ItemRow row)
        {
            ShowDetail(row.ItemId);
        }
    }

    private void Reload(string? query)
    {
        var library = App.Services.GetRequiredService<LibraryRepository>();
        var hits = library.Search(query ?? string.Empty);
        ItemList.ItemsSource = hits.Select(hit => new ItemRow(
            hit.Item.ItemId,
            hit.Item.Title,
            hit.Snippet ?? FileLocator.Parse(hit.Item.LocatorJson).Path)).ToArray();

        if (hits.Count == 0)
        {
            DetailTitle.Text = string.IsNullOrWhiteSpace(query) ? "Nothing ingested yet" : "No matches";
            DetailMeta.Text = string.Empty;
            DetailBody.Text = string.Empty;
        }
    }

    private void ShowDetail(string itemId)
    {
        var detail = App.Services.GetRequiredService<LibraryRepository>().GetItem(itemId);
        if (detail is null)
        {
            return;
        }

        var path = FileLocator.Parse(detail.Item.LocatorJson).Path;
        DetailTitle.Text = detail.Item.Title;
        DetailMeta.Text = $"{detail.Item.Subtype} · {path}\nSHA-256 {detail.Item.OriginalHash}";
        DetailBody.Text = detail.Body ?? string.Empty;
    }

}

public sealed record ItemRow(string ItemId, string Title, string Subtitle);
