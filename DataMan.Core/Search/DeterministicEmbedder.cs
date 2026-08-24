namespace DataMan.Core.Search;

public sealed class DeterministicEmbedder : ITextEmbedder
{
    public EmbeddingModel Model { get; } = new("deterministic-hash-v1", 32);

    public bool IsAvailable => true;

    public EmbeddingVector? TryEmbed(string text)
    {
        var bins = new float[Model.Dimensions];
        foreach (var token in Tokens(text))
        {
            bins[StableBin(token, Model.Dimensions)] += 1f;
        }

        Normalize(bins);
        return new EmbeddingVector(Model, bins);
    }

    private static IEnumerable<string> Tokens(string text)
    {
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetterOrDigit(text[i]))
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                yield return text[start..i].ToLowerInvariant();
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return text[start..].ToLowerInvariant();
        }
    }

    private static int StableBin(string token, int bins)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in token)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return (int)(hash % (uint)bins);
        }
    }

    private static void Normalize(float[] values)
    {
        double sumSquares = 0;
        foreach (var value in values)
        {
            sumSquares += value * value;
        }

        if (sumSquares <= 0)
        {
            values[0] = 1f;
            return;
        }

        var scale = (float)(1.0 / Math.Sqrt(sumSquares));
        for (var i = 0; i < values.Length; i++)
        {
            values[i] *= scale;
        }
    }
}
