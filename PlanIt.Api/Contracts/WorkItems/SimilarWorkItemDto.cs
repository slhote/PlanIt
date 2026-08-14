namespace PlanIt.Api.Contracts.WorkItems;

// Deliberate small extension beyond the bare WorkItemSummaryDto[] originally sketched in
// planit-api-contracts-backend.md §2 — exposes the ranking score for explainability
// (planit-similar-tasks-lexical-metadata.md).
public record SimilarWorkItemDto(WorkItemSummaryDto WorkItem, double Score);
