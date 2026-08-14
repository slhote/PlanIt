using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Tests.Application.Similarity;

internal static class WorkItemTestFactory
{
    public static WorkItem Create(
        Guid? id = null,
        string title = "",
        string? description = null,
        Guid? assigneeId = null,
        IEnumerable<string>? tags = null,
        WorkItemStatus status = WorkItemStatus.ToDo) => new()
    {
        Id = id ?? Guid.NewGuid(),
        WorkItemType = WorkItemType.Task,
        ProjectId = Guid.NewGuid(),
        Title = title,
        Description = description,
        Status = status,
        AssigneeId = assigneeId,
        Tags = tags?.ToList() ?? new List<string>(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
