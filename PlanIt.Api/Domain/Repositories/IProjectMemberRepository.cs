using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetAsync(Guid projectId, Guid userId);
    Task<IReadOnlyList<ProjectMember>> GetForProjectAsync(Guid projectId);

    // Backs the 404-not-403 unauthorized-access rule (System Design §4) — a single uniform
    // membership check, never a repeated OR/UNION against Project.CreatedByUserId.
    Task<bool> IsMemberAsync(Guid projectId, Guid userId);

    void Add(ProjectMember member);
    void Remove(ProjectMember member);
}
