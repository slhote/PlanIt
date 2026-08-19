using Microsoft.Extensions.Options;
using Pgvector;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;
using PlanIt.Api.Startup.Options;

namespace PlanIt.Api.Application.Similarity;

// Batch fetches precomputed vectors for the reference + candidate pool up front, rather than a per-candidate lookup inside Score.
public class EmbeddingSimilaritySignal(IWorkItemEmbeddingRepository embeddingRepository, IOptions<SimilarWorkItemsOptions> options)
    : ISimilaritySignal
{
    private IReadOnlyDictionary<Guid, Vector> _vectors = new Dictionary<Guid, Vector>();

    public void Prepare(WorkItem reference, IReadOnlyList<WorkItem> candidates)
    {
        var source = ParseSource(options.Value.EmbeddingSource);
        var ids = new List<Guid>(candidates.Count + 1) { reference.Id };
        ids.AddRange(candidates.Select(c => c.Id));

        // Sync-over-async: ISimilaritySignal.Prepare is deliberately synchronous, and every
        // other signal is pure in-memory -- this is the only one with a real I/O dependency.
        // Safe here because ASP.NET Core/Kestrel has no SynchronizationContext to deadlock
        // against (unlike classic ASP.NET). Widening Prepare to async would ripple across every
        // signal for the sake of one; not worth it at this scale.
        _vectors = embeddingRepository.GetVectorsAsync(source, ids).GetAwaiter().GetResult();
    }

    public double Score(WorkItem candidate, WorkItem reference)
    {
        if (!_vectors.TryGetValue(candidate.Id, out var candidateVector) ||
            !_vectors.TryGetValue(reference.Id, out var referenceVector))
        {
            // Not yet computed (or the sweep hasn't caught up) -- doesn't benefit from this signal's contribution,
            // same as a candidate with no tags scoring 0.0 on TagOverlapSignal, rather than being excluded from the pool.
            return 0.0;
        }

        return CosineSimilarity(candidateVector.ToArray(), referenceVector.ToArray());
    }

    // Both generators L2-normalize their output (OnnxEmbeddingGenerator and the Python service's normalize_embeddings=True),
    // so this is really just a dot product -- computed generally here rather than assuming that invariant holds forever.
    // Clamped to [0.0, 1.0]: raw cosine similarity ranges [-1.0, 1.0], but ISimilaritySignal.Score's contract is
    // [0.0, 1.0] -- negative cosine (semantically opposed text) is treated as "no similarity", not a negative score.
    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0.0 || normB == 0.0)
        {
            return 0.0;
        }

        return Math.Max(0.0, dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    private static EmbeddingSource ParseSource(string value) =>
        Enum.TryParse<EmbeddingSource>(value, ignoreCase: true, out var parsed) ? parsed : EmbeddingSource.Onnx;
}
