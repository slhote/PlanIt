using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly PlanItDbContext _db;

    public ProjectMemberRepository(PlanItDbContext db) => _db = db;

    public Task<ProjectMember?> GetAsync(Guid projectId, Guid userId) =>
        _db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);

    public async Task<IReadOnlyList<ProjectMember>> GetForProjectAsync(Guid projectId) =>
        await _db.ProjectMembers
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .ToListAsync();

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId) =>
        _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);

    public void Add(ProjectMember member) => _db.ProjectMembers.Add(member);

    public void Remove(ProjectMember member) => _db.ProjectMembers.Remove(member);
}
