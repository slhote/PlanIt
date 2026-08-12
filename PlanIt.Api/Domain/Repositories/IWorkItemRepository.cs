using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IWorkItemRepository
{
    Task<WorkItem?> GetByIdAsync(Guid id);

    // Board view — every top-level and nested work item for a project (WorkItem(ProjectId,
    // ParentId) index in planit-persistence-data-model.md).
    Task<IReadOnlyList<WorkItem>> GetForProjectAsync(Guid projectId);

    // A Feature's child Tasks.
    Task<IReadOnlyList<WorkItem>> GetChildrenAsync(Guid parentId);

    // "My tasks" filter (WorkItem(AssigneeId) index).
    Task<IReadOnlyList<WorkItem>> GetForAssigneeAsync(Guid assigneeId);

    void Add(WorkItem workItem);
    void Remove(WorkItem workItem);
    void RemoveRange(IEnumerable<WorkItem> workItems);
}
