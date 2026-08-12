using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.WorkItems;

// RowVersion is already available (xmin is mapped in WorkItemConfiguration). Order is NOT here
// yet — planit-api-contracts-backend.md §6 adds a new WorkItem.Order column + migration in step 5;
// this step only exposes what already exists in the schema.
public record WorkItemDto(
    Guid Id,
    WorkItemType WorkItemType,
    Guid ProjectId,
    Guid? ParentId,
    string Title,
    string? Description,
    WorkItemStatus Status,
    Guid? AssigneeId,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint RowVersion);
