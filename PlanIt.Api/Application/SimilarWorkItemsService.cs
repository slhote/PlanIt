using PlanIt.Api.Application.Similarity;
using PlanIt.Api.Application.Similarity.Embeddings;
using PlanIt.Api.Contracts.WorkItems;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class SimilarWorkItemsService(IWorkItemRepository workItemRepository, WeightedSimilarityScorer scorer, EmbeddingWorkQueue embeddingQueue)
{
    public async Task<IReadOnlyList<SimilarWorkItemDto>> GetSimilarAsync(Guid projectId, Guid workItemId)
    {
        var reference = await workItemRepository.GetByIdAsync(workItemId)
            ?? throw new TaskNotFoundException($"Work item {workItemId} not found.");

        var candidates = await workItemRepository.GetSimilarityCandidatesAsync(projectId, reference.Id, reference.ParentId);

        var ranked = scorer.Rank(reference, candidates);

        return ranked
            .Select(r => new SimilarWorkItemDto(WorkItemMapper.ToSummaryDto(r.WorkItem), r.Score))
            .ToList();
    }

    // On-demand bulk recompute (planit-similar-tasks-semantic-embeddings.md) -- just another
    // producer onto the same EmbeddingWorkQueue the event-driven triggers and periodic sweep use,
    // so it goes through the exact same background processing, no separate code path.
    public async Task<int> RecomputeAllAsync(Guid projectId)
    {
        var workItems = await workItemRepository.GetForProjectAsync(projectId);
        foreach (var workItem in workItems)
        {
            embeddingQueue.Enqueue(workItem.Id);
        }

        return workItems.Count;
    }
}
