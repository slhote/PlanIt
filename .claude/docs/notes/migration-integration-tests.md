# Migration Integration Tests

## Overview

Data-driven integration tests ensure migrations actually work against a real PostgreSQL database. These tests apply migrations to a test database and verify the resulting schema is correct, constraints work, and data persists properly.

These tests complement:
- **Migration sequencing tests** — verify data survives through a sequence of migrations (e.g., no data loss)
- **Backwards-compatibility tests** — verify old code still works with new schema
- **Manual review** — catches hand-edits to migrations that don't match entity changes

## What These Tests Catch

✅ **Schema correctness** — migrations actually create valid tables  
✅ **Constraints work** — unique indexes, foreign keys, check constraints enforce as expected  
✅ **Data persistence** — data survives inserts/queries  
✅ **Cascading behavior** — delete cascades work as designed  
✅ **Concurrency token** — `xmin` column exists and works  
✅ **Defaults** — `CreatedAt`, `JoinedAt` defaults apply correctly  

## What These Tests Don't Catch

❌ **Data loss during schema evolution** — if migration 5 drops a column, tests on a fresh DB won't catch it. Use migration sequencing tests instead.  
❌ **Performance regressions** — belong in query performance tests, not migration tests.  
❌ **Backwards compatibility** — old code can't work with new schema. Use backwards-compatibility tests instead.  

## Test Setup

Use **Testcontainers** to spin up a real PostgreSQL container per test (or per test class):

```csharp
[Collection("Database")]
public class MigrationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainer _container;
    private PlanItDbContext _db;

    public async Task InitializeAsync()
    {
        _container = new PostgresBuilder()
            .WithImage("postgres:16")
            .Build();

        await _container.StartAsync();

        var options = new DbContextOptions<PlanItDbContext>(
            new Dictionary<string, object>
            {
                { "ConnectionString", _container.GetConnectionString() }
            });

        _db = new PlanItDbContext(options);
        await _db.Database.MigrateAsync(); // Apply all migrations
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _container.StopAsync();
    }

    // Tests go here
}
```

## Example Tests

### 1. Schema Correctness — Table and Columns Exist

```csharp
[Fact]
public async Task Migration_InitialCreate_CreatesUsersTable()
{
    // After MigrateAsync() in setup, verify table structure
    var userTable = await _db.Database.SqlQueryRaw<(string TableName, string ColumnName, string DataType)>(
        @"SELECT table_name, column_name, data_type 
          FROM information_schema.columns 
          WHERE table_name = 'Users'")
        .ToListAsync();

    Assert.NotEmpty(userTable);
    Assert.Contains(userTable, c => c.ColumnName == "Id" && c.DataType == "uuid");
    Assert.Contains(userTable, c => c.ColumnName == "Username" && c.DataType == "character varying");
    Assert.Contains(userTable, c => c.ColumnName == "PasswordHash" && c.DataType == "text");
}
```

### 2. Unique Constraints Work

```csharp
[Fact]
public async Task Migration_UniqueIndexOnUsername_PreventsDuplicates()
{
    var user1 = new User 
    { 
        Id = Guid.NewGuid(), 
        Username = "alice", 
        Email = "alice@example.com",
        PasswordHash = "hash1"
    };
    var user2 = new User 
    { 
        Id = Guid.NewGuid(), 
        Username = "alice", // Same username
        Email = "bob@example.com",
        PasswordHash = "hash2"
    };

    _db.Users.Add(user1);
    await _db.SaveChangesAsync();

    _db.Users.Add(user2);
    
    // Should throw DbUpdateException due to unique constraint violation
    await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
}
```

### 3. Foreign Key Constraints Work

```csharp
[Fact]
public async Task Migration_ForeignKeyConstraint_PreventsDanglingReferences()
{
    // Try to insert a ProjectMember that references a non-existent Project
    var projectMember = new ProjectMember
    {
        ProjectId = Guid.NewGuid(), // Project doesn't exist
        UserId = Guid.NewGuid(),
        Role = ProjectMemberRole.Member
    };

    _db.ProjectMembers.Add(projectMember);

    // Should throw DbUpdateException due to FK constraint violation
    await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
}
```

### 4. Cascade Delete Works

```csharp
[Fact]
public async Task Migration_CascadeDelete_DeletesChildWorkItems()
{
    // Create a Project with child WorkItems
    var userId = Guid.NewGuid();
    var projectId = Guid.NewGuid();

    var project = new Project 
    { 
        Id = projectId, 
        Name = "Test", 
        CreatedByUserId = userId 
    };
    
    var feature = new WorkItem 
    { 
        Id = Guid.NewGuid(), 
        ProjectId = projectId, 
        Title = "Feature",
        WorkItemType = WorkItemType.Feature
    };
    
    var task = new WorkItem 
    { 
        Id = Guid.NewGuid(), 
        ProjectId = projectId, 
        ParentId = feature.Id, // Child of the feature
        Title = "Task",
        WorkItemType = WorkItemType.Task
    };

    _db.Projects.Add(project);
    _db.WorkItems.AddRange(feature, task);
    await _db.SaveChangesAsync();

    // Delete the project
    _db.Projects.Remove(project);
    await _db.SaveChangesAsync();

    // Verify all work items are also deleted (cascade)
    var remaining = await _db.WorkItems.CountAsync();
    Assert.Equal(0, remaining);
}
```

### 5. Check Constraints Work

```csharp
[Fact]
public async Task Migration_TagsCardinalityConstraint_RejectsMoreThan3Tags()
{
    var workItem = new WorkItem
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Title = "Test",
        Tags = new List<string> { "tag1", "tag2", "tag3", "tag4" } // 4 tags, exceeds max of 3
    };

    _db.WorkItems.Add(workItem);

    // Should throw DbUpdateException due to CHECK constraint violation
    await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
}
```

### 6. Concurrency Token (xmin) Exists and Works

```csharp
[Fact]
public async Task Migration_XminConcurrencyToken_IsPresent()
{
    // Insert a work item
    var workItem = new WorkItem
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Title = "Original",
        WorkItemType = WorkItemType.Task
    };

    _db.WorkItems.Add(workItem);
    await _db.SaveChangesAsync();

    // Fetch it
    var fetched = await _db.WorkItems.FirstOrDefaultAsync();

    // Simulate another process updating it
    await _db.Database.ExecuteSqlRawAsync(
        @"UPDATE ""WorkItems"" SET ""Title"" = @title WHERE ""Id"" = @id",
        new[] 
        { 
            new SqlParameter("@title", "Updated by other process"),
            new SqlParameter("@id", fetched.Id)
        });

    // Try to update the stale entity (should have old xmin)
    fetched.Title = "Updated by us";
    
    // Should throw DbUpdateConcurrencyException due to xmin mismatch
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _db.SaveChangesAsync());
}
```

### 7. Defaults Apply Correctly

```csharp
[Fact]
public async Task Migration_DefaultValueSql_CreatedAtDefaults()
{
    var user = new User
    {
        Id = Guid.NewGuid(),
        Username = "alice",
        Email = "alice@example.com",
        PasswordHash = "hash"
        // CreatedAt is NOT set — should default to now() in the DB
    };

    _db.Users.Add(user);
    await _db.SaveChangesAsync();

    var inserted = await _db.Users.FirstOrDefaultAsync();
    
    // CreatedAt should be set to approximately now (within a few seconds)
    var now = DateTimeOffset.UtcNow;
    Assert.True(inserted.CreatedAt >= now.AddSeconds(-5) && inserted.CreatedAt <= now.AddSeconds(5));
}
```

### 8. Email Uniqueness (Case-Insensitive)

```csharp
[Fact]
public async Task Migration_UniqueIndexOnEmail_IsCaseInsensitive()
{
    var user1 = new User
    {
        Id = Guid.NewGuid(),
        Username = "alice",
        Email = "ALICE@EXAMPLE.COM", // Uppercase
        PasswordHash = "hash1"
    };

    _db.Users.Add(user1);
    await _db.SaveChangesAsync();

    // Try to insert with different casing
    var user2 = new User
    {
        Id = Guid.NewGuid(),
        Username = "bob",
        Email = "alice@example.com", // Lowercase — should conflict
        PasswordHash = "hash2"
    };

    _db.Users.Add(user2);

    // Should throw due to case-insensitive unique index
    // (Only if migration includes case-insensitive index on Email)
    await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
}
```

## Running These Tests

```bash
# Run all migration integration tests
dotnet test --filter Category=MigrationIntegration

# Run with verbose output to see container logs
dotnet test --filter Category=MigrationIntegration --verbosity detailed
```

## Integration with CI/CD

Add to your CI pipeline:

```yaml
- name: Run Migration Integration Tests
  run: |
    dotnet test \
      --filter Category=MigrationIntegration \
      --logger "junit;LogFilePath=test-results.xml" \
      --collect:"XPlat Code Coverage" \
      --no-restore
  continue-on-error: false  # Fail the build if any test fails
```

## Best Practices

1. **One container per test class** (via `IAsyncLifetime`) — isolation is important
2. **Use meaningful assertion messages** — say *why* the assertion matters
3. **Test the boundary cases** — exact max lengths, edge case constraint values
4. **Include both positive and negative cases** — "insert succeeds" and "duplicate insert fails"
5. **Comment why the test matters** — link to the migration or the architectural decision it validates
6. **Keep tests fast** — Testcontainers startup can be slow; consider a shared container per test run in later CI optimization if tests become a bottleneck

## When to Add New Tests

- After each new migration is written
- When a schema constraint is especially critical (e.g., cascade delete, unique constraints)
- When a migration involves complex data transforms (use migration sequencing tests for this)
- When onboarding new maintainers (tests serve as documentation of what the schema actually does)
