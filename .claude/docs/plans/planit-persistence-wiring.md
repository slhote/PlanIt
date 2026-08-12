# Persistence Wiring Subplan

**Date:** 2026-08-12
**Status:** Design Decision (Phase 1 — unblocked; does not depend on subplan 3 existing)
**Scope:** Repository pattern, project structure, `DbContext` lifetime, and testing strategy for wiring up EF Core in `PlanIt.Api` — the "how do we implement it" companion to [`planit-persistence-data-model.md`](planit-persistence-data-model.md)'s "what the schema is."

## Context

[`planit-persistence-data-model.md`](planit-persistence-data-model.md) fully specifies the schema (entities, columns, constraints, `xmin` concurrency, cascade rules) but explicitly defers "wiring up the actual `DbContext`, migrations, and DI registration" to subplan 3 (API Contracts & Backend), which hasn't been written yet. That deferral is organizational, not a real decision gate — the schema has nothing left open, so the wiring itself can proceed without waiting on subplan 3's endpoint/DTO work. This doc captures that wiring design so it doesn't get invented ad hoc mid-implementation, consistent with this repo's standing practice of writing decisions down before building against them.

Reconciles with the layering diagram already confirmed in [`planit-system-design-architecture.md`](planit-system-design-architecture.md) §11:

```
API layer (controllers, DTOs, auth)
  ↕ [service interfaces]
Service layer (domain logic, transactions)
  ↕ [repository/data-access interfaces]
Data Access layer (EF Core)
```

---

## Decisions

### 1. Repository pattern: one interface per aggregate root, not generic

**Decision:** `Domain/Repositories/` holds one repository interface per aggregate root from the schema — `IProjectRepository`, `IProjectMemberRepository`, `IWorkItemRepository`, `IUserRepository`, `IRefreshTokenRepository` — each with intention-revealing, domain-specific methods (e.g. `IWorkItemRepository.GetChildTasksAsync(featureId)`), not a generic `IRepository<T>`.

**Why not generic:** a generic repository either leaks EF Core's `IQueryable` past the abstraction (defeating the point of the boundary) or forces an awkward specification-object pattern to stay leak-proof. It also produces CRUD-shaped method names (`GetAllAsync`, `FindAsync`) that don't reveal intent, conflicting with this repo's standing "intention-revealing names" convention.

### 2. Project structure: entities are persistence-ignorant

```
PlanIt.Api/
  Domain/
    Entities/        — User, Project, ProjectMember, WorkItem, RefreshToken (plain POCOs, no EF attributes)
    Repositories/     — I*Repository interfaces
    Exceptions/        — TaskNotFoundException, ConcurrencyConflictException, ValidationException
  Data/
    PlanItDbContext.cs
    Configurations/    — IEntityTypeConfiguration<T> per entity, matching the persistence doc's
                          column/constraint spec exactly, including .UseXminAsConcurrencyToken()
    Repositories/       — EF Core-backed implementations of the Domain/Repositories/ interfaces
  Migrations/            — EF Core Migrations output, checked in
```

Entities carry no EF Core attributes or dependencies — mapping lives entirely in `Data/Configurations/`. This keeps the service layer (which depends only on `Domain/`) transport- and ORM-agnostic, matching the existing "service layer stays transport-agnostic" principle already applied to the SignalR design.

### 3. `DbContext` lifetime: scoped, no Unit-of-Work wrapper

**Decision:** `PlanItDbContext` is registered scoped (ASP.NET Core's default for `AddDbContext<T>()` — one instance per HTTP request). No separate `IUnitOfWork` abstraction is introduced.

**Why this is sufficient:** every repository implementation constructor-injects the same scoped `DbContext` instance within a request. `DbContext`'s change tracker already accumulates every `Add`/`Update`/`Remove` call made through any repository sharing that instance, and a single `SaveChangesAsync()` call commits all of them together in one database transaction — this is exactly what a hand-rolled Unit of Work would provide, for free, via DI scoping. Example: creating a `Project` must also insert the creator's `ProjectMember` (`Role = Owner`) row in the same transaction (per the persistence doc's explicit callout) — this works by both `ProjectRepository.Add()` and `ProjectMemberRepository.Add()` staging changes on the same shared context, with the service layer calling `SaveChangesAsync()` once.

**Rejected alternatives:**
- **Singleton `DbContext`** — not thread-safe; would require manual locking under concurrent requests, serializing all DB access and also risking change-tracker corruption. A genuine correctness and throughput hazard, not just unnecessary.
- **Transient `DbContext`** (new instance per repository) — would give each repository in a request a *different* context, breaking the free atomic-commit property above and requiring an explicit coordinator to reintroduce it.

**Performance note:** scoped-per-request doesn't imply unbounded resource growth as traffic grows — `DbContext` construction is cheap (no connection opens at construction time), and the actual expensive resource (physical Postgres connections) is pooled separately by Npgsql underneath, bounded independently of request/context count.

### 4. Concurrency exception translation happens at the repository boundary

A `DbUpdateConcurrencyException` (a stale `xmin` write) is caught inside the repository implementation and rethrown as the domain's `ConcurrencyConflictException` (`Domain/Exceptions/`). This keeps the service layer — and the `IExceptionHandler` → 409 mapping already decided in System Design §6 — free of any EF Core-specific exception type.

### 5. Testing strategy: mock the repository interfaces, never `DbContext`

**Decision:** `DbContext` is never mocked directly. Testing splits at the same repository-interface boundary as the architecture itself:

- **Service-layer unit tests mock the repository interfaces with Moq.** `IProjectRepository`/`IWorkItemRepository`/etc. are plain interfaces with a handful of methods — trivial and fast to mock. These tests verify the service's *decision logic* (e.g. "does creating a project add an Owner `ProjectMember` row?"), with zero database dependency.

  ```csharp
  [Fact]
  public async Task CreateProjectAsync_AddsCreatorAsOwnerMember()
  {
      var projectRepo = new Mock<IProjectRepository>();
      var memberRepo = new Mock<IProjectMemberRepository>();

      var service = new ProjectService(projectRepo.Object, memberRepo.Object, /* ... */);
      await service.CreateProjectAsync("My Project", creatorId);

      memberRepo.Verify(m => m.Add(It.Is<ProjectMember>(pm =>
          pm.UserId == creatorId && pm.Role == ProjectMemberRole.Owner)), Times.Once);
  }
  ```

- **Repository implementations are integration-tested against a real Postgres, not mocked.** Mocking `DbContext` itself is both impractical (Moq can fake virtual members, but `DbSet<T>` doesn't support LINQ query mocking without extra tooling) and untrustworthy (a mock's query behavior diverges from what the real provider translates to SQL). What repository implementations exist to do — verify EF Core mapping correctness, confirm `xmin` conflicts actually throw, confirm `CHECK (cardinality(tags) <= 3)` actually rejects a 4th tag, confirm cascade deletes actually cascade — is Postgres-specific behavior that can only be verified against real Postgres.

- **Real Postgres via Testcontainers, not EF Core's `InMemory` provider.** `InMemory` is a known trap for this purpose: it doesn't enforce relational constraints or translate real SQL, so it would pass tests that Postgres itself would reject (the exact behaviors — `xmin`, `CHECK` constraints, cascades — that repository tests need to verify). Testcontainers spinning up a real Postgres container per test run is a natural fit here since the project's already committed to Docker for local dev Postgres.

**Consistency check:** this matches the master plan's already-stated Moq philosophy ("narrow, leaf-level collaborator mocking") — the repository interfaces are exactly that leaf. No new testing philosophy is being introduced here, only its concrete application to the persistence layer.

### 6. Migration testing strategy

**Decision:** Two types of migration tests complement the repository integration tests above. A third type (backwards-compatibility tests) is documented as a reference for production codebases but not implemented for PlanIt.

**PlanIt's test suite:**

1. **Migration integration tests** — apply all migrations to a fresh Postgres container via Testcontainers, then verify:
   - Table/column structure is correct (schema matches entity config)
   - Constraints enforce (unique, FK, check constraints work)
   - Defaults apply (CreatedAt, JoinedAt, etc.)
   - Cascading behavior works (FK cascades delete as expected)
   - Concurrency token (xmin column) exists and functions
   - Data persists correctly through insert/query cycles

   These tests catch schema correctness bugs and constraint-enforcement problems with the first-time schema creation.

2. **Migration sequencing tests** — verify data survives through a sequence of migrations:
   - Apply migrations up to point N
   - Insert representative test data
   - Apply migration N+1 (and subsequent migrations)
   - Verify data is still present, unchanged (except where explicitly transformed)
   - Verify queries still work post-migration

   These tests catch data loss or data corruption during schema evolution (e.g., if a migration accidentally drops a column or corrupts existing rows).

Both are integration tests and run against real Postgres via Testcontainers, never mocked. See [`migration-integration-tests.md`](../notes/migration-integration-tests.md) for detailed examples and patterns.

**Reference for production codebases:**

A third type of test, **backwards-compatibility tests**, is valuable for mature production systems where rolling deployments overlap old and new code versions. These tests verify that old code can still function with a new database schema (or explicitly document why downtime/deployment coordination is required). See [`migration-backwards-compatibility-testing.cs`](../notes/migration-backwards-compatibility-testing.cs) and [`production-migration-best-practices.md`](../notes/production-migration-best-practices.md) (§ "Backwards-Compatibility During Rollout") for how this would work and the benefits it provides — documented as a reference pattern, not part of PlanIt's implementation.

---

## Relationship to other subplans

- **`planit-persistence-data-model.md`** — owns the schema (what). This doc owns the wiring (how). Read both before implementing.
- **Subplan 3 (API Contracts & Backend, not yet written)** — still owns controllers, DTOs, and the exact REST endpoint surface. This doc's repository/service layers are designed to be consumed by that work once it exists, but don't require it to exist first — the persistence layer can be built and tested (via the strategy in §5) standalone.
- **Subplan 6 (Testing, not yet written)** — owns the broader test strategy (integration tests, seed-data generator, overall Moq scope). This doc's §5 anticipates and is consistent with that subplan's stated Moq intent, but doesn't preempt decisions Testing hasn't made yet (e.g. whether Testcontainers vs. a shared docker-compose test DB is the final call for CI).

## Verification

1. `dotnet ef migrations add InitialCreate` (once entities/configurations are written) produces a migration matching the persistence doc's schema exactly.
2. `dotnet ef database update` against local Docker Postgres succeeds.
3. Service-layer unit tests (mocked repositories) run with no database dependency and no network I/O.
4. Repository integration tests (Testcontainers-backed real Postgres) confirm: `xmin` concurrency conflict throws `ConcurrencyConflictException` on a stale write; a 4th tag insert is rejected by the DB `CHECK` constraint; deleting a Feature cascades to its child Tasks.
5. `dotnet test` runs both suites; unit tests should be near-instant, integration tests slower (container startup) but still fast enough for routine local runs.
