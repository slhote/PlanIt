# CLAUDE.md

This file provides guidance to Claude Code when working with code in `PlanIt.Api`. See the [repo-root CLAUDE.md](../CLAUDE.md) for shared commands and cross-project context. The architecture decisions referenced throughout this file come from four subplans under `.claude/docs/plans/`, in order of authority (later ones supersede earlier ones where they overlap — e.g. the master plan's DB engine placeholder was superseded by System Design's actual choice):

1. [`planit-master-plan.md`](../.claude/docs/plans/planit-master-plan.md) — top-level binding decisions (idempotency, auth model shape, cascade-delete rule, hierarchy/routing shape). **Its "deferred" DB-engine line is stale** — see #2.
2. [`planit-system-design-architecture.md`](../.claude/docs/plans/planit-system-design-architecture.md) — DB engine (confirmed: PostgreSQL), SignalR hub design, CORS/auth-handshake, error handling, secrets/JWT signing, observability, versioning, layering.
3. [`planit-persistence-data-model.md`](../.claude/docs/plans/planit-persistence-data-model.md) — the actual EF Core entity schema (`User`, `Project`, `ProjectMember`, `WorkItem`, `RefreshToken`), constraints, migration strategy — the *what*.
4. [`planit-persistence-wiring.md`](../.claude/docs/plans/planit-persistence-wiring.md) — the *how*: repository pattern (one interface per aggregate root, not generic), project structure (`Domain/` vs `Data/`), `DbContext` scoped lifetime (no Unit-of-Work wrapper needed), and the testing split (Moq against repository interfaces for service logic, Testcontainers-backed real Postgres for repository implementations — never mock `DbContext` directly).

**Subplan "API Contracts & Backend" (endpoint URLs, exact DTO shapes/casing, HTTP verbs per route) has not been written yet.** Don't invent specific endpoint contracts as if they were decided; the subplans above cover architecture, schema, and persistence wiring, not the wire format. The persistence wiring in #4 doesn't need to wait for it, though — the schema has nothing left open, so that layer can be built and tested standalone.

## Current State: unmodified `dotnet new webapi` scaffold

**As of this writing, `PlanIt.Api` has no real API surface.** It is the stock ASP.NET Core Web API template, unchanged apart from what `dotnet new` generated. There is no domain model, no persistence, no auth, no SignalR hub, and no CORS policy. Every .cs file:

- **`Program.cs`** — minimal hosting model: `AddControllers()`, `AddOpenApi()`, `MapOpenApi()` gated on `IsDevelopment()`, `UseHttpsRedirection()`, `UseAuthorization()` (no `UseAuthentication()` — there's nothing to authenticate yet), `MapControllers()`. **No CORS is registered** (`AddCors`/`UseCors` don't exist) — this must be added before `PlanIt.Web` (a different origin) can call it for real.
- **`Controllers/WeatherForecastController.cs`** — the stock scaffold controller (`[Route("[controller]")]`, one `GET` returning 5 random `WeatherForecast` records). Delete this once real controllers exist; don't build alongside it.
- **`WeatherForecast.cs`** — the stock record (`Date`, `TemperatureC`, computed `TemperatureF`, `Summary`).

No `Hubs/`, `Contracts/`, `Application/`, `Domain/`, `Services/`, or `Data/` folders exist. No SignalR package, no auth packages, no EF Core / `DbContext`.

**`PlanIt.Api.csproj`**: `net10.0`, `Nullable` + `ImplicitUsings` enabled. One package reference: `Microsoft.AspNetCore.OpenApi 10.0.10` (the known `NU1903` transitive warning on this package is tracked in the repo-root `CLAUDE.md` — don't try to silently suppress it).

**Config**: `appsettings.json`/`appsettings.Development.json` hold only default logging levels and `AllowedHosts: *` — no connection strings, no JWT settings, no CORS origins list yet. `launchSettings.json` has `http` (`http://localhost:5223`) and `https` (`https://localhost:7263;http://localhost:5223`) profiles, both `ASPNETCORE_ENVIRONMENT=Development`, `launchBrowser: false`. `PlanIt.Web`'s dev server runs on `http://localhost:5173` — once CORS is added, that's the origin to allow.

**`PlanIt.Api.Tests`**: references `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`; no Moq yet (deliberately deferred — see below). The only test file, `UnitTest1.cs`, has a single `[Fact]` with an **empty body** — it verifies nothing. Don't treat its existence as coverage of anything.

## What's actually planned (not yet built)

Everything below is **design intent from the three subplans above**, not current code — don't reference nonexistent types/files/folders as if they're already there. But treat these as genuinely *decided*, not open questions to re-litigate; where something really is still undecided, it's explicitly labeled "not yet decided" below.

### Database & persistence — decided

- **Engine: PostgreSQL** (Azure Database for PostgreSQL Flexible Server in prod; same engine/major version in local Docker Compose for dev). This was an explicit, reasoned choice in System Design (`xmin`-native optimistic concurrency, `pgvector` for the future Similar Tasks Suggestions feature vs. a bolted-on vector store elsewhere) — **not** deferred, despite the master plan's top-level doc still saying "deferred" (that line is stale relative to the later subplan).
- **Migrations**: EF Core Migrations, checked into `PlanIt.Api/Migrations/`, named for the change they make (not `Migration1`-style).
- **Concurrency**: optimistic, via Postgres's native `xmin` system column mapped as the EF Core concurrency token (`.UseXminAsConcurrencyToken()`) — no hand-maintained `RowVersion` column needed. A stale write throws `ConcurrencyConflictException` → 409. A dedicated optimistic-vs-pessimistic comparison/learning session is deliberately deferred to after the planning phase; that doesn't block implementing optimistic concurrency now.
- **Schema** (`planit-persistence-data-model.md` has full column-level detail): `User` (Id/Username/Email/PasswordHash), `Project` (Id/Name/Description/CreatedByUserId — provenance only, not the access-control source), `ProjectMember` (composite PK `ProjectId`+`UserId`, `Role` Owner=0/Member=1 — **the single source of truth for project access**; the creator gets an explicit `Owner` row here, inserted in the same transaction as the `Project` row), `WorkItem` (single-table EF Core TPH with `WorkItemType` discriminator Feature=0/Task=1, nullable `ParentId` self-FK, `Status` ToDo=0/InProgress=1/Completed=2, nullable `AssigneeId`, `Tags: text[]` with `CHECK (cardinality(tags) <= 3)`), `RefreshToken` (Id/UserId/TokenHash/ExpiresAt/RevokedAt/ReplacedByTokenId — **rotation with reuse detection**: presenting an already-revoked token revokes every active token for that user).
- **Tags**: decided as a native Postgres `text[]` column directly on `WorkItem`, not a junction table — no global Tag entity, per-project scoped (matching text in different projects is unrelated), max 3, case-insensitive matching via lowercase-at-write. The frontend mock's `MAX_TAGS_PER_WORK_ITEM = 3` already matches this.
- **Cascade delete**: deleting a Feature deletes its child Tasks — enforced both at the DB level (`ParentId` FK is `ON DELETE CASCADE`, defense-in-depth) and the service layer (which also supplies the "this will delete N tasks" count the frontend confirms before committing — already implemented on the mock side via `countCascadeDeletions` in `PlanIt.Web/src/api/workItems.ts`, as the shape the real endpoint should match).
- **Completion cascade** (distinct from delete cascade): marking a Feature complete cascades to mark its Tasks complete too; the reverse never cascades (un-completing a parent doesn't un-complete children that were already done). This logic lives in the service layer, not the DB, and both delete/completion cascade logic should be written as an extensible per-child-type strategy.
- **Domain invariant enforced at the service layer, not DB constraints**: a Feature can't have a `ParentId`; a Task's `ParentId`, if set, must point to a Feature, not another Task.
- **Assignment**: single nullable `AssigneeId` per work item, no join table.
- **Idempotency**: creates use **client-generated GUIDs** as the PK with server-side upsert, not a separate `Idempotency-Key` header scheme (explicitly out of scope for baseline). `PUT`/`PATCH`/`DELETE` just need standard REST idempotent semantics.

### Real-time (SignalR) — decided, not yet built

- **Per-project groups**, not global broadcast — group membership verified server-side at join time. `OnConnectedAsync` must re-verify and re-join groups on *every* connect (not just the first), since `connectionId`/group membership resets on reconnect — a client that skips this silently stops receiving updates after a network blip.
- **REST writes, SignalR broadcasts only** — all mutations go through REST; the service layer publishes a domain event after a successful write, a notifier subscriber pushes it to the hub group (excluding the originating client via `GroupExcept`). Never a second write path through the hub.
- **MVP event set (7 events)**: structural (`WorkItemCreated`, `WorkItemDeleted`, `WorkItemStatusChanged`, `WorkItemMoved` — full payload, board updates immediately) vs. content-only (`WorkItemUpdated` — lightweight, just invalidates cache) vs. membership (`ProjectMemberAdded`, `ProjectMemberRemoved`).
- **Handshake**: `POST /hub/negotiate` (unauthenticated) → client opens `GET /hub?access_token={token}` (query-string token, standard for WebSocket upgrades which can't set custom headers) → server validates before accepting.
- **No missed-event replay** — a reconnecting client just re-fetches (periodic background refetch / on-refocus); optimistic concurrency prevents corruption from a stale write, but the API doesn't replay broadcasts that fired while a client was offline. Accepted eventual-consistency tradeoff, not an oversight.
- **No Azure SignalR Service backplane** — single-instance in-process hub is fine at this scale; only becomes a deployment-layer change later if scale ever justifies it.

### Auth — decided

- JWT-only, no cookies (sidesteps GitHub Pages ↔ Azure cross-origin cookie complexity — don't introduce cookie-based auth as an alternative without discussing it first, it was explicitly considered and rejected). HS256 signing (single service both mints and verifies, so no need for RS256's asymmetric split).
- ~15-minute access token, `Authorization: Bearer`. Refresh token in the response body + client `localStorage`, **rotation with reuse detection** (see schema above) — presenting an already-rotated-away token revokes every active session for that user, not just a silent no-op.
- **Token refresh is proactive/timer-based on the client** (fires at ~80% of TTL), not reactive to a 401 or a SignalR reconnect — needed because a user can sit idle on SignalR-only updates with zero outgoing REST calls, which would never trigger a reactive refresh.
- No dual-key rotation grace window — changing the signing key invalidates all sessions immediately (accepted tradeoff for a portfolio-project threat model; a compromised-key scenario wants the hard cutover anyway).
- Secrets: .NET User Secrets locally, environment variables when deployed (`Jwt__SigningKey`, `ConnectionStrings__DefaultConnection`); `appsettings.json` holds structure only, never real values.

### API-surface conventions — decided

- **404, not 403** for unauthorized project access (don't leak project existence to non-members).
- **No API versioning** for MVP — first-party frontend/backend pair, no independent consumer to protect a contract for.
- **Global exception handling via `IExceptionHandler`** classes (ASP.NET Core 10 pattern) mapping typed domain exceptions to RFC 7807 `ProblemDetails` — e.g. `TaskNotFoundException` → 404, `ConcurrencyConflictException` → 409, business-rule `ValidationException` → 400. Model-binding validation errors are automatic via `[ApiController]`.
- **CORS**: allow `https://slhote.github.io` (the GitHub Pages frontend origin — note this differs from the local dev origin `http://localhost:5173`, so dev config needs its own allowed-origins entry), credentialed requests allowed (required for the SignalR handshake), methods GET/POST/PATCH/DELETE (no PUT — PATCH is the idempotent mutation verb here), headers include `Authorization`.
- **Strict layering, no skipping**: Client → API layer (controllers/DTOs/auth) → Service layer (domain logic/transactions) → Data Access (EF Core) → DB. A controller must never call data access directly; a service must never take an HTTP dependency.
- **Observability**: skip Application Insights for MVP, just `ILogger<T>` structured logging; leave commented placeholder config so App Insights is a drop-in later. Single `GET /health` endpoint for deployment health probes.
- **Input validation**: trimmed/validated at the API boundary (null/empty/length checks) — the backend never trusts the frontend's client-side validation as a security boundary.

### Similar Tasks Suggestions (post-MVP) — groundwork decided, feature itself not built

MVP similarity = lexical + metadata only (tag overlap, same-project, same-assignee, keyword overlap in title/description) via an `ISimilaritySignal` interface with a `WeightedSimilarityScorer`; no behavioral/collaborative signals, no precomputation (on-demand per request), same-project-only candidate scope, no pre-built `pgvector` column yet (the interface seam is what protects the future embeddings swap, not pre-built infra). Single endpoint `GET /projects/{projectId}/workitems/{workItemId}/similar-tasks`. See `similar-tasks-feature-planning.md` for full detail — this is sequenced last (subplan 8), after Tags/Labels and seed data exist.

### Testing — decided

Moq is intended for narrow, leaf-level collaborator mocking once real domain code exists (confirmed by the user) — do not add it preemptively before there's something worth mocking, and don't assume the mockable seams look like another project's (e.g. TrashAnimal's `Mock<Die>`/`Mock<IDrawPile>` pattern) without confirming they fit this codebase's actual domain design once it exists.

### Genuinely still open (per the master plan's own list)

- Whether a completed Feature/Task's status change should auto-cascade in cases beyond the one already decided above — the *completion* cascade direction is settled; anything else status-related not covered above is not.
- Exact REST endpoint URLs/verbs/DTO field casing — no API Contracts subplan has been written yet; don't invent one and present it as decided.

When implementing any of the above, build it against these subplans' decisions rather than improvising a different shape — if a decision needed isn't covered here or in the subplans, stop and ask rather than assuming.

## Common Commands

```bash
# Run the API
dotnet run --project PlanIt.Api

# Run tests
dotnet test

# Build
dotnet build
```
