using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Similarity;

// Extensibility seam for Similar Tasks scoring (planit-similar-tasks-lexical-metadata.md).
// Prepare is a per-request hook for signals that need corpus-wide context before scoring
// individual pairs (e.g. TF-IDF document frequencies) — the same seam a future
// EmbeddingSimilaritySignal will use to batch-fetch/compute vectors up front.
public interface ISimilaritySignal
{
    void Prepare(WorkItem reference, IReadOnlyList<WorkItem> candidates) { }

    // Returns a normalized score in [0.0, 1.0].
    double Score(WorkItem candidate, WorkItem reference);
}
