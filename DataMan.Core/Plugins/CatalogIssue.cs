namespace DataMan.Core.Plugins;

public enum CatalogIssueKind
{
    InvalidManifest,
    Cycle,
    AssemblyLoadFailed,
    EntryTypeInvalid,
    DuplicateId,
    UnsupportedDependency,
    PathEscape
}

public sealed class CatalogIssue
{
    public CatalogIssue(CatalogIssueKind kind, string message, string? path = null, string? detail = null)
    {
        Kind = kind;
        Message = message;
        Path = path;
        Detail = detail;
    }

    public CatalogIssueKind Kind { get; }
    public string Message { get; }
    public string? Path { get; }
    public string? Detail { get; }
}
