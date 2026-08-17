using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class WorkItemRepository : IWorkItemRepository
{
    private readonly PlanItDbContext _db;

    public WorkItemRepository(PlanItDbContext db) => _db = db;

    public Task<WorkItem?> GetByIdAsync(Guid id) =>
        _db.WorkItems.FirstOrDefaultAsync(w => w.Id == id);

    public async Task<IReadOnlyList<WorkItem>> GetForProjectAsync(Guid projectId) =>
        await _db.WorkItems
            .Where(w => w.ProjectId == projectId)
            .ToListAsync();

    public async Task<IReadOnlyList<WorkItem>> GetChildrenAsync(Guid parentId) =>
        await _db.WorkItems
            .Where(w => w.ParentId == parentId)
            .ToListAsync();

    public async Task<IReadOnlyList<WorkItem>> GetForAssigneeAsync(Guid assigneeId) =>
        await _db.WorkItems
            .Where(w => w.AssigneeId == assigneeId)
            .ToListAsync();

    public async Task<IReadOnlyList<WorkItem>> GetSimilarityCandidatesAsync(
        Guid projectId, Guid referenceId, Guid? referenceParentId) =>
        await _db.WorkItems
            .Where(w => w.ProjectId == projectId
                && w.Id != referenceId
                && w.ParentId != referenceId
                && w.Status != WorkItemStatus.Completed
                && (referenceParentId == null
                    || (w.Id != referenceParentId && w.ParentId != referenceParentId)))
            .ToListAsync();

    public void Add(WorkItem workItem) => _db.WorkItems.Add(workItem);

    public void Remove(WorkItem workItem) => _db.WorkItems.Remove(workItem);

    public void RemoveRange(IEnumerable<WorkItem> workItems) => _db.WorkItems.RemoveRange(workItems);
}
