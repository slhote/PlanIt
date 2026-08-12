using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Contracts.WorkItems;

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
    double Order,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint RowVersion);
