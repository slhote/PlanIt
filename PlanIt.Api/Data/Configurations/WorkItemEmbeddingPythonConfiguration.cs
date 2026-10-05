using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Data.Configurations;

public class WorkItemEmbeddingPythonConfiguration : IEntityTypeConfiguration<WorkItemEmbeddingPython>
{
    public void Configure(EntityTypeBuilder<WorkItemEmbeddingPython> builder)
    {
        builder.HasKey(e => e.WorkItemId);

        // all-mpnet-base-v2 output dimensionality.
        builder.Property(e => e.Vector).HasColumnType("vector(768)").IsRequired();
        builder.Property(e => e.SourceText).IsRequired();
        builder.Property(e => e.ComputedAt).IsRequired();

        builder.HasOne(e => e.WorkItem)
            .WithOne()
            .HasForeignKey<WorkItemEmbeddingPython>(e => e.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
