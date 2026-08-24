namespace DataMan.Contracts;

public sealed class ItemDraft
{
    public required string SourceId { get; init; }
    public string? ParentItemId { get; init; }
    public required string Kind { get; init; }
    public string? Subtype { get; init; }
    public required string Title { get; init; }
    public string? ContentHash { get; init; }
    public string? OriginalHash { get; init; }
    public required string LocatorJson { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public string? SourceCreatedAt { get; init; }
    public string? SourceUpdatedAt { get; init; }
    public ItemStatus Status { get; init; } = ItemStatus.Active;
}

public sealed class ContentDraft
{
    public required string ContentType { get; init; }
    public required string Body { get; init; }
    public string? Language { get; init; }
    public required string ExtractedBy { get; init; }
}

public sealed class ItemRecord
{
    public required string ItemId { get; init; }
    public string? ParentItemId { get; init; }
    public required string SourceId { get; init; }
    public required string Kind { get; init; }
    public string? Subtype { get; init; }
    public required string Title { get; init; }
    public string? ContentHash { get; init; }
    public string? OriginalHash { get; init; }
    public required string LocatorJson { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public required string IngestedAt { get; init; }
    public required ItemStatus Status { get; init; }
}

public sealed class ItemDetail
{
    public required ItemRecord Item { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }
}

public sealed class SearchHit
{
    public required ItemRecord Item { get; init; }
    public string? Snippet { get; init; }
}

public sealed class LibraryStats
{
    public required int ItemCount { get; init; }
    public required int SourceCount { get; init; }
    public required int ContentCount { get; init; }
    public required long DatabaseBytes { get; init; }
    public required string DatabasePath { get; init; }
    public required int SchemaVersion { get; init; }
}
