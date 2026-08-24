using System.Security.Cryptography;
using System.Text;
using DataMan.Contracts;

namespace DataMan.Plugins.SampleCsv;

public sealed class CsvTextPlugin : IIngestionPlugin
{
    public string Id => "dataman.plugin.samplecsv";
    public string DisplayName => "Sample CSV";
    public string Version => "1.0.0";
    public IReadOnlyList<string> SupportedSchemesOrExtensions { get; } = [".csv"];

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
                Subtype = "csv",
                Title = Path.GetFileName(locator.Path),
                ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant(),
                OriginalHash = context.OriginalHash,
                LocatorJson = context.LocatorJson,
                MimeType = "text/csv",
                SizeBytes = context.SizeBytes,
                SourceCreatedAt = context.SourceCreatedAt,
                SourceUpdatedAt = context.SourceUpdatedAt
            },
            new ContentDraft
            {
                ContentType = "plain",
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
