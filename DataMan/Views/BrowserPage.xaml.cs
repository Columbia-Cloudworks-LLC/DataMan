using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace DataMan.Views;

public sealed partial class BrowserPage : Page
{
    private BrowserKind _kind = BrowserKind.Text;

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
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && _kind is BrowserKind.Text)
        {
            Reload(sender.Text);
        }
    }

    private void SearchKindBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        _kind = sender.SelectedItem == MeaningKindItem ? BrowserKind.Meaning : BrowserKind.Text;
        Reload(SearchBox.Text);
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemList.SelectedItem is ItemRow row)
        {
            ShowDetail(row.ItemId);
        }
    }

    private void Reload(string? box)
    {
        var library = App.Services.GetRequiredService<LibraryRepository>();
        var query = BuildQuery(box, _kind);
        var outcome = library.Search(query);

        switch (outcome)
        {
            case SearchOutcome.Hits hits:
                ItemList.ItemsSource = hits.Items.Select(hit => new ItemRow(
                    hit.Item.ItemId,
                    hit.Item.Title,
                    hit.Snippet ?? FileLocator.Parse(hit.Item.LocatorJson).Path)).ToArray();
                if (hits.Items.Count == 0)
                {
                    DetailTitle.Text = query is LibraryQuery.Recent
                        ? "Nothing ingested yet"
                        : "No matches";
                    DetailMeta.Text = string.Empty;
                    DetailBody.Text = string.Empty;
                }

                break;
            case SearchOutcome.SemanticUnavailable missing:
                ItemList.ItemsSource = Array.Empty<ItemRow>();
                DetailTitle.Text = missing.Gap switch
                {
                    SemanticGap.EmbedderMissing => "Semantic model is not installed",
                    SemanticGap.IndexEmpty => "Nothing has been indexed for meaning yet",
                    _ => throw new ArgumentOutOfRangeException(nameof(missing.Gap))
                };
                DetailMeta.Text = string.Empty;
                DetailBody.Text = string.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private static LibraryQuery BuildQuery(string? box, BrowserKind kind)
    {
        if (!QueryText.TryCreate(box, out var text))
        {
            return new LibraryQuery.Recent();
        }

        return kind switch
        {
            BrowserKind.Text => new LibraryQuery.Lexical(text),
            BrowserKind.Meaning => new LibraryQuery.Semantic(text),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
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

    private enum BrowserKind
    {
        Text,
        Meaning
    }
}

public sealed record ItemRow(string ItemId, string Title, string Subtitle);
