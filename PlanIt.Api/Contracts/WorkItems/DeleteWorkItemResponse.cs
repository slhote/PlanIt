namespace PlanIt.Api.Contracts.WorkItems;

public record DeleteWorkItemResponse(IReadOnlyList<Guid> DeletedIds);
