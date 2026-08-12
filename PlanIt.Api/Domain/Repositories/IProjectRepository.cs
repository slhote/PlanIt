using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Domain.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);

    // "My projects" — every project the user is a ProjectMember of (per the
    // ProjectMember(UserId) index rationale in planit-persistence-data-model.md).
    Task<IReadOnlyList<Project>> GetForUserAsync(Guid userId);

    void Add(Project project);
    void Remove(Project project);
}
