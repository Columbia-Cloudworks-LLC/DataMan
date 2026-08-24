namespace DataMan.Contracts;

public interface IIngestionPlugin
{
    string Id { get; }
    string DisplayName { get; }
    string Version { get; }
    IReadOnlyList<string> SupportedSchemesOrExtensions { get; }

    Task<IngestionResult> IngestAsync(IngestionContext context, CancellationToken cancellationToken);
}

public sealed class IngestionContext
{
    public required string SourceId { get; init; }
    public required string LocatorJson { get; init; }
    public Stream? ContentStream { get; init; }
    public string? LocalPath { get; init; }
    public string? ParentItemId { get; init; }
    public string? OriginalHash { get; init; }
    public long? SizeBytes { get; init; }
    public string? SourceCreatedAt { get; init; }
    public string? SourceUpdatedAt { get; init; }
    public IProgress<IngestionProgress>? Progress { get; init; }
    public required IItemWriter Writer { get; init; }
    public required IServiceProvider Services { get; init; }
}

public sealed class IngestionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<string> CreatedItemIds { get; set; } = [];
}

public sealed class IngestionProgress
{
    public required string CurrentPath { get; init; }
    public int Completed { get; init; }
    public int Total { get; init; }
}

public sealed class BatchIngestionResult
{
    public int Accepted { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> ItemIds { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}
