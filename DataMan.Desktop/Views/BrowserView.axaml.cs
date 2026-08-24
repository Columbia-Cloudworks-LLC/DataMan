using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataMan.Contracts;
using DataMan.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DataMan.Desktop.Views;

public sealed partial class BrowserView : UserControl
{
    private BrowserKind _kind = BrowserKind.Text;

    public BrowserView()
    {
        InitializeComponent();
        Reload(SearchBox.Text);
        LibraryEvents.Changed += () => Reload(SearchBox.Text);
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_kind is BrowserKind.Text)
        {
            Reload(SearchBox.Text);
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Reload(SearchBox.Text);
        }
    }

    private void Kind_Checked(object? sender, RoutedEventArgs e)
    {
        _kind = sender == MeaningKindRadio ? BrowserKind.Meaning : BrowserKind.Text;
        Reload(SearchBox.Text);
    }

    private void ItemList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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
