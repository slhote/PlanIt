using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.WorkItems;

// Id is client-generated — server upserts (idempotent create, per the master plan's idempotency
// decision). No Status field: new items always start ToDo.
public record CreateWorkItemRequest(
    Guid Id,
    WorkItemType WorkItemType,
    Guid? ParentId,
    string Title,
    string? Description,
    Guid? AssigneeId,
    IReadOnlyList<string> Tags);
