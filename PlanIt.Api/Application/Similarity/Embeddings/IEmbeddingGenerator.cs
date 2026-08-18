namespace PlanIt.Api.Application.Similarity.Embeddings;

// One implementation per generation source (Onnx, Python) — see
// planit-similar-tasks-semantic-embeddings.md. The background worker computes via both
// unconditionally; SimilarWorkItems:EmbeddingSource only picks which table the live scoring
// path reads from.
public interface IEmbeddingGenerator
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
