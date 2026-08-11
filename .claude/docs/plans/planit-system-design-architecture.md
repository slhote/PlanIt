# System Design & Architecture Subplan

**Date:** 2026-08-11  
**Status:** Design Decision (Phase 0 — foundational)  
**Scope:** Component topology, database engine, real-time collaboration architecture, Similar Tasks Suggestions groundwork, cross-cutting concerns

## Context

This subplan finalizes the open decisions flagged in the master plan as gating Phase 1 implementation. It covers:
- Deployed components and hosting topology
- Database engine choice (PostgreSQL, Azure SQL, Cosmos DB)
- Real-time collaboration architecture (SignalR hub design, events, caching)
- Authentication/authorization flow, especially the SignalR handshake and token-refresh semantics
- Similar Tasks Suggestions groundwork (definition of "similar," data audit, compute strategy)
- Error handling, secrets management, observability, and other cross-cutting concerns

**Key principle:** All decisions are written with explicit "why deferred" rationales for any open items, so later subplans don't guess.

**Related document:** [`data-loading-architecture.md`](data-loading-architecture.md) covers the frontend-facing lazy-load/cache/invalidation strategy in more detail and was merged separately; this subplan reconciles its SignalR event examples into the finalized 7-event set in section 3 below, and confirms the per-project group design it already assumed.

---

## Decisions

### 1. Component topology & hosting

**Deployed shape:**
- **API service** (ASP.NET Core 10 + SignalR hub, single container)
- **PostgreSQL database** (Azure Database for PostgreSQL Flexible Server)
- **Frontend** (React 19, static files on GitHub Pages)
- **Local dev** (Docker Compose: `PlanIt.Api` container + Postgres container)

**Scale-out strategy:** Single API instance is acceptable for a portfolio project. **No Azure SignalR Service backplane needed** — in-memory SignalR with per-project groups is fine indefinitely at this scale. If/when scale justifies a backplane, it's a deployment-layer change (migrate to Azure SignalR Service), not an architecture rewrite.

**Why not split the hub:** The API and SignalR hub live in the same process. A separate scale unit would require out-of-process communication (Redis backplane, message queue) to fan out updates — architectural overhead that doesn't exist if the hub stays in-process. Single-instance deployment makes this a non-issue.

### 2. Database engine: PostgreSQL

**Final choice: PostgreSQL (Azure Database for PostgreSQL Flexible Server in production, matching engine/major version in local Docker for dev/test)**

**Trade-offs and why PostgreSQL won:**

| Aspect | PostgreSQL | Azure SQL | Cosmos DB |
|--------|------------|-----------|-----------|
| **Domain fit** | Excellent — tree structure maps cleanly to foreign keys + parent-child relationships | Excellent — identical relational model | Poor — document model fights hierarchical constraints (tree embedding vs. referencing tradeoffs) |
| **Optimistic concurrency** | Native `xmin` system column — perfect backing for `RowVersion` EF Core concurrency checks (no hand-maintained column needed) | Row versioning exists; hand-maintained `RowVersion` column needed | Document versioning works but less natural |
| **Vector search (future semantic work)** | **Decisive win:** `pgvector` extension + `pg_trgm` for text similarity. Exact cosine search via pgvector is fast enough indefinitely at same-project scale (see Similar Tasks Suggestions). Zero new infrastructure. | No native vector search — requires bolted-on separate vector store (Azure Cognitive Search, Elasticsearch, etc.). Splits architecture, operational complexity. | Native vector search, but document model forcing schema rework to use it |
| **Tooling maturity** | EF Core + Npgsql, battle-tested; migrations smooth | EF Core + SqlClient, battle-tested; migrations smooth | EF Core support exists but Document DB paradigm complicates the tree model |
| **Local dev/prod consistency** | Same engine/version across Docker (local) → managed Azure service (prod) — migrations and behavior stay consistent | Same engine in Docker → managed Azure — consistency guaranteed | Same — but document model inconsistency matters more here |

**Decision rationale:** The tree-shaped domain (strict Project → Feature/Task hierarchy) is the relational sweet spot. Optimistic concurrency via `xmin` is both more elegant and cheaper than hand-maintaining a version column. **The decisive factor:** Similar Tasks Suggestions groundwork (section 5 below) answers "semantic search is probably yes eventually." On Postgres, that's "enable pgvector, add an embedding column" — a 10-minute schema change. On Azure SQL, it's "deploy a separate vector store and integrate it into the API" — months of work. For a portfolio project betting on extensibility, that future-proofs the choice now.

**Migration tooling:** EF Core Migrations, as assumed in the master plan. No special version-control strategy needed for a single-engine choice; migrations live in `PlanIt.Api/Migrations/`, checked in.

### 3. Real-time collaboration: SignalR architecture

#### Hub design: Per-project groups

**The hub uses per-project groups, not a global broadcast.**

```
When a user connects or opens a project:
  1. SignalR connection established (negotiation/WebSocket upgrade)
  2. Client invokes hub.Groups.AddToGroupAsync(connId, $"project-{projectId}")
  3. Server verifies the user has access to that project
  4. Only if verified: connection added to the group
```

**Why per-project groups:**
- **Authorization:** Group membership is verified at join time (server-side), so unauthorized clients never receive payloads at all, not "receive-then-discard" client-side.
- **Efficiency:** Updates broadcast only to users with a stake in that project, no noise across unrelated projects.

**Critical implementation detail — reconnection gotcha:** Group membership is tied to `connectionId`, which changes on every reconnect (including automatic reconnects after network blips). The hub's `OnConnectedAsync` method must re-run the "verify membership → re-join groups" logic on *every* connect, not just the first one. Otherwise, a client silently stops receiving updates after a network blip while the UI still shows "connected."

#### Request/write flow: REST writes, domain events for broadcasts

**All work-item mutations flow through REST, never through hub methods.**

```
User action (drag task to "In Progress"):
  1. Client → PATCH /api/projects/{id}/workitems/{id}  { status: "InProgress" }
  2. API layer validates, calls service
  3. Service layer validates business rules, updates DB
  4. Service publishes domain event: TaskStatusChangedEvent(taskId, projectId, newStatus)
  5. Event handler (TaskStatusChangedNotifier) subscribed to the event
  6. Handler → hub.Clients.Group($"project-{projectId}").SendAsync("TaskUpdated", ...)
       (excluding the originating client via GroupExcept)
```

**Why this shape:**
- Service layer stays transport-agnostic (no HTTP, no SignalR dependencies).
- No accidental second write path through the hub (avoids validation divergence).
- Extensibility: future subscribers (audit log, embedding-recompute trigger) hook into the same event stream without touching the write path.
- Broadcast originates from the same service call that wrote the data, so the payload is always fresh.

**Event originator exclusion:** The client that made the change already has the result from its own REST response — don't re-broadcast to it via SignalR.

#### MVP event set (7 events)

Reconciles the earlier data-loading-architecture doc's generic "invalidation" into concrete events, split by urgency:

**Structural events (need full payload, board updates immediately):**
- `WorkItemCreated(workItemId, projectId, parentId, type, title, ...)` — new card appears live on collaborators' boards.
- `WorkItemDeleted(workItemId, projectId, parentId)` — card disappears immediately; UI must remove cached item and any child-cache entries.
- `WorkItemStatusChanged(workItemId, projectId, newStatus, oldStatus)` — the core drag-and-drop interaction; card must move columns live.
- `WorkItemMoved(workItemId, projectId, newParentId, oldParentId)` — reparented between Feature/Project; card moves lists immediately.

**Content-only events (lightweight invalidation pings):**
- `WorkItemUpdated(workItemId, projectId)` — title/description/tags/assignee edited; doesn't change board layout. Marks cached entry stale, re-fetch on next open.

**Membership events:**
- `ProjectMemberAdded(projectId, userId)` — collaborator list updates; new member is added to the project's SignalR group.
- `ProjectMemberRemoved(projectId, userId)` — collaborator removed; member left the group.

#### Token refresh and idle SignalR-only sessions

**Token refresh is proactive and timer-based, not reactive.**

Problem: A user can sit on the board watching live SignalR updates with zero outgoing REST calls. If token refresh were tied to REST 401 responses or SignalR reconnect events, a token expiring mid-idle would never trigger a refresh — the user would be silently authenticated with an expired token (or the next action fails).

Solution:
- Frontend maintains an in-memory access token.
- A timer refreshes this token proactively at ~80% of its TTL (e.g., 12 min for a 15-min token), regardless of user activity.
- `accessTokenFactory` (SignalR's callback to get the current token) just returns whatever the timer-refreshed in-memory value is.
- REST calls use the same in-memory token; no special refresh logic per call.

This gives one source of truth (the timer) that both REST and SignalR read from, so an idle SignalR-only user's token stays fresh.

**Deliberately accepted gap:** The server does not actively force-disconnect a WebSocket the instant a token's expiry timestamp passes. A connection can linger slightly beyond the token's nominal lifetime. This is fine because:
- The proactive timer ensures tokens don't actually go stale in normal use.
- The exposure window is bounded by the token's own short TTL.
- Acceptable for a portfolio-project threat model (not a multi-tenant SaaS with strict session policies).

#### Reconnect/missed-event handling

**Periodic refetch + eventual consistency is acceptable.**

If a client drops mid-session and reconnects:
- The client re-establishes the WebSocket (new `connectionId`, automatically re-joins the group via the fixed `OnConnectedAsync`).
- For updates that fired while the client was offline, the client doesn't need an explicit "resync" request.
- Instead: periodic background refetch (e.g., every 30 sec, or on-demand when user refocuses the tab) ensures data converges.
- The API must reject/correct stale writes (optimistic concurrency) without corrupting data, but doesn't need to replay missed broadcasts.

This trades "instantaneous perfect consistency" for architectural simplicity — acceptable since the project scope is a single first-party app, not a distributed system with strict causality requirements.

#### Cascade behavior for completion

**Completed status cascades to children; completion from a partial state does NOT cascade backward.**

- If a Feature is marked complete, all its child Tasks are also marked complete.
- If a Task is marked complete, it has no children so nothing cascades.
- If a Feature or Task is marked back to (e.g.) "In Progress," its children stay in their current status — no reverse cascade.

Why this shape: Marking a parent complete is often a convenience (collect all work together), but un-completing a parent shouldn't force un-complete its children if some were already done. Asymmetry is intentional.

**Implementation:** Delete logic and completion cascade logic must both be written extensibly (registered per-child-type strategy), so adding a future hierarchy level (e.g., "Stories" above Features) is a small addition, not a rewrite.

### 4. Authentication & authorization

#### 404, not 403

**Unauthorized project access returns 404, not 403.**

A user attempting to access a project they're not a member of receives a 404 ("Not Found"), not a 403 ("Forbidden"). This avoids leaking the existence of projects the user shouldn't know about.

#### CORS & SignalR handshake

**CORS policy:**
- **Allowed origins:** `https://slhote.github.io` (frontend on GitHub Pages).
- **Credentialed requests:** Allowed (required for SignalR to send the access token in the handshake).
- **Allowed methods:** GET, POST, PATCH, DELETE (no PUT, since PATCH is the idempotent mutation verb).
- **Allowed headers:** Standard + `Authorization` (Bearer token).

**SignalR handshake flow:**
1. Client initiates negotiate: `POST /hub/negotiate` (unauthenticated HTTP request to get connection info).
2. Server responds with connection details.
3. Client establishes WebSocket: `GET /hub?access_token={token}` (token passed as query string in the upgrade request).
4. Server validates the token via middleware/hub filter before accepting the connection.

Query-string tokens for WebSocket upgrade are a standard SignalR pattern (browsers can't set custom headers on WebSocket upgrade).

### 5. Similar Tasks Suggestions: MVP groundwork

Full feature detail lives in [`similar-tasks-feature-planning.md`](similar-tasks-feature-planning.md). This subplan answers the groundwork questions to avoid retrofit later.

#### Definition of "similar"

**MVP = lexical + metadata (tags/project/assignee), no behavioral or collaborative signals.**

- **Behavioral signals ruled out entirely** — co-occurrence of tasks in the same sprint, assigned to same user, edited together. At portfolio scale with no historical data, these signals are either empty or noise.
- **Collaborative signals ruled out entirely** — too fine-grained and privacy-sensitive for this scope.
- **Semantic (embeddings) vs. lexical trade-off:** Semantic similarity subsumes lexical (same text detected via embeddings as well as keywords), so they're not two separate parallel signals. Build lexical first (cheap, interpretable); add embeddings later if lexical clearly falls short.

**MVP signals:**
1. `TagOverlapSignal` — shared tags (case-insensitive, per-project scope).
2. `ProjectMatchSignal` — same project (gates all comparisons; same-project scope only, not cross-project).
3. `AssigneeMatchSignal` — same assignee.
4. `LexicalTextSignal` — keyword overlap in title + description.
5. `WeightedSimilarityScorer` — combines the above via DI; each signal contributes a normalized score (0–1), weighted sum produces final similarity.

#### Extensibility design: ISimilaritySignal interface

```csharp
public interface ISimilaritySignal
{
  double Score(WorkItem candidate, WorkItem reference);
}

// MVP implementations:
public class TagOverlapSignal : ISimilaritySignal { ... }
public class ProjectMatchSignal : ISimilaritySignal { ... }
public class AssigneeMatchSignal : ISimilaritySignal { ... }
public class LexicalTextSignal : ISimilaritySignal { ... }

// Future semantic work:
public class EmbeddingSimilaritySignal : ISimilaritySignal { ... }  // Add later, no controller/API changes
```

**Why no pre-built pgvector column:** Deliberately not adding a vector column to the schema now. The interface seam is what protects the future swap, not pre-built infrastructure. This keeps the immediate schema simple and avoids premature commitment.

**API shape:** A single `GET /projects/{projectId}/workitems/{workItemId}/similar-tasks` endpoint returns a list of similar work items. No database precomputation; scoring happens on-demand per request.

#### Data audit: Tags/Labels schema

**Tags are the one net-new entity needed** (Project, Assignee, Title, Description already exist for other reasons).

**Tag shape:**
- Free-text strings, 1–25 characters per tag (case-insensitive for matching).
- Cardinality: max 3 tags per work item.
- **Scope: per-project, not shared globally.** The string "bug" in Project A and "bug" in Project B are unrelated tags that happen to share text. A `TagOverlapSignal` match must be gated by `ProjectMatchSignal` (same project) to be meaningful.

**Schema implications:**
- Add a `WorkItemTags` junction table (or a `tags` JSON array column, if using Postgres JSONB) — details deferred to the Persistence subplan.
- No global Tag entity; no tag-autosuggest by popularity (too much scope). Tags are just strings.

#### Candidate scope: Same-project only

**Similar-task searches are restricted to the same project as the reference task.**

Why: Tags are per-project scoped, so semantic comparisons across projects (even with a future embeddings model) would need per-project embeddings anyway (different project contexts, different terminology). Keeping the candidate pool to one project simplifies scale management (a "loop and compare" approach stays cheap even if a project has hundreds of tasks) and aligns with tags being project-scoped.

#### Compute timing: On-demand, no precomputation

**Similarity is computed on-demand when a user opens a task. No background precomputation, no cache.**

Why:
- Same-project scope keeps the candidate pool small (tens to low hundreds per project).
- Scoring four cheap signals (tag overlap, project match, assignee match, keyword overlap) against a few hundred candidates is trivial work.
- On-demand computation is strictly fresher than any cache — zero staleness (data is always current).
- Avoids cache-invalidation complexity entirely.
- Also means the future embeddings signal (exact pgvector cosine search) stays fast at same-project scale; no approximate-nearest-neighbor index (HNSW/IVFFlat) needed.

#### Edge cases

**Zero matches:** Return a plain empty state ("No similar tasks found yet"). No fallback (e.g., "explore by tag") added now; tag-browse can be its own feature later if wanted.

**Completed/archived tasks:** Excluded from candidates. The list stays focused on other open work; reference materials (e.g., "this was solved before") are a separate feature, not part of MVP similar-tasks.

**Future semantic work:** When embeddings are added (subplan 8, once seed data and metadata baseline exist), the `EmbeddingSimilaritySignal : ISimilaritySignal` just gets registered in DI. No API/controller changes; the scorer automatically includes it.

### 6. Error handling

**Global exception handling via `IExceptionHandler` classes** (ASP.NET Core 10 pattern).

**Architecture:**
- Service/domain layer throws typed, HTTP-agnostic exceptions: `TaskNotFoundException`, `ConcurrencyConflictException`, `ValidationException` (for business-rule violations).
- Each exception type has a corresponding `IExceptionHandler` in the API layer that maps it to a `ProblemDetails` response (RFC 7807) with the appropriate status code.
- Basic model-binding validation (missing field, wrong type) is automatic via `[ApiController]` — returns 400 `ValidationProblemDetails` with no custom code.
- Exception details/stack traces are hidden in production (gated behind `IsDevelopment()`); development mode shows full traces.
- The handler is also the place to log unhandled exceptions (via DI'd `ILogger<T>`).

**Example mapping:**
- `TaskNotFoundException` → 404 Not Found
- `ConcurrencyConflictException` (stale `xmin` write) → 409 Conflict
- Business-rule `ValidationException` → 400 Bad Request

### 7. Secrets management & JWT signing

#### JWT signing: HS256 (shared secret)

**Algorithm: HS256 (HMAC with a shared secret key).**

Why not RS256 (asymmetric): RS256 makes sense when multiple independent services need to *verify* tokens without being trusted to *mint* them. PlanIt is a single API service doing both, so a single shared secret is the right fit. RS256 would be solving a problem that doesn't exist.

#### Secrets management

**Local dev:** .NET User Secrets (`dotnet user-secrets set "Jwt:SigningKey" "your-key"`). Built into the tooling, keeps secrets out of source control and `appsettings.json`.

**Deployed (baseline):** Environment variables (`Jwt__SigningKey` env var on the Azure App Service or Container App). Works anywhere, no vendor lock-in.

**Future upgrade:** A cloud secret manager (e.g., Azure Key Vault) is a natural upgrade once a hosting target is locked in, but that's deferred to the DevOps/Hosting subplan. This subplan only requires that env vars are the baseline.

**Pattern:** `appsettings.json` holds structure/schema only, never real secret values. Secrets come from User Secrets (local) or environment variables (deployed).

#### No dual-key rotation window for MVP

**When the signing key changes, all currently-issued tokens become invalid immediately.**

Why no grace window: Graceful rotation (two keys accepted in parallel for a window, allowing old tokens to still verify while new tokens are issued under the new key) exists to avoid logging out active users during routine scheduled rotation. However:
- A *compromised*-key scenario (the reason you'd want an instant rotation) actually wants the hard cutover — keeping the old key in the accepted set defeats the purpose.
- For a portfolio project, logging out active users (worst case) is an acceptable cost; the threat model doesn't justify dual-key infrastructure.

This is a known, accepted limitation: rotate the key only when necessary, understanding it will invalidate all sessions.

### 8. Observability

**Skip Application Insights for MVP.** It's overkill for a single-service portfolio project.

**Instead:** Wire structured logging via DI (`ILogger<T>` from the framework).

**Future extensibility:** Leave commented placeholder code and configuration keys (`Logging:LogLevel`, `ApplicationInsights:InstrumentationKey`) in both local and production config, so App Insights becomes a drop-in later (set an env var, uncomment the registration, done).

**Health check:** Single endpoint (`GET /health`) confirming the API is up and DB is reachable (for Azure deployment health probes).

### 9. API surface: no versioning

**Skip API versioning for MVP.** No `/api/v1/` prefix, no versioning package/infrastructure.

Why: PlanIt.Api and PlanIt.Web are a first-party pair deployed together. There's no independent consumer with its own release cycle to protect a contract for. Adding versioning infrastructure (package, multiple concurrent implementations, deprecation headers) solves a problem that doesn't exist yet.

**Future:** If the API becomes consumed independently (mobile app, third-party integration), versioning is added then. The controller signatures and routing are version-agnostic, so adding it later is a straightforward retrofit.

### 10. Input sanitization & validation

**API layer:** Trim and validate all string inputs (null/empty checks, length limits). Reject invalid data at the boundary, not deep in the service layer.

**Frontend:** Client-side form validation (no blank/invalid submits) — improves UX, not a security boundary.

**Backend never trusts the frontend.** All validation happens server-side on the API boundary.

### 11. Strict layering requirement

**All changes must respect the layer boundary:**

```
Client (React frontend)
  ↕ [HTTP/SignalR, CORS-guarded]
API layer (controllers, DTOs, auth)
  ↕ [service interfaces]
Service layer (domain logic, transactions)
  ↕ [repository/data-access interfaces]
Data Access layer (EF Core)
  ↕ [SQL]
Database (PostgreSQL)
```

No skipping layers (e.g., controller calling data access directly, service calling HTTP). This keeps business logic testable and reusable, and makes future features (audit log subscribers, background jobs) fit naturally into the seams.

---

## Configuration file structure

**Note:** Full details deferred to the DevOps/Hosting subplan, but shape confirmed here.

**appsettings.json (checked in, no secrets):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "[replaced by env var in deployed]"
  },
  "Jwt": {
    "Issuer": "PlanIt",
    "Audience": "PlanIt",
    "ExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "ApplicationInsights": {
    "Enabled": false,
    "InstrumentationKey": "[placeholder — enable in production]"
  }
}
```

**appsettings.Development.json (not checked in, local dev only):**
```json
{
  "Logging": { "LogLevel": { "Default": "Debug" } },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=PlanIt;Username=postgres;Password=postgres"
  }
}
```

**Deployed (via App Service env vars):**
```
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...
Jwt__SigningKey=[secret from Key Vault or stored directly as env var]
Logging__LogLevel__Default=Warning
```

---

## Verification

This is a design decision document. Verification happens at the subplan level:

1. **Persistence subplan** confirms the schema matches the decisions here (Tags/Labels, cascade rules, RowVersion for concurrency).
2. **API Contracts & Backend subplan** implements SignalR hub with per-project groups, domain events + notifiers, exception handlers, strict layering.
3. **Frontend subplan** implements proactive token-refresh timer, SignalR client integration (per-project group joins on `OnConnectedAsync`).
4. **DevOps/Hosting subplan** sets up `.env` files, Dockerfile, docker-compose, CI/CD with environment-specific config.
5. **Testing subplan** includes integration tests for concurrency conflict handling, end-to-end multi-user SignalR scenarios, and seeded data for Similar Tasks Suggestions.
6. **Concurrency Learning + Decision subplan** happens in parallel with API implementation, ending in a confirmed choice (optimistic vs pessimistic).
7. **Similar Tasks Suggestions subplan** (post-MVP) extends the `ISimilaritySignal` interface with additional implementations once seed data exists.

No code exists yet; decisions are validated by their consistency across subplans and their absence of hidden contradictions.
