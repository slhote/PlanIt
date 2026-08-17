using PlanIt.Api.Application.Similarity;
using PlanIt.Api.Contracts.WorkItems;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class SimilarTasksService(IWorkItemRepository workItemRepository, WeightedSimilarityScorer scorer)
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
}
