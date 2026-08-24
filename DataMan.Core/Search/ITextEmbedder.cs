namespace DataMan.Core.Search;

public readonly record struct EmbeddingModel
{
    public string Id { get; }
    public int Dimensions { get; }

    public EmbeddingModel(string id, int dimensions)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Model id is required.", nameof(id));
        }

        if (dimensions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        }

        Id = id;
        Dimensions = dimensions;
    }
}

public readonly struct EmbeddingVector
{
    public EmbeddingModel Model { get; }
    public IReadOnlyList<float> Values { get; }

    public EmbeddingVector(EmbeddingModel model, float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != model.Dimensions)
        {
            throw new ArgumentException(
                $"Expected {model.Dimensions} floats, got {values.Length}.",
                nameof(values));
        }

        Model = model;
        Values = values.ToArray();
    }
}

public interface ITextEmbedder
{
    EmbeddingModel Model { get; }
    bool IsAvailable { get; }
    EmbeddingVector? TryEmbed(string text);
}
