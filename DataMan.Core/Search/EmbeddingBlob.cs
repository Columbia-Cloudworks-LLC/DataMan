using System.Buffers.Binary;

namespace DataMan.Core.Search;

internal static class EmbeddingBlob
{
    public static byte[] ToBlob(EmbeddingVector vector)
    {
        var blob = new byte[vector.Values.Count * sizeof(float)];
        for (var i = 0; i < vector.Values.Count; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(blob.AsSpan(i * sizeof(float)), vector.Values[i]);
        }

        return blob;
    }

    public static float[] FromBlob(byte[] blob, int dimensions)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length != dimensions * sizeof(float))
        {
            throw new ArgumentException(
                $"Expected {dimensions * sizeof(float)} bytes, got {blob.Length}.",
                nameof(blob));
        }

        var values = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(blob.AsSpan(i * sizeof(float)));
        }

        return values;
    }

    public static float Cosine(EmbeddingVector a, EmbeddingVector b)
    {
        if (!string.Equals(a.Model.Id, b.Model.Id, StringComparison.Ordinal)
            || a.Model.Dimensions != b.Model.Dimensions
            || a.Values.Count != b.Values.Count)
        {
            throw new InvalidOperationException("Cannot compare embeddings from different models.");
        }

        float dot = 0;
        for (var i = 0; i < a.Values.Count; i++)
        {
            dot += a.Values[i] * b.Values[i];
        }

        return dot;
    }
}
