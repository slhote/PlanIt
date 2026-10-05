using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Similarity.Embeddings;

// In-process embedding generator -- sentence-transformers/all-MiniLM-L6-v2 exported to ONNX, run via Microsoft.ML.OnnxRuntime.
// No network call; the only failure modes are local (model/vocab file missing or malformed), so this fails fast in the constructor
// rather than retrying. Registered as a singleton -- the InferenceSession and tokenizer are expensive to construct and are
// thread-safe for inference, so one instance is reused across requests/background-worker items.
public class OnnxEmbeddingGenerator : IEmbeddingGenerator, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxTokens;

    public OnnxEmbeddingGenerator(IOptions<OnnxEmbeddingOptions> options)
    {
        var opts = options.Value;
        _maxTokens = opts.MaxTokens;
        _session = new InferenceSession(opts.ModelPath);
        _tokenizer = BertTokenizer.Create(opts.VocabPath, new BertOptions());
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var tokenIds = _tokenizer.EncodeToIds(text, _maxTokens, true, out _, out _, true, true);

        var seqLen = tokenIds.Count;
        var inputIds = new DenseTensor<long>(new[] { 1, seqLen });
        var attentionMask = new DenseTensor<long>(new[] { 1, seqLen });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, seqLen });
        for (var i = 0; i < seqLen; i++)
        {
            inputIds[0, i] = tokenIds[i];
            attentionMask[0, i] = 1;
            tokenTypeIds[0, i] = 0;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds),
        };

        using var results = _session.Run(inputs);
        
        // [1, seqLen, hiddenSize] token embeddings -- mean-pooled over the attention mask and L2-normalized,
        // the standard sentence-transformers sentence-embedding recipe.
        var lastHiddenState = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var hiddenSize = lastHiddenState.Dimensions[2];

        var pooled = new float[hiddenSize];
        for (var t = 0; t < seqLen; t++)
        {
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] += lastHiddenState[0, t, h];
            }
        }
        for (var h = 0; h < hiddenSize; h++)
        {
            pooled[h] /= seqLen;
        }

        var norm = MathF.Sqrt(pooled.Sum(v => v * v));
        if (norm > 0f)
        {
            for (var h = 0; h < hiddenSize; h++)
            {
                pooled[h] /= norm;
            }
        }

        return Task.FromResult(pooled);
    }

    public void Dispose() => _session.Dispose();
}