# Production Migration Best Practices

## Overview

This document captures best practices for managing database migrations in production codebases, based on lessons learned from mature projects and industry standards.

---

## The Five Core Disciplines

### 1. Never Modify an Existing Migration After It's Committed

**Rule:** Once a migration has been merged to the main branch or applied to any production database, it is immutable. If you need to change it, create a new migration.

**Why:** 
- A migration applied to prod is already executed against a real database
- Modifying it retroactively creates a schema divergence between production and development
- Different developers may have already applied the old version locally
- Rollback/replay becomes unpredictable if the migration history isn't stable

**What to do if you realize a migration has a bug after committing:**
- Create a *new* migration that applies a fix (e.g., if migration 5 made a column `NOT NULL` without a default, migration 6 can add the default and backfill)
- Never rewrite migration 5 after it's committed
- Document the reason in a comment on migration 6

### 2. Always Test the Full Migration Sequence

**Rule:** Before deploying a release, test that the entire migration sequence (from a clean database) produces the expected schema.

**How:**
```bash
# On a fresh test database or container:
dotnet ef database update

# Verify the schema matches expectations:
# - All tables exist
# - All columns present with correct types
# - Constraints in place
# - Indexes created
```

**Why:**
- Catches "migration 5 breaks migration 7" bugs before prod
- Ensures new developers can apply all migrations cleanly
- Documents the total schema at each version boundary

**In CI/CD:**
```yaml
- name: Test Full Migration Sequence
  run: |
    # Spin up a fresh Postgres container
    docker run -d --name test-db postgres:16
    
    # Apply all migrations
    dotnet ef database update --connection "Host=localhost;Database=PlanIt;..."
    
    # Run schema validation tests
    dotnet test --filter Category=MigrationIntegration
```

### 3. Ensure Backwards Compatibility During Rollout

**Rule:** Database migrations may be applied before, during, or after code deployment. Ensure old code can still function with the new schema (or explicitly plan for downtime).

**Backwards-Compatible Changes:**
- Adding a nullable column (old code ignores it)
- Adding a column with a default value (old code ignores it)
- Adding an index (doesn't affect DML operations)
- Relaxing a constraint (e.g., making nullable)

**Incompatible Changes (require strategy):**
- Adding a required column without a default
- Removing a column
- Renaming a column
- Changing a column type
- Making a nullable column required

**How to handle incompatible changes:**

**Option A: Make it backwards-compatible (preferred)**
```sql
-- Instead of:
ALTER TABLE "Users" ADD COLUMN "Department" varchar(100) NOT NULL;

-- Do:
ALTER TABLE "Users" ADD COLUMN "Department" varchar(100) NULL DEFAULT 'Unknown';

-- Old code works (column exists, has a value)
-- New code works (can set the column)
-- No downtime needed
```

**Option B: Two-phase migration (requires coordination)**
```sql
-- Migration 1 (runs first):
ALTER TABLE "Users" ADD COLUMN "Department" varchar(100) NULL;
-- Old code continues working, new code starts populating Department

-- Migration 2 (only after all old instances are rolled to new code):
ALTER TABLE "Users" ALTER COLUMN "Department" SET NOT NULL;
```

**Option C: Blue-green deployment (full downtime is acceptable)**
- Deploy new code to a separate environment
- Run migration
- Verify functionality
- Switch traffic (all at once, no overlapping versions)
- This is the safest but slowest approach

**Testing:** Write backwards-compatibility tests (see `migration-backwards-compatibility-testing.cs`) to catch incompatible changes before they reach prod.

### 4. Have a Documented Rollback Strategy

**Rule:** Every deployment must have a rollback plan that includes both code and database.

**Rollback Plan:**
```bash
# 1. Rollback the code deployment (usual process)
#    — This restarts the old version of the API

# 2. Rollback the database (if needed)
dotnet ef database update --target <previous-migration-name>

# Example:
dotnet ef database update --target 20260805_AddUserDepartment
# This runs the Down() method of all migrations after it, reverting the schema
```

**Why `Down()` methods matter:**
Every migration must have a complete `Down()` implementation that reverses the `Up()` method. Without it, you can't rollback.

```csharp
protected override void Up(MigrationBuilder mb)
{
    mb.CreateTable("Users", /* ... */);
}

protected override void Down(MigrationBuilder mb)
{
    mb.DropTable("Users");  // Must be complete and correct
}
```

**Data loss risk:** `Down()` may lose data (e.g., dropping a table). Document this risk in the migration's XML comments:

```csharp
/// <summary>
/// Adds the Department column to Users.
/// WARNING: Rolling back this migration will not preserve Department data
/// (it is not stored elsewhere).
/// </summary>
public partial class AddUserDepartment : Migration { ... }
```

### 5. Enforce Discipline in Code Review and CI

**Rule:** Migrations are high-risk changes and deserve extra scrutiny.

**CI Checks:**

1. **Detect unmigrated schema changes:**
   ```bash
   dotnet ef migrations has-pending-model-changes --exit-code
   ```
   Fails the build if someone changed entities but forgot to add a migration.

2. **Run migration integration tests:**
   ```bash
   dotnet test --filter Category=MigrationIntegration
   ```

3. **Run backwards-compatibility tests:**
   ```bash
   dotnet test --filter Category=MigrationBackwardsCompatibility
   ```

**Code Review Checklist:**

- [ ] Does the migration file name describe the change? (e.g., `AddUserDepartmentColumn`, not `Migration42`)
- [ ] Do the `Up()` and `Down()` methods match each other exactly (inverse)?
- [ ] Is the migration generated from EF Core (`dotnet ef migrations add`), or hand-edited? If hand-edited, is there a comment explaining why?
- [ ] Does the migration create/modify constraints (unique, FK, check)? Verify they match the persistence design doc.
- [ ] If data transformation is needed (renaming column, splitting data), is the migration correct and documented?
- [ ] Are there any data-loss operations? If yes, is the risk documented in the Down() method's XML comment?
- [ ] Did integration tests pass? Do they cover the new constraints/defaults?
- [ ] Is this change backwards-compatible? If not, is the deployment plan documented?

---

## The Reality at Scale

### Migrations Accumulate Over Time

A mature project accumulates 50-100+ migrations over 2-3 years. This is normal and expected.

```
Migrations/
├─ 20250101_InitialCreate.cs
├─ 20250115_AddProjectsTable.cs
├─ 20250201_AddWorkItemsTable.cs
├─ 20250220_AddCascadeDeleteToWorkItems.cs
├─ 20250305_AddUserDepartmentColumn.cs
├─ 20250320_DropLegacySettings.cs
├─ 20250401_AddIndexesToWorkItems.cs
... (many more) ...
└─ 20260815_AddRefreshTokenRotationSupport.cs
```

### Why Not Consolidate?

**Consolidation (squashing migrations)** combines multiple migrations into one. While it reduces file count, it's risky and rarely done in production:

**Consolidation is safe ONLY if:**
- No customer database has ever applied the old migrations (e.g., pre-launch)
- You're dropping support for old upgrade paths (e.g., v1.0 → v2.0, and no one runs v1.x anymore)
- You're confident the squashed migration is correct (easy to introduce bugs)

**Consolidation is UNSAFE if:**
- Any production database has applied the old migrations
- You need to support customer upgrades from old versions
- The migration history is useful documentation of schema evolution

**Industry standard:** Just let migrations accumulate. The history is valuable documentation.

### Migrations as Audit Trail

Over time, migrations become a chronological record of schema evolution:
- Why was a column added? (Look at the migration date and the commit message)
- When did we stop using a feature? (Look for a column drop migration)
- What did the schema look like on a given date? (Apply migrations up to that date)

This is invaluable for debugging "why is this old data in the format it is?" questions.

---

## Common Patterns and Pitfalls

### Pattern: Two-Phase Nullable Columns

When adding a required column:

```csharp
// Migration 1: Add nullable
public override void Up(MigrationBuilder mb)
{
    mb.AddColumn<string>(
        name: "Department",
        table: "Users",
        nullable: true);  // Nullable, no default
}

// Migration 1 Down:
public override void Down(MigrationBuilder mb)
{
    mb.DropColumn(name: "Department", table: "Users");
}

// Later, Migration 2 (after all new code is deployed):
public override void Up(MigrationBuilder mb)
{
    // Backfill any nulls
    mb.Sql("UPDATE \"Users\" SET \"Department\" = 'Unknown' WHERE \"Department\" IS NULL");
    
    // Make it required
    mb.AlterColumn<string>(
        name: "Department",
        table: "Users",
        nullable: false);
}
```

This allows zero-downtime: old code works with nullable, new code backfills, second migration locks it down.

### Pitfall: Hand-Editing Generated Migrations

EF Core generates migrations from entities. If you hand-edit a migration *after* generation:
- The `ModelSnapshot.cs` no longer matches the migration
- Future migrations may be generated incorrectly
- CI checks fail (pending model changes detected)

**Only hand-edit migrations if:**
- You're adding raw SQL (e.g., a custom index, a trigger, a check constraint that EF can't express)
- You're refining performance (e.g., changing index type)
- You need to transform data alongside the schema change

**Always add a comment explaining why:**
```csharp
protected override void Up(MigrationBuilder mb)
{
    mb.AddColumn<string>(/* ... */);
    
    // Raw SQL for custom index that EF Core doesn't support syntax for
    mb.Sql(@"CREATE INDEX idx_workitems_project_status 
             ON ""WorkItems""(""ProjectId"", ""Status"") 
             WHERE ""Status"" != 2");  // Partial index
}
```

### Pitfall: Forgetting Down()

Every migration must have a complete `Down()` method. An incomplete or missing `Down()` means the migration can't be rolled back.

```csharp
// BAD: No Down() method
public partial class AddUserDepartment : Migration
{
    protected override void Up(MigrationBuilder mb) { /* ... */ }
    // Down() is missing — rollback is impossible
}

// GOOD: Complete Down()
public partial class AddUserDepartment : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        mb.AddColumn<string>(/* ... */);
    }

    protected override void Down(MigrationBuilder mb)
    {
        mb.DropColumn(/* ... */);
    }
}
```

---

## For PlanIt Specifically

### Current State
- **One migration:** `20260812_InitialCreate.cs` — creates all five tables
- **Status:** Pre-launch, safe to consolidate if needed

### As Development Proceeds
1. Add features → modify entities → `dotnet ef migrations add FeatureName`
2. Review the generated migration (ensure it's correct)
3. Write integration tests to verify the schema works
4. Write backwards-compatibility tests if the migration is incompatible
5. Commit the migration with the PR
6. Code review: verify `Up()` / `Down()` match, constraints are correct

### Before Launch
Decide whether to consolidate:
- **Option A (simpler):** Keep `InitialCreate` as-is. New customers start fresh, nothing to consolidate.
- **Option B (cleaner history):** If you have 5-10 migrations, squash them into one `InitialCreate` (safe pre-launch). Ensures new databases have one migration instead of ten.

### After Launch
Never consolidate. Just add migrations as the schema evolves. The history is locked in.

---

## Checklist: Before Merging a Migration PR

- [ ] Migration file name describes the change
- [ ] `Up()` method matches the entity/config changes
- [ ] `Down()` method exactly reverses `Up()`
- [ ] Backwards-compatibility is verified (or downtime is documented)
- [ ] Integration tests pass (schema is correct, constraints work)
- [ ] Backwards-compatibility tests pass (if the change requires it)
- [ ] No hand-edits unless documented with a comment
- [ ] If data transformation is involved, is the migration idempotent? (safe to re-run)
- [ ] Large tables: are data-transformation performance implications understood?
- [ ] Deployment runbook is updated (if deployment coordination is needed)
