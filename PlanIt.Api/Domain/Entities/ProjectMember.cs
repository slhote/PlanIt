namespace PlanIt.Api.Domain.Entities;

// Composite PK (ProjectId, UserId) — configured in ProjectMemberConfiguration.
public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public ProjectMemberRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}
