using Microsoft.EntityFrameworkCore;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Data;

public class PlanItDbContext : DbContext
{
    public PlanItDbContext(DbContextOptions<PlanItDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WorkItemEmbeddingOnnx> WorkItemEmbeddingsOnnx => Set<WorkItemEmbeddingOnnx>();
    public DbSet<WorkItemEmbeddingPython> WorkItemEmbeddingsPython => Set<WorkItemEmbeddingPython>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Similar Tasks semantic embeddings (planit-similar-tasks-semantic-embeddings.md).
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanItDbContext).Assembly);
    }
}
