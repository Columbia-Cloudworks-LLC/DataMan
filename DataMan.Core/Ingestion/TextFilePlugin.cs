using DataMan.Contracts;

namespace DataMan.Core.Ingestion;

public abstract class TextFilePlugin : IIngestionPlugin
{
    protected TextFilePlugin(string id, string displayName, string subtype, string contentType, string mimeType, params string[] extensions)
    {
        Id = id;
        DisplayName = displayName;
        Subtype = subtype;
        ContentType = contentType;
        MimeType = mimeType;
        SupportedSchemesOrExtensions = extensions;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Version => "1.0.0";
    public IReadOnlyList<string> SupportedSchemesOrExtensions { get; }
    private string Subtype { get; }
    private string ContentType { get; }
    private string MimeType { get; }

    public async Task<IngestionResult> IngestAsync(IngestionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.LocalPath) || !File.Exists(context.LocalPath))
        {
            return new IngestionResult { Success = false, ErrorMessage = "Local file path is missing." };
        }

        string body;
        try
        {
            body = await File.ReadAllTextAsync(context.LocalPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return new IngestionResult { Success = false, ErrorMessage = ex.Message };
        }

        var locator = FileLocator.Parse(context.LocatorJson);
        var itemId = await context.Writer.UpsertItemAsync(
            new ItemDraft
            {
                SourceId = context.SourceId,
                ParentItemId = context.ParentItemId,
                Kind = "file",
                Subtype = Subtype,
                Title = Path.GetFileName(locator.Path),
                ContentHash = ContentHasher.Sha256Text(body),
                OriginalHash = context.OriginalHash,
                LocatorJson = context.LocatorJson,
                MimeType = MimeType,
                SizeBytes = context.SizeBytes,
                SourceCreatedAt = context.SourceCreatedAt,
                SourceUpdatedAt = context.SourceUpdatedAt
            },
            new ContentDraft
            {
                ContentType = ContentType,
                Body = body,
                ExtractedBy = $"{Id}@{Version}"
            },
            cancellationToken);

        return new IngestionResult
        {
            Success = true,
            CreatedItemIds = [itemId]
        };
    }
}

public sealed class PlainTextPlugin() : TextFilePlugin(
    "dataman.plugin.plaintext",
    "Plain text",
    "text",
    "plain",
    "text/plain",
    ".txt");

public sealed class MarkdownPlugin() : TextFilePlugin(
    "dataman.plugin.markdown",
    "Markdown",
    "markdown",
    "markdown",
    "text/markdown",
    ".md",
    ".markdown");

public sealed class LogFilePlugin() : TextFilePlugin(
    "dataman.plugin.log",
    "Log files",
    "log",
    "plain",
    "text/plain",
    ".log");
