using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Data.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly PlanItDbContext _db;

    public ProjectRepository(PlanItDbContext db) => _db = db;

    public Task<Project?> GetByIdAsync(Guid id) =>
        _db.Projects.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<Project>> GetForUserAsync(Guid userId) =>
        await _db.Projects
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .ToListAsync();

    public void Add(Project project) => _db.Projects.Add(project);

    public void Remove(Project project) => _db.Projects.Remove(project);
}
