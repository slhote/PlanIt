using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Data.Configurations;

public class WorkItemEmbeddingOnnxConfiguration : IEntityTypeConfiguration<WorkItemEmbeddingOnnx>
{
    public void Configure(EntityTypeBuilder<WorkItemEmbeddingOnnx> builder)
    {
        builder.HasKey(e => e.WorkItemId);

        // all-MiniLM-L6-v2 output dimensionality.
        builder.Property(e => e.Vector).HasColumnType("vector(384)").IsRequired();
        builder.Property(e => e.SourceText).IsRequired();
        builder.Property(e => e.ComputedAt).IsRequired();

        builder.HasOne(e => e.WorkItem)
            .WithOne()
            .HasForeignKey<WorkItemEmbeddingOnnx>(e => e.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
