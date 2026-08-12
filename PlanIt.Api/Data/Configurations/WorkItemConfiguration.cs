using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlanIt.Api.Domain.Entities;

namespace PlanIt.Api.Data.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.WorkItemType).IsRequired();

        builder.Property(w => w.Title).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(4000);
        builder.Property(w => w.Status).IsRequired();
        builder.Property(w => w.Order).IsRequired();

        // Native Postgres text[], not a junction table — per-project scoped, no global Tag
        // entity, max 3, case-insensitive matching via lowercase-at-write (application layer).
        builder.Property(w => w.Tags)
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'")
            .IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkItem_TagsCardinality", "cardinality(\"Tags\") <= 3"));

        builder.Property(w => w.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(w => w.UpdatedAt).HasDefaultValueSql("now()");

        // xmin-backed optimistic concurrency — native Postgres system column, no hand-maintained
        // RowVersion column needed (planit-system-design-architecture.md §2). Npgsql.EntityFrameworkCore
        // .PostgreSQL 10.0.3 has no UseXminAsConcurrencyToken() convenience method (verified absent
        // from the assembly), so this is the manual shadow-property mapping: xmin is a Postgres
        // system column of type xid (32-bit transaction ID), surfaced as a uint row-version token.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasOne(w => w.Project)
            .WithMany(p => p.WorkItems)
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Feature -> Task cascade, DB-level defense-in-depth. The primary delete-cascade UX
        // (confirming "this will delete N tasks") lives in the service layer, not here.
        builder.HasOne(w => w.Parent)
            .WithMany(w => w.Children)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a user isn't a planned feature yet; SetNull keeps "unassigned" a normal,
        // always-valid state rather than blocking or cascading if that ever changes.
        builder.HasOne(w => w.Assignee)
            .WithMany()
            .HasForeignKey(w => w.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Board-view queries (fetch all work items for a project, grouped by parent) and the
        // Similar Tasks same-project candidate scan.
        builder.HasIndex(w => new { w.ProjectId, w.ParentId });

        // "My tasks" filter views.
        builder.HasIndex(w => w.AssigneeId);
    }
}
