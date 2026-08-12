namespace PlanIt.Api.Domain.Entities;

// Single-table (TPH) design: WorkItemType discriminates Feature vs Task.
// Invariant (enforced at the service layer, not the DB): a Feature's ParentId must be null;
// a Task's ParentId, if set, must reference a Feature, never another Task.
public class WorkItem
{
    public Guid Id { get; set; }
    public WorkItemType WorkItemType { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; }
    public Guid? AssigneeId { get; set; }

    // Fractional-index sort key, meaningful only within a (ProjectId, ParentId, Status) group —
    // one drag-and-drop column of one board view. A single-item move is a single-row update: new
    // Order = midpoint of its two new neighbors, no sibling renumbering
    // (planit-api-contracts-backend.md §6).
    public double Order { get; set; }

    // Native Postgres text[], per-project scoped, max 3, case-insensitive (stored lowercased).
    public List<string> Tags { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public WorkItem? Parent { get; set; }
    public ICollection<WorkItem> Children { get; set; } = new List<WorkItem>();
    public User? Assignee { get; set; }
}
