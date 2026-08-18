using Pgvector;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IWorkItemEmbeddingRepository
{
    // Batch fetch for EmbeddingSimilaritySignal.Prepare — one query for the reference +
    // candidate pool, not a per-candidate lookup. Missing rows (not yet computed) are simply
    // absent from the result rather than erroring.
    Task<IReadOnlyDictionary<Guid, Vector>> GetVectorsAsync(EmbeddingSource source, IReadOnlyList<Guid> workItemIds);

    // Insert-or-update, keyed on WorkItemId. Used by the background worker (event-driven queue,
    // periodic sweep, recompute-all) — every trigger writes to both sources unconditionally.
    Task UpsertAsync(EmbeddingSource source, Guid workItemId, Vector vector, string sourceText, DateTimeOffset computedAt);
}
