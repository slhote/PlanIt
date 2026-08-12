namespace PlanIt.Api.Contracts.WorkItems;

public record FeatureDetailDto(WorkItemDto Feature, IReadOnlyList<WorkItemDto> ChildTasks);
