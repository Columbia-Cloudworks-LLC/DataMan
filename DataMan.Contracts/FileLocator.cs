using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataMan.Contracts;

public sealed class FileLocator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("scheme")]
    public string Scheme { get; init; } = "file";

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("is_directory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsDirectory { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static FileLocator Parse(string json)
    {
        var locator = JsonSerializer.Deserialize<FileLocator>(json, JsonOptions);
        if (locator is null || string.IsNullOrWhiteSpace(locator.Path))
        {
            throw new InvalidOperationException("Locator JSON is missing a file path.");
        }

        if (!string.Equals(locator.Scheme, "file", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported locator scheme '{locator.Scheme}'.");
        }

        return locator;
    }
}
