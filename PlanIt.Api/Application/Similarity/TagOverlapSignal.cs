using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Similarity;

// Jaccard similarity over tag sets. Tags are already stored lowercased at write time
// (WorkItemService.NormalizeTags), so no re-normalization is needed here.
public class TagOverlapSignal : ISimilaritySignal
{
    public double Score(WorkItem candidate, WorkItem reference)
    {
        if (reference.Tags.Count == 0 || candidate.Tags.Count == 0)
            return 0.0;

        var referenceTags = new HashSet<string>(reference.Tags);
        var candidateTags = new HashSet<string>(candidate.Tags);

        var intersectionCount = referenceTags.Intersect(candidateTags).Count();
        var unionCount = referenceTags.Union(candidateTags).Count();

        return unionCount == 0 ? 0.0 : (double)intersectionCount / unionCount;
    }
}
