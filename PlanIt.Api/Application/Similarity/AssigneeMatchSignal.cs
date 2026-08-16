using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application.Similarity;

public class AssigneeMatchSignal : ISimilaritySignal
{
    public double Score(WorkItem candidate, WorkItem reference) =>
        reference.AssigneeId is not null && reference.AssigneeId == candidate.AssigneeId ? 1.0 : 0.0;
}
