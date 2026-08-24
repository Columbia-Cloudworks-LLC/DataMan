using DataMan.Core.Search;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace DataMan;

public sealed class OnnxTextEmbedder : ITextEmbedder, IDisposable
{
    public const string ModelFileName = "all-MiniLM-L6-v2.onnx";
    public const string VocabFileName = "vocab.txt";
    private const int MaxTokens = 256;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    private OnnxTextEmbedder(string modelPath, string vocabPath)
    {
        var session = new InferenceSession(modelPath);
        try
        {
            var tokenizer = BertTokenizer.Create(vocabPath, new BertOptions
            {
                LowerCaseBeforeTokenization = true
            });
            _session = session;
            _tokenizer = tokenizer;
            Model = new EmbeddingModel("all-minilm-l6-v2", ReadOutputWidth(session));
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public EmbeddingModel Model { get; }

    public bool IsAvailable => true;

    public static OnnxTextEmbedder? TryCreate()
    {
        foreach (var directory in CandidateDirectories())
        {
            var created = TryCreate(Path.Combine(directory, ModelFileName));
            if (created is not null)
            {
                return created;
            }
        }

        return null;
    }

    public static OnnxTextEmbedder? TryCreate(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(modelPath);
        if (directory is null)
        {
            return null;
        }

        var vocabPath = Path.Combine(directory, VocabFileName);
        if (!File.Exists(vocabPath))
        {
            return null;
        }

        try
        {
            return new OnnxTextEmbedder(modelPath, vocabPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public EmbeddingVector? TryEmbed(string text)
    {
        try
        {
            var ids = _tokenizer.EncodeToIds(text, MaxTokens, addSpecialTokens: true, out _, out _);
            if (ids.Count == 0)
            {
                return null;
            }

            using var results = _session.Run(BuildInputs(ids));
            var hidden = results[0].AsTensor<float>();
            var pooled = MeanPool(hidden, ids.Count);
            if (pooled.Length != Model.Dimensions)
            {
                return null;
            }

            Normalize(pooled);
            return new EmbeddingVector(Model, pooled);
        }
        catch (OnnxRuntimeException)
        {
            return null;
        }
    }

    public void Dispose() => _session.Dispose();

    private List<NamedOnnxValue> BuildInputs(IReadOnlyList<int> ids)
    {
        var seq = ids.Count;
        var shape = new[] { 1, seq };
        var inputIds = new DenseTensor<long>(shape);
        var attention = new DenseTensor<long>(shape);
        var types = new DenseTensor<long>(shape);
        for (var i = 0; i < seq; i++)
        {
            inputIds[0, i] = ids[i];
            attention[0, i] = 1;
        }

        var inputs = new List<NamedOnnxValue>(_session.InputMetadata.Count);
        foreach (var name in _session.InputMetadata.Keys)
        {
            if (name.Contains("mask", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, attention));
            }
            else if (name.Contains("type", StringComparison.OrdinalIgnoreCase))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, types));
            }
            else
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(name, inputIds));
            }
        }

        return inputs;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        yield return AppPaths.ModelsDirectory;
        yield return Path.Combine(AppContext.BaseDirectory, "models");
    }

    private static int ReadOutputWidth(InferenceSession session)
    {
        var dims = session.OutputMetadata.First().Value.Dimensions;
        var width = dims.Length == 0 ? 0 : dims[^1];
        return width > 0 ? width : 384;
    }

    private static float[] MeanPool(Tensor<float> hidden, int seq)
    {
        var rank = hidden.Dimensions.Length;
        if (rank == 2)
        {
            var already = new float[hidden.Dimensions[1]];
            for (var d = 0; d < already.Length; d++)
            {
                already[d] = hidden[0, d];
            }

            return already;
        }

        var width = hidden.Dimensions[rank - 1];
        var pooled = new float[width];
        for (var t = 0; t < seq; t++)
        {
            for (var d = 0; d < width; d++)
            {
                pooled[d] += hidden[0, t, d];
            }
        }

        var scale = 1f / seq;
        for (var d = 0; d < width; d++)
        {
            pooled[d] *= scale;
        }

        return pooled;
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
