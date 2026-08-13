using PlanIt.Api.Contracts.WorkItems;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Application;

// Shared WorkItem -> WorkItemDto mapping. Needs the DbContext to read the xmin shadow property
// (it isn't a regular property on the WorkItem entity, so it can't be read off the instance
// directly — see WorkItemConfiguration).
internal static class WorkItemMapper
{
    public static WorkItemDto ToDto(WorkItem workItem, PlanItDbContext db) => new(
        workItem.Id,
        workItem.WorkItemType,
        workItem.ProjectId,
        workItem.ParentId,
        workItem.Title,
        workItem.Description,
        workItem.Status,
        workItem.AssigneeId,
        workItem.Tags,
        workItem.Order,
        workItem.CreatedAt,
        workItem.UpdatedAt,
        db.Entry(workItem).Property<uint>("xmin").CurrentValue);
}
