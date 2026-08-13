# CLAUDE.md

This file provides guidance to Claude Code when working with code in `PlanIt.Api`. See the [repo-root CLAUDE.md](../CLAUDE.md) for shared commands and cross-project context. The architecture decisions referenced throughout this file come from four subplans under `.claude/docs/plans/`, in order of authority (later ones supersede earlier ones where they overlap — e.g. the master plan's DB engine placeholder was superseded by System Design's actual choice):

1. [`planit-master-plan.md`](../.claude/docs/plans/planit-master-plan.md) — top-level binding decisions (idempotency, auth model shape, cascade-delete rule, hierarchy/routing shape). **Its "deferred" DB-engine line is stale** — see #2.
2. [`planit-system-design-architecture.md`](../.claude/docs/plans/planit-system-design-architecture.md) — DB engine (confirmed: PostgreSQL), SignalR hub design, CORS/auth-handshake, error handling, secrets/JWT signing, observability, versioning, layering.
3. [`planit-persistence-data-model.md`](../.claude/docs/plans/planit-persistence-data-model.md) — the actual EF Core entity schema (`User`, `Project`, `ProjectMember`, `WorkItem`, `RefreshToken`), constraints, migration strategy — the *what*.
4. [`planit-persistence-wiring.md`](../.claude/docs/plans/planit-persistence-wiring.md) — the *how*: repository pattern (one interface per aggregate root, not generic), project structure (`Domain/` vs `Data/`), `DbContext` scoped lifetime (no Unit-of-Work wrapper needed), and the testing split (Moq against repository interfaces for service logic, Testcontainers-backed real Postgres for repository implementations — never mock `DbContext` directly).
5. [`planit-api-contracts-backend.md`](../.claude/docs/plans/planit-api-contracts-backend.md) — endpoint URLs, DTO shapes, service layer, auth (password hashing, JWT minting, refresh rotation), SignalR hub design (MediatR-based event dispatch), the `WorkItem.Order` column resolving the reorder/position gap, and policy-based access control (`[Authorize(Policy = "ProjectMember")]` + a custom 403→404 rewrite). **All implementation from this doc has landed** — see current state section below for the built state.

## Current State: fully implemented API

**As of the most recent commits, `PlanIt.Api` is a fully built API** — the `dotnet new webapi` scaffold is long gone. The following are all present and integrated against a real PostgreSQL database via EF Core.

### Project structure

```
PlanIt.Api/
  Controllers/       — AuthController, ProjectsController, ProjectMembersController,
                        UsersController, WorkItemsController
  Application/       — AuthService, ProjectService, WorkItemService, UserService,
                        ProjectMemberService, WorkItemMapper
  Application/Auth/  — JwtTokenService, ProjectMemberAuthorizationHandler,
                        ProjectMember404ResultHandler, ICurrentUserAccessor,
                        ClaimsCurrentUserAccessor
  Application/Realtime/ — 7 MediatR notification + handler pairs (see SignalR section)
  Contracts/         — request/response DTOs for auth, projects, project members,
                        users, work items
  Domain/Entities/   — User, Project, ProjectMember, RefreshToken, WorkItem + enums
  Domain/Repositories/ — repository interfaces (one per aggregate root)
  Domain/Exceptions/ — typed domain exceptions (TaskNotFoundException, etc.)
  Data/              — PlanItDbContext, entity Configurations/, repository implementations,
                        two migrations (InitialCreate, AddWorkItemOrder)
  ExceptionHandling/ — 5 IExceptionHandler classes → RFC 7807 ProblemDetails
  HealthChecks/      — DatabaseHealthCheck (GET /health for Azure probes)
  Hubs/              — PlanItHub (SignalR)
  Startup/Options/   — CorsOptions, JwtOptions (bound/validated at startup)
  Startup/Validation/ — CorsOptionsValidator, JwtOptionsValidator
  Program.cs         — full wiring (212 lines)
```

### Package references

`net10.0`, `Nullable` + `ImplicitUsings` enabled. Packages: `MediatR 14.2.0`, `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.10`, `Microsoft.AspNetCore.OpenApi 10.0.10` (known `NU1903` transitive warning — see root `CLAUDE.md`), `Microsoft.EntityFrameworkCore` + `Design` + `Relational` 10.0.11, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`. SignalR is in the shared framework — no extra package.

### Config

- `appsettings.json` — `ConnectionStrings:DefaultConnection` placeholder `"[replaced by env var in deployed]"`, `Cors:AllowedOrigins: ["https://slhote.github.io"]`, `Jwt` section (Issuer/Audience/ExpirationMinutes: 15/RefreshTokenExpirationDays: 7/SigningKey: "")
- `appsettings.Development.json` — overrides `Cors:AllowedOrigins: ["http://localhost:5173"]`
- Secrets: `.NET User Secrets` locally (`dev.ps1` generates `Jwt__SigningKey` on first run), environment variables when deployed

### `PlanIt.Api.Tests`

`Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`, and a `ProjectReference` to `PlanIt.Api` are wired up. No Moq yet (deliberately deferred to the Testing subplan). The only test file, `UnitTest1.cs`, has a single `[Fact]` with an **empty body** — it verifies nothing. Don't treat its existence as coverage of anything.

## What's built

Everything below is **design intent from the three subplans above**, not current code — don't reference nonexistent types/files/folders as if they're already there. But treat these as genuinely *decided*, not open questions to re-litigate; where something really is still undecided, it's explicitly labeled "not yet decided" below.

### Database & persistence — built

- **Engine: PostgreSQL**, EF Core + Npgsql. Two migrations checked in: `InitialCreate`, `AddWorkItemOrder`.
- **Concurrency**: optimistic, via Postgres `xmin` system column (`.UseXminAsConcurrencyToken()`). Stale write → `ConcurrencyConflictException` → 409. A deliberate optimistic-vs-pessimistic comparison/learning session is deferred (see subplan 7) — optimistic is the implementation baseline.
- **Schema** (full column detail in `planit-persistence-data-model.md`): `User`, `Project`, `ProjectMember` (composite PK, `Role` Owner/Member — single source of project access truth), `WorkItem` (single-table EF Core TPH, `WorkItemType` discriminator, nullable `ParentId`/`AssigneeId`, `Tags text[]`, `Order` column), `RefreshToken` (with rotation/reuse detection).
- **Tags**: native Postgres `text[]` column on `WorkItem`, max 3, lowercase-at-write, no junction table.
- **Cascade delete**: `ParentId` FK `ON DELETE CASCADE` at DB level + service layer supplies count for frontend confirmation. Delete returns `{ deletedIds }` payload.
- **Completion cascade**: marking a Feature complete cascades to its Tasks; reversal never cascades up. Service layer, not DB.

### Real-time (SignalR) — built

- `Hubs/PlanItHub.cs` — per-project groups, group membership re-verified on every connect.
- `Application/Realtime/` — 7 MediatR notification + handler pairs: `WorkItemCreated`, `WorkItemDeleted`, `WorkItemStatusChanged`, `WorkItemMoved`, `WorkItemUpdated`, `ProjectMemberAdded`, `ProjectMemberRemoved`. Service layer publishes; hub handler broadcasts via `GroupExcept` (excludes originating client via `X-SignalR-Connection-Id` request header).
- JWT delivered via query-string `access_token` on `/hub` path (WebSocket can't set `Authorization` header). Scoped only to the `/hub` path — REST calls still use the `Bearer` header.
- No Azure SignalR Service backplane — single in-process hub, fine at this scale.

### Auth — built

- JWT-only (HS256). `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`. 15-minute access token in memory; 7-day refresh token with rotation and reuse detection (presenting an already-rotated token revokes every active session for that user).
- Policy-based `[Authorize(Policy = "ProjectMember")]` access control on all project-scoped routes, with a `ProjectMember404ResultHandler` that rewrites 403 → 404 (don't leak project existence).

### API-surface conventions — built

- **404, not 403** for non-member project access.
- **Global exception handling** via 5 `IExceptionHandler` classes → RFC 7807 `ProblemDetails`.
- **CORS**: `Cors:AllowedOrigins` bound from config. Production → `https://slhote.github.io`, Development → `http://localhost:5173`. Credentialed, GET/POST/PATCH/DELETE, `AllowAnyHeader`.
- **Strict layering**: Controller → Service → Repository → EF Core → DB. No skipping.
- `GET /health` endpoint backed by `DatabaseHealthCheck`.

### Similar Tasks Suggestions (post-MVP) — route stub only

Route stub `GET /projects/{projectId}/workitems/{workItemId}/similar-tasks` exists in `WorkItemsController` but returns a placeholder — no similarity logic built yet. Full implementation design is in `similar-tasks-feature-planning.md`; sequenced after Testing (subplan 6) and seed data.

### Testing — not started

`PlanIt.Api.Tests` has no Moq package reference and no real tests — deliberately deferred to the Testing subplan (master plan subplan 6). Moq is intended for narrow, leaf-level collaborator mocking (e.g. against the repository interfaces in `Domain/Repositories/`). Testcontainers-backed real Postgres is planned for repository-implementation tests. Don't add Moq preemptively; the Testing subplan decides the seams.

### Genuinely still open

- Concurrency Learning session (subplan 7) — deliberate deferral; optimistic concurrency is implemented as the MVP baseline.
- Testing subplan (subplan 6) — not started.
- Similar Tasks Suggestions implementation (subplan 8) — route stub only.
- DevOps/deploy to production — Dockerfile exists, CD does not; see `planit-devops-hosting.md`.

When working in this codebase, build against the subplan decisions above rather than improvising a different shape. If a needed decision isn't covered, stop and ask rather than assuming.

## Common Commands

```bash
# Run the API
dotnet run --project PlanIt.Api

# Run tests
dotnet test

# Build
dotnet build
```
