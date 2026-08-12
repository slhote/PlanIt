// Migration Backwards-Compatibility Testing Notes
//
// OVERVIEW
// ========
// Backwards-compatibility tests protect against a specific deployment risk:
// during a rolling deployment, old code may still be running while the new database
// schema is already live. If the migration broke assumptions the old code made,
// it will fail in production, causing requests to fail until all instances have rolled.
//
// This is distinct from "migration works with new code" (caught by integration tests)
// and "data survives" (caught by sequencing tests). This tests the transition period.
//
// EXAMPLE SCENARIO WHERE THIS MATTERS
// ====================================
// 1. Current production: Users table has Username (varchar)
// 2. New migration adds: Department (varchar NOT NULL) with NO DEFAULT
// 3. Deployment happens:
//    - Database migration runs first (or simultaneously)
//    - Department column is added, required, no default
//    - Old API code (v1.0) is still running on some instances
//    - A request comes in to create a User
//    - Old code knows nothing about Department, sends: { Username: "alice", Email: "alice@example.com" }
//    - INSERT fails: Department is required, old code didn't provide it
//    - 500 error, customer impact, until all instances roll to v2.0
//
// HOW TO PREVENT THIS
// ===================
// Option A: Make the column nullable or provide a default
//   ALTER TABLE "Users" ADD COLUMN "Department" varchar(100) NULL DEFAULT 'Unknown';
//   Now old code works (Department is optional), new code works (has a value).
//   This is "zero-downtime" compatible.
//
// Option B: Use a two-migration approach
//   Migration 1: Add Department nullable, no default
//   - Old code continues working (column exists but is null)
//   - New code starts populating it
//   Migration 2 (after all old code is rolled): Make Department NOT NULL
//   - Only run this after every instance is on new code
//   This requires deployment coordination/staged rollouts.
//
// TESTING THE OLD CODE AGAINST NEW SCHEMA
// =========================================
// The test below simulates: "given the new schema, can old code still perform its operations?"
// This should FAIL if backwards compatibility is broken, forcing the developer to either:
// 1. Make the migration backwards-compatible (Option A above), or
// 2. Document the deployment coordination requirement and add to deployment runbook (Option B)
//
// KEY INSIGHT
// ===========
// This test is NOT written against the current entity model. It simulates the old code
// by using raw SQL or minimal/outdated DTOs, then applies the new migration, then tries
// to perform the old operations. If it fails, backwards compatibility is broken.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PlanIt.Api.Tests.Migrations;

[Collection("Database")]
public class MigrationBackwardsCompatibilityTests
{
    private readonly PostgresContainer _container;
    private readonly PlanItDbContext _db;

    public MigrationBackwardsCompatibilityTests()
    {
        _container = new PostgresBuilder()
            .WithImage("postgres:16")
            .Build();

        _db = new PlanItDbContext(new DbContextOptions<PlanItDbContext>
        {
            ConnectionString = _container.GetConnectionString()
        });
    }

    /// <summary>
    /// EXAMPLE: If a migration adds a required column without a default,
    /// this test catches that old code can no longer insert users.
    ///
    /// This test should FAIL until the migration is fixed to include:
    ///   - A DEFAULT value, OR
    ///   - Make the column nullable, OR
    ///   - Deployment plan documents that all old code must be rolled before applying migration
    /// </summary>
    [Fact]
    public async Task OldCodeCanInsertUser_WithoutKnowingAboutNewRequiredColumn()
    {
        // Apply the new migration (which added a required column)
        await _db.Database.MigrateAsync();

        // Simulate old code (v1.0) inserting a User
        // Old code only knows about Id, Username, Email, PasswordHash, CreatedAt
        // If a new column was added to the schema (e.g., Department), old code
        // would NOT know to provide a value for it.

        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Users"" (""Id"", ""Username"", ""Email"", ""PasswordHash"", ""CreatedAt"")
              VALUES (@id, @username, @email, @passwordHash, @createdAt)",
            new[]
            {
                new SqlParameter("@id", Guid.NewGuid()),
                new SqlParameter("@username", "alice"),
                new SqlParameter("@email", "alice@example.com"),
                new SqlParameter("@passwordHash", "hash..."),
                new SqlParameter("@createdAt", DateTime.UtcNow)
            });

        // If this assertion passes, the migration is backwards-compatible.
        // If it throws DbUpdateException (constraint violation), backwards compat is broken.
        // Resolution: update migration to add DEFAULT or make column nullable.

        var inserted = await _db.Users.CountAsync();
        Assert.Equal(1, inserted);
    }

    /// <summary>
    /// EXAMPLE: If a migration renames or removes a column, old code's queries break.
    /// This test verifies the old select query still works.
    /// </summary>
    [Fact]
    public async Task OldCodeCanQueryUserByUsername_ColumnStillExistsAndNamed()
    {
        await _db.Database.MigrateAsync();

        // Insert a user using new code
        var user = new User { Id = Guid.NewGuid(), Username = "bob", Email = "bob@example.com" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Simulate old code querying by Username
        // If Username column was renamed/removed, this raw SQL fails.
        var result = await _db.Database.SqlQueryRaw<(Guid Id, string Username)>(
            @"SELECT ""Id"", ""Username"" FROM ""Users"" WHERE ""Username"" = 'bob'")
            .FirstOrDefaultAsync();

        // If the column was renamed/removed, the above query throws before this assertion.
        Assert.NotNull(result);
        Assert.Equal("bob", result.Username);
    }

    /// <summary>
    /// EXAMPLE: If a migration changes a column type (e.g., string → int),
    /// old code's inserts fail because it sends the wrong type.
    /// </summary>
    [Fact]
    public async Task OldCodeCanInsertWithOriginalColumnTypes()
    {
        await _db.Database.MigrateAsync();

        // Old code expects Username to be a string (varchar)
        // If migration changed it to int or uuid, this fails at the DB layer.

        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""Users"" (""Id"", ""Username"", ""Email"", ""PasswordHash"", ""CreatedAt"")
              VALUES (@id, @username, @email, @passwordHash, @createdAt)",
            new[]
            {
                new SqlParameter("@id", Guid.NewGuid()),
                new SqlParameter("@username", "charlie"),  // String, as old code expects
                new SqlParameter("@email", "charlie@example.com"),
                new SqlParameter("@passwordHash", "hash..."),
                new SqlParameter("@createdAt", DateTime.UtcNow)
            });

        var inserted = await _db.Users.FirstOrDefaultAsync(u => u.Username == "charlie");
        Assert.NotNull(inserted);
    }
}

// DEPLOYMENT IMPLICATIONS
// =======================
// If a test above fails, the migration breaks backwards compatibility.
// Options:
//
// 1. FIX THE MIGRATION (preferred)
//    - Add DEFAULT to the new column
//    - Make it nullable
//    - Rename/remove in a separate migration after old code is retired
//
// 2. DOCUMENT DEPLOYMENT COORDINATION (if fix isn't possible)
//    - Add to deployment runbook: "All old instances must be rolled before this migration"
//    - This is riskier: requires careful orchestration, higher downtime risk
//
// 3. USE FEATURE FLAGS / STAGED ROLLOUT
//    - Deploy new code (with feature flag disabled) first
//    - Wait for all instances to be on new code
//    - Enable feature flag
//    - Run migration
//    - This requires pre-planning at implementation time
//
// WHEN TO APPLY THIS TEST
// =======================
// - When a migration adds a required column
// - When a migration removes/renames a column
// - When a migration changes a column type
// - When a migration changes a constraint (e.g., nullable → required)
// - After each new migration is written, before it's merged
//
// WHEN NOT TO APPLY THIS TEST
// ============================
// - Adding a nullable column (always backwards-compatible)
// - Relaxing a constraint (nullable existing column doesn't affect inserts)
// - Adding an index (doesn't affect DML operations)
