namespace DataMan.Core.Search;

public sealed class UnavailableEmbedder : ITextEmbedder
{
    public EmbeddingModel Model { get; } = new("unavailable", 0);

    public bool IsAvailable => false;

    public EmbeddingVector? TryEmbed(string text) => null;
}
