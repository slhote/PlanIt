using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Contracts.WorkItems;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

public class WorkItemService(IWorkItemRepository workItemRepository, IUserRepository userRepository, PlanItDbContext db)
{
    private const int MaxTags = 3;
    private const double OrderGap = 1024;

    public async Task<WorkItemDto> GetByIdAsync(Guid id)
    {
        var workItem = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");
        return WorkItemMapper.ToDto(workItem, db);
    }

    public async Task<FeatureDetailDto> GetFeatureDetailAsync(Guid id)
    {
        var feature = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");

        if (feature.WorkItemType != WorkItemType.Feature)
        {
            throw new TaskNotFoundException($"Work item {id} is not a Feature.");
        }

        var children = await workItemRepository.GetChildrenAsync(id);
        var childDtos = children.Select(c => WorkItemMapper.ToDto(c, db)).ToList();

        return new FeatureDetailDto(WorkItemMapper.ToDto(feature, db), childDtos);
    }

    // Client-generated GUID id, server upsert: a retried create with the same Id is a no-op
    // returning the already-created item, not a second insert or a validation re-run (master
    // plan's idempotent-create decision).
    public async Task<WorkItemDto> CreateAsync(Guid projectId, CreateWorkItemRequest request)
    {
        var existing = await workItemRepository.GetByIdAsync(request.Id);
        if (existing is not null)
        {
            return WorkItemMapper.ToDto(existing, db);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Title is required.");
        }

        WorkItem? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await workItemRepository.GetByIdAsync(parentId)
                ?? throw new TaskNotFoundException($"Parent work item {parentId} not found.");
        }
        ValidateHierarchy(request.WorkItemType, request.ParentId, parent);

        if (request.AssigneeId is { } assigneeId && await userRepository.GetByIdAsync(assigneeId) is null)
        {
            throw new TaskNotFoundException($"User {assigneeId} not found.");
        }

        var siblings = await workItemRepository.GetForProjectAsync(projectId);
        var order = NextOrder(siblings, request.ParentId, WorkItemStatus.ToDo);

        var workItem = new WorkItem
        {
            Id = request.Id,
            WorkItemType = request.WorkItemType,
            ProjectId = projectId,
            ParentId = request.ParentId,
            Title = request.Title,
            Description = request.Description,
            Status = WorkItemStatus.ToDo,
            AssigneeId = request.AssigneeId,
            Tags = NormalizeTags(request.Tags),
            Order = order,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        workItemRepository.Add(workItem);
        await db.SaveChangesAsync();

        return WorkItemMapper.ToDto(workItem, db);
    }

    public async Task<WorkItemDto> UpdateAsync(Guid id, UpdateWorkItemRequest request)
    {
        var workItem = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");

        var currentRowVersion = db.Entry(workItem).Property<uint>("xmin").CurrentValue;
        if (request.RowVersion is { } expectedRowVersion && expectedRowVersion != currentRowVersion)
        {
            throw new ConcurrencyConflictException($"Work item {id} was modified since it was last read; reload and try again.");
        }

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ValidationException("Title cannot be blank.");
            }
            workItem.Title = request.Title;
        }

        if (request.Description is not null)
        {
            workItem.Description = request.Description;
        }

        if (request.AssigneeId is { } assigneeId)
        {
            if (await userRepository.GetByIdAsync(assigneeId) is null)
            {
                throw new TaskNotFoundException($"User {assigneeId} not found.");
            }
            workItem.AssigneeId = assigneeId;
        }

        if (request.Tags is not null)
        {
            workItem.Tags = NormalizeTags(request.Tags);
        }

        if (request.Order is { } order)
        {
            workItem.Order = order;
        }

        if (request.Status is { } newStatus && newStatus != workItem.Status)
        {
            workItem.Status = newStatus;

            // Completion cascade: completing a Feature completes its Tasks too. The reverse never
            // cascades (un-completing a parent doesn't touch already-done children). Tasks have no
            // children, so there's nothing to cascade from a Task's own status change
            // (planit-api-contracts-backend.md §3).
            if (workItem.WorkItemType == WorkItemType.Feature && newStatus == WorkItemStatus.Completed)
            {
                var children = await workItemRepository.GetChildrenAsync(workItem.Id);
                foreach (var child in children)
                {
                    child.Status = WorkItemStatus.Completed;
                    child.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        workItem.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException($"Work item {id} was modified concurrently; reload and try again.", ex);
        }

        return WorkItemMapper.ToDto(workItem, db);
    }

    // Deleting a Feature deletes its child Tasks too (DB FK cascade is defense-in-depth; this is
    // the primary path so the response can report every id that was actually removed). Deleting a
    // Task is always a single-row delete (planit-api-contracts-backend.md §3).
    public async Task<DeleteWorkItemResponse> DeleteAsync(Guid id)
    {
        var workItem = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");

        var deletedIds = new List<Guid> { id };

        if (workItem.WorkItemType == WorkItemType.Feature)
        {
            var children = await workItemRepository.GetChildrenAsync(id);
            if (children.Count > 0)
            {
                deletedIds.AddRange(children.Select(c => c.Id));
                workItemRepository.RemoveRange(children);
            }
        }

        workItemRepository.Remove(workItem);
        await db.SaveChangesAsync();

        return new DeleteWorkItemResponse(deletedIds);
    }

    // A Feature's ParentId must be null; a Task's ParentId, if set, must point to a Feature
    // (never another Task — Tasks have no children).
    private static void ValidateHierarchy(WorkItemType type, Guid? parentId, WorkItem? parent)
    {
        if (type == WorkItemType.Feature && parentId is not null)
        {
            throw new ValidationException("A Feature cannot have a parent.");
        }

        if (type == WorkItemType.Task && parentId is not null && parent!.WorkItemType != WorkItemType.Feature)
        {
            throw new ValidationException("A Task's parent, if set, must be a Feature.");
        }
    }

    private static List<string> NormalizeTags(IReadOnlyList<string> tags)
    {
        var normalized = tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

        if (normalized.Count > MaxTags)
        {
            throw new ValidationException($"A work item can have at most {MaxTags} tags.");
        }

        return normalized;
    }

    // New items land at the end of their column: (ProjectId, ParentId, Status) is the grouping
    // scope for Order (planit-api-contracts-backend.md §6) — comparing Order across different
    // parents/statuses is meaningless.
    private static double NextOrder(IReadOnlyList<WorkItem> siblings, Guid? parentId, WorkItemStatus status)
    {
        var maxOrder = siblings
            .Where(w => w.ParentId == parentId && w.Status == status)
            .Select(w => (double?)w.Order)
            .Max();

        return (maxOrder ?? 0) + OrderGap;
    }
}
