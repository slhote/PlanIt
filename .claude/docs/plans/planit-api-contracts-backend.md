# PlanIt — API Contracts & Backend (Subplan 3)

## Context

This is subplan 3 from [`planit-master-plan.md`](planit-master-plan.md), gating almost all remaining implementation work (real auth, frontend integration against a live API, SignalR). It builds directly on subplan 1 ([`planit-system-design-architecture.md`](planit-system-design-architecture.md) — REST/error-handling/SignalR/auth-handshake conventions) and subplan 2 ([`planit-persistence-data-model.md`](planit-persistence-data-model.md) / [`planit-persistence-wiring.md`](planit-persistence-wiring.md) — schema, repository pattern, layering). Nothing here re-decides anything already settled in those docs; where this doc extends beyond them (the `Order` column, MediatR, policy-based authorization), it says so explicitly.

**As of this writing**, `PlanIt.Api` has: all five entities (`User`, `Project`, `ProjectMember`, `WorkItem`, `RefreshToken`), all five repository interfaces + EF Core implementations, `PlanItDbContext` with full relationship/cascade config, JWT Bearer *validation* wired in `Program.cs` (no minting yet), a `"Frontend"` CORS policy, three `IExceptionHandler` classes (`TaskNotFoundException`→404, `ConcurrencyConflictException`→409, `ValidationException`→400) wired to RFC 7807 `ProblemDetails`, and a `/health` endpoint. There are **no controllers, no DTOs, no auth logic, and no SignalR hub** — this doc designs all of that.

**Route convention**: no `/api` prefix, matching the existing `/health` endpoint and the already-decided Similar Tasks route (`GET /projects/{projectId}/workitems/{workItemId}/similar-tasks`).

---

## §1 Full REST Endpoint List

### Auth (`AuthController`)

| Method | Route | Request | Response | Auth | Service call | Exceptions |
|---|---|---|---|---|---|---|
| POST | `/auth/register` | `RegisterRequest` | `AuthResponse` (201) | none | `AuthService.RegisterAsync` | `ValidationException` (username/email taken, weak password) |
| POST | `/auth/login` | `LoginRequest` | `AuthResponse` (200) | none | `AuthService.LoginAsync` | `InvalidCredentialsException` → 401 |
| POST | `/auth/refresh` | `RefreshRequest` | `AuthResponse` (200) | none (refresh token *is* the credential) | `AuthService.RefreshAsync` | `InvalidRefreshTokenException` → 401 |
| POST | `/auth/logout` | `RefreshRequest` | 204 | Bearer | `AuthService.LogoutAsync` | none (idempotent no-op if already revoked) |

### Users (`UsersController`)

| Method | Route | Request | Response | Auth | Service call | Exceptions |
|---|---|---|---|---|---|---|
| GET | `/users/search?q={term}&take={n}` | query | `UserSummaryDto[]` | Bearer | `UserService.SearchAsync` | none (empty `q` → `[]`) |
| GET | `/users/{id}` | — | `UserSummaryDto` | Bearer | `UserService.GetByIdAsync` | `TaskNotFoundException` |

### Projects (`ProjectsController`)

| Method | Route | Request | Response | Auth | Service call | Exceptions |
|---|---|---|---|---|---|---|
| GET | `/projects` | — | `ProjectDto[]` ("my projects") | Bearer | `ProjectService.GetForUserAsync(currentUserId)` | none |
| POST | `/projects` | `CreateProjectRequest` | `ProjectDto` (201) | Bearer | `ProjectService.CreateAsync` | `ValidationException` |
| GET | `/projects/{projectId}` | — | `ProjectBoardDto` (project + all work items) | Bearer + `[Authorize(Policy = "ProjectMember")]` | `ProjectService.GetBoardAsync` | (membership enforced by the policy — see §7; a non-member gets 404 before the action runs) |

`POST /projects` only needs plain Bearer auth — there's no existing project to be a member of yet.

### Project Members (`ProjectMembersController`, nested under `/projects/{projectId}/members`)

All routes: Bearer + `[Authorize(Policy = "ProjectMember")]`.

| Method | Route | Request | Response | Service call | Exceptions |
|---|---|---|---|---|---|
| GET | `/projects/{projectId}/members` | — | `ProjectMemberDto[]` (with nested `user`) | `ProjectMemberService.GetForProjectAsync` | — |
| POST | `/projects/{projectId}/members` | `AddProjectMemberRequest{userId, role?}` | `ProjectMemberDto` (201) | `ProjectMemberService.AddAsync` | `TaskNotFoundException` (user not found), `ValidationException` (already a member) |
| DELETE | `/projects/{projectId}/members/{userId}` | — | 204 | `ProjectMemberService.RemoveAsync` | `TaskNotFoundException` (membership not found), `ValidationException` (would remove the last Owner) |

**Note on "already a member"**: modeled as `ValidationException` → 400, not a new 409 type. 409 (`ConcurrencyConflictException`) is reserved exclusively for optimistic-concurrency conflicts per its documented purpose in subplan 1 — "already a member" is a business-rule conflict, not a stale-write conflict, so overloading 409 for both would blur that meaning. The frontend distinguishes this case by message, not status code.

### Work Items (`WorkItemsController`, nested under `/projects/{projectId}/workitems`)

All routes: Bearer + `[Authorize(Policy = "ProjectMember")]`.

| Method | Route | Request | Response | Service call | Exceptions |
|---|---|---|---|---|---|
| GET | `/projects/{projectId}/workitems/{id}` | — | `WorkItemDto` | `WorkItemService.GetByIdAsync` | `TaskNotFoundException` |
| GET | `/projects/{projectId}/workitems/{id}/children` | — | `FeatureDetailDto{feature, childTasks[]}` | `WorkItemService.GetFeatureDetailAsync` | `TaskNotFoundException` (not found, or not a Feature) |
| POST | `/projects/{projectId}/workitems` | `CreateWorkItemRequest` | `WorkItemDto` (201, client-generated GUID id, upsert-idempotent) | `WorkItemService.CreateAsync` | `ValidationException` (hierarchy invariant, tag count), `TaskNotFoundException` (parent/assignee not found) |
| PATCH | `/projects/{projectId}/workitems/{id}` | `UpdateWorkItemRequest` | `WorkItemDto` | `WorkItemService.UpdateAsync` | `TaskNotFoundException`, `ValidationException`, `ConcurrencyConflictException` (stale `rowVersion`) |
| DELETE | `/projects/{projectId}/workitems/{id}` | — | `DeleteWorkItemResponse{deletedIds[]}` | `WorkItemService.DeleteAsync` | `TaskNotFoundException` |
| GET | `/projects/{projectId}/workitems/{id}/similar-tasks` | — | `WorkItemSummaryDto[]` | `SimilarTasksService.GetSimilarAsync` | **deferred to subplan 8** — route reserved now (not registered, or returns 501), not implemented |

### Health / hub handshake

- `GET /health` — already built.
- `POST /hub/negotiate` (unauthenticated) → `GET /hub?access_token={token}` — SignalR handshake, see §5.

---

## §2 DTO Shapes

All C# `record`s, default `System.Text.Json` serialization (camelCase property names — the default, no config needed). `Guid` fields serialize as strings; `DateTimeOffset` as ISO-8601 strings, matching the frontend's existing timestamp field shapes.

**Required global config**: register `JsonStringEnumConverter` (`AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`). System.Text.Json serializes enums as numbers by default — the frontend's `WorkItemType`/`WorkItemStatus`/`ProjectMemberRole` TypeScript types are string unions (`"Feature" | "Task"`, etc.) matching the C# enum *member names* exactly, so this converter is required for wire compatibility, not optional.

```csharp
// --- Auth ---
public record RegisterRequest(string Username, string Email, string Password);
public record LoginRequest(string UsernameOrEmail, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(UserSummaryDto User, string AccessToken, int ExpiresInSeconds, string RefreshToken);

// --- Users ---
public record UserSummaryDto(Guid Id, string Username, string Email, DateTimeOffset CreatedAt);

// --- Projects ---
public record CreateProjectRequest(string Name, string? Description);
public record ProjectDto(Guid Id, string Name, string? Description, Guid CreatedByUserId, DateTimeOffset CreatedAt);
public record ProjectBoardDto(ProjectDto Project, IReadOnlyList<WorkItemDto> WorkItems);

// --- Project Members ---
public record AddProjectMemberRequest(Guid UserId, ProjectMemberRole Role = ProjectMemberRole.Member);
public record ProjectMemberDto(Guid ProjectId, Guid UserId, ProjectMemberRole Role, DateTimeOffset JoinedAt, UserSummaryDto User);

// --- Work Items ---
public record CreateWorkItemRequest(
    Guid Id,                  // client-generated GUID, PK — server upserts (idempotent create)
    WorkItemType WorkItemType,
    Guid? ParentId,
    string Title,
    string? Description,
    Guid? AssigneeId,
    IReadOnlyList<string> Tags);

public record UpdateWorkItemRequest(
    string? Title = null,
    string? Description = null,
    WorkItemStatus? Status = null,
    Guid? AssigneeId = null,
    IReadOnlyList<string>? Tags = null,
    double? Order = null,          // see §6
    uint? RowVersion = null);      // xmin, for optimistic-concurrency check

public record WorkItemDto(
    Guid Id, WorkItemType WorkItemType, Guid ProjectId, Guid? ParentId,
    string Title, string? Description, WorkItemStatus Status, Guid? AssigneeId,
    IReadOnlyList<string> Tags, double Order,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, uint RowVersion);

public record WorkItemSummaryDto(Guid Id, WorkItemType WorkItemType, string Title, WorkItemStatus Status); // similar-tasks (subplan 8)
public record FeatureDetailDto(WorkItemDto Feature, IReadOnlyList<WorkItemDto> ChildTasks);
public record DeleteWorkItemResponse(IReadOnlyList<Guid> DeletedIds);
```

**Divergences from the frontend mock (`PlanIt.Web/src/api/*.ts`), justified**:
- Auth DTOs are wholly new — the mock has no real login/register (it picks from seeded users). `AuthResponse` matches the mock's `MockLoginResult` field names (`user`, `accessToken`, `expiresInSeconds`) plus `refreshToken`, so `PlanIt.Web/src/auth/authStore.ts` needs minimal reshaping when real auth lands.
- `WorkItemDto`/`UpdateWorkItemRequest` add `order` (§6) and `rowVersion` — both absent from the mock, which models neither ordering nor concurrency.
- `CreateWorkItemRequest` requires the client to supply `Id` — the mock's `nextId()` was mock-only convenience; real creates are idempotent via client-generated GUID per the master plan.

---

## §3 Service Layer

All services live in `PlanIt.Api/Application/` (new folder, sibling to `Domain`/`Data`), constructor-injecting only `Domain/Repositories` interfaces, `PlanItDbContext` (for `SaveChangesAsync`), and cross-cutting collaborators (password hasher, JWT service, `IMediator`). No HTTP types anywhere in this layer.

- **`AuthService`** — `RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync`. Uses `IUserRepository`, `IRefreshTokenRepository`, `IPasswordHasher<User>`, `IJwtTokenService`. Owns the rotation/reuse-detection state machine (§4).
- **`UserService`** — thin wrapper: `SearchAsync`, `GetByIdAsync` over `IUserRepository`.
- **`ProjectService`** — `GetForUserAsync`, `GetBoardAsync`, `CreateAsync`. `CreateAsync` inserts `Project` + creator `ProjectMember(Role=Owner)` on the same `DbContext`, one `SaveChangesAsync()` call.
- **`ProjectMemberService`** — `GetForProjectAsync`, `AddAsync`, `RemoveAsync`. `RemoveAsync` enforces "at least one Owner remains." Publishes `ProjectMemberAddedNotification`/`ProjectMemberRemovedNotification` via `IMediator` after a successful write.
- **`WorkItemService`** — `GetByIdAsync`, `GetFeatureDetailAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`. Owns:
  - the Feature-no-parent / Task-parent-must-be-Feature invariant,
  - the extensible per-child-type delete-cascade strategy (`ICascadeStrategy<TParent>`, registered per `WorkItemType` — MVP has exactly one non-trivial case, Feature→Task, but this is a registered strategy, not an inline `if`, so a future hierarchy level plugs in as a new registration rather than a service rewrite),
  - the same extensible-strategy shape for the completion cascade (completing a Feature cascades to its Tasks; reverse never cascades),
  - `Order` assignment on create/update (§6),
  - publishes the 4 structural + 1 content-only work-item notifications via `IMediator` after a successful write.
- **`SimilarTasksService`** — deferred to subplan 8; stub only, not wired into DI yet.

**Membership is not checked in the service layer.** Per §7, `[Authorize(Policy = "ProjectMember")]` on the controller/action enforces it before any service method runs — services don't take a `userId` parameter purely for that check (though they still take one where it's substantively used, e.g. `ProjectService.GetForUserAsync`, or as an audit/provenance value on writes).

---

## §4 Auth Implementation Plan

**Password hashing: `Microsoft.AspNetCore.Identity`'s `PasswordHasher<User>`.** Lightweight package (not the full Identity/EF store system — no `IdentityDbContext`, no user-management UI), ships PBKDF2 with adaptive iteration counts and a versioned hash format, no new third-party dependency.

**Refresh token value & hashing**: raw token = 256-bit random value (`RandomNumberGenerator.GetBytes(32)`), Base64Url-encoded, returned to the client once. Stored server-side as `TokenHash = SHA256(rawToken)` — a fast hash is appropriate here (unlike a password, a refresh token is already high-entropy random, so PBKDF2-style slow hashing buys nothing and costs perf on every refresh lookup).

**JWT minting (`IJwtTokenService`)**: claims = `sub` (userId), `unique_name` (username), `jti` (unique token id). Signed HS256 with `JwtOptions.SigningKey`, `exp` = `ExpirationMinutes` (15) — matches the `TokenValidationParameters` already configured in `Program.cs` for *validation*; this service is purely the *minting* counterpart, no new validation config needed.

**Refresh rotation/reuse-detection (`AuthService.RefreshAsync`)**:
1. Hash presented raw token, `GetByTokenHashAsync`.
2. Not found → `InvalidRefreshTokenException` (401).
3. `RevokedAt != null` → reuse detected: `GetActiveForUserAsync(token.UserId)`, set `RevokedAt = now` on all of them, `SaveChangesAsync()`, throw `InvalidRefreshTokenException` (401) — forces re-login everywhere.
4. `ExpiresAt < now` → `InvalidRefreshTokenException` (401).
5. Otherwise: mint new access token + new refresh token, set old row `RevokedAt = now`, `ReplacedByTokenId = new.Id`, insert new row, `SaveChangesAsync()`, return `AuthResponse`.

**New exception types** (extend `Domain/Exceptions/`): `InvalidCredentialsException` → 401 (bad login), `InvalidRefreshTokenException` → 401 (bad/reused/expired refresh). Each gets its own `IExceptionHandler`, following the exact pattern of the three existing handlers, registered in `Program.cs` alongside them.

**Controller**: `AuthController` is a thin pass-through to `AuthService` — never touches `IUserRepository`/`IRefreshTokenRepository` directly. `[Authorize]` only on `/auth/logout` (revokes the specific presented refresh token, not "all sessions for this user").

---

## §5 SignalR Hub (MediatR-based)

**Hub class**: `PlanItHub : Hub`, `[Authorize]` at the class level, one method: `JoinProject(Guid projectId)`.

```csharp
public class PlanItHub(IProjectMemberRepository projectMembers) : Hub
{
    public async Task JoinProject(Guid projectId)
    {
        var userId = GetUserId(Context.User);
        if (!await projectMembers.IsMemberAsync(projectId, userId))
            throw new HubException("Not authorized for this project.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
    }
}
```

No group join happens in `OnConnectedAsync` — a client calls `JoinProject` explicitly after connecting, allowing one connection to join multiple project groups (e.g. project-list page vs. a specific board) without reconnecting. The frontend's SignalR client calls `JoinProject` again after every `onreconnected` event, not just on first connect — this is what satisfies subplan 1's "re-verify/re-join on every connect" requirement, since `ConnectionId` (and therefore group membership) resets on reconnect.

**Handshake**: `POST /hub/negotiate` (unauthenticated) → `GET /hub?access_token={token}`. Query-string tokens aren't picked up by the default Authorization-header extraction, so `AddJwtBearer` needs an `OnMessageReceived` override in `Program.cs` to read `access_token` from the query string when the request path starts with `/hub`.

**Domain events, via MediatR** — 7 `INotification` record types, one per event, mirroring the MVP event set from subplan 1:

```csharp
public record WorkItemCreatedNotification(WorkItem Item, string? OriginConnectionId) : INotification;
public record WorkItemDeletedNotification(Guid WorkItemId, Guid ProjectId, Guid? ParentId, string? OriginConnectionId) : INotification;
public record WorkItemStatusChangedNotification(Guid WorkItemId, Guid ProjectId, WorkItemStatus OldStatus, WorkItemStatus NewStatus, string? OriginConnectionId) : INotification;
public record WorkItemMovedNotification(Guid WorkItemId, Guid ProjectId, Guid? OldParentId, Guid? NewParentId, string? OriginConnectionId) : INotification;
public record WorkItemUpdatedNotification(Guid WorkItemId, Guid ProjectId, string? OriginConnectionId) : INotification;
public record ProjectMemberAddedNotification(Guid ProjectId, Guid UserId, string? OriginConnectionId) : INotification;
public record ProjectMemberRemovedNotification(Guid ProjectId, Guid UserId, string? OriginConnectionId) : INotification;
```

One `INotificationHandler<T>` per notification, in a new `Application/Realtime/` folder — this is the only place `Microsoft.AspNetCore.SignalR` types are referenced, keeping the service layer transport-agnostic:

```csharp
public class WorkItemStatusChangedSignalRHandler(IHubContext<PlanItHub> hub) : INotificationHandler<WorkItemStatusChangedNotification>
{
    public Task Handle(WorkItemStatusChangedNotification n, CancellationToken ct)
    {
        var group = $"project-{n.ProjectId}";
        var clients = n.OriginConnectionId is { } id ? hub.Clients.GroupExcept(group, [id]) : hub.Clients.Group(group);
        return clients.SendAsync("WorkItemStatusChanged", new { n.WorkItemId, n.OldStatus, n.NewStatus }, ct);
    }
}
```

Services publish via `IMediator.Publish(...)` after `SaveChangesAsync()`, e.g. `await _mediator.Publish(new WorkItemStatusChangedNotification(item.Id, item.ProjectId, oldStatus, item.Status, originConnectionId))`.

Register `AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>())` in `Program.cs`.

**Origin exclusion**: the frontend sends its current SignalR `connectionId` on mutating REST calls via a custom header (`X-SignalR-Connection-Id`). The relevant controller reads it and passes it through to the service call as a plain string parameter; the service includes it in the notification payload; the handler uses `GroupExcept` when present, `Group` when absent (e.g. no SignalR connection open yet).

**Why MediatR here, deliberately, despite only one consumer per event today**: the 7-event set currently has exactly one handler each (the SignalR broadcast), so a pub/sub bus isn't *required* to solve a fan-out problem — but it was chosen anyway for production-pattern value: a second consumer (an audit-log handler, a cache-invalidation handler) becomes a new class with zero changes to the publishing service or any existing handler, and MediatR's `IPipelineBehavior<TRequest,TResponse>` seam is a documented, standard place to later add cross-cutting concerns (logging, validation) around `Send`/`Publish` calls without repeating boilerplate per handler — not built now, but the seam exists because MediatR is already in the stack.

**Licensing note**: MediatR moved to a commercial license starting with v13 (2025) for commercial use above a revenue threshold; free/OSS use remains unaffected. Fine for this portfolio project, but worth keeping in mind if PlanIt's usage model ever changes.

---

## §6 Position/Ordering Resolution

**New schema addition, beyond subplan 2**: `WorkItem.Order` (`double`, `NOT NULL`) — requires a new EF Core migration, `AddWorkItemOrder`.

**Why fractional indexing, not integer position**: an `int` position requires renumbering every sibling on most inserts (the classic array-shift problem) — either the client sends a full reordered array (a bulk endpoint) or the server does O(n) row updates per drag. A `double` "order key" makes a single-item move a single-row `PATCH`, touching no other rows: new order = midpoint between the two neighbors in the target position (`(prev.Order + next.Order) / 2`), or `neighbor.Order ± 1024` at a list boundary. Same idea as Trello/Figma/Linear's fractional-rank ordering (Jira's LexoRank uses base-62 strings for unbounded precision — a `double` is a simpler, sufficient approximation at this project's scale).

**Grouping scope**: `Order` is meaningful only within a `(ProjectId, ParentId, Status)` triple — one drag-and-drop column of one board view. Comparing `Order` across different parents/statuses is meaningless.

**Assignment on create**: `Order = (max Order among siblings in the same group) + 1024`, landing new items at the end of their column. `WorkItemService.CreateAsync` already queries siblings for the hierarchy-invariant check, so this adds no extra round trip.

**Endpoint shape — reuses the existing `PATCH`, no separate bulk-reorder endpoint**: the frontend computes the new `order` value client-side from its already-sorted, in-memory `dnd-kit` column (midpoint math is trivial there) and sends it as part of the same `UpdateWorkItemRequest` used for any other partial edit, alongside `status` when the drag also crosses columns. This departs from the mock's `reorderWorkItems(orderedIds: Guid[])` bulk-array shape — that shape existed *because* there was no order field to update per-item; once `Order` exists, a single-item `PATCH` is simpler and matches the project's decided "PATCH is the mutation verb, naturally idempotent" convention. A bulk endpoint would need its own partial-failure-across-N-rows idempotency story that per-item PATCH sidesteps.

**Accepted edge case, not solved now**: repeated inserts between the exact same two neighbors could in principle exhaust `double` precision after enough operations. Not reachable at this project's realistic scale (a handful of collaborators, tens of items per column). If it ever became a real concern, the fix is a periodic renormalization pass rewriting a column's `Order` values to evenly-spaced integers — noted as a deliberately-deferred escape hatch, not implemented.

---

## §7 Access Control Pattern

**Decision: policy-based authorization via a custom `IAuthorizationHandler`, applied through `[Authorize(Policy = "ProjectMember")]`.**

```csharp
services.AddAuthorization(options =>
    options.AddPolicy("ProjectMember", p => p.Requirements.Add(new ProjectMemberRequirement())));

public class ProjectMemberRequirement : IAuthorizationRequirement { }

public class ProjectMemberAuthorizationHandler(IProjectMemberRepository projectMembers, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ProjectMemberRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectMemberRequirement requirement)
    {
        var routeValues = httpContextAccessor.HttpContext!.GetRouteData().Values;
        var projectId = Guid.Parse(routeValues["projectId"]!.ToString()!);
        var userId = GetUserId(context.User);
        if (await projectMembers.IsMemberAsync(projectId, userId))
            context.Succeed(requirement);
    }
}
```

`[Authorize(Policy = "ProjectMember")]` is applied per-controller/action as listed in §1. This is an explicit, acknowledged departure from the "repository access is service-layer-only" layering rule in subplan 2 — the authorization handler is not the controller by name, but it sits at the API layer and touches `IProjectMemberRepository` directly. Called out here deliberately, not an oversight: the alternative (a manual `EnsureMemberAsync` check inside each service method) was considered and rejected in favor of this more idiomatic, DRY ASP.NET Core pattern, at the cost of this one layering exception plus the 403→404 rewrite below.

**Custom 403→404 rewrite** — ASP.NET Core's authorization middleware returns 403 on a failed policy by default; the project's "404, not 403" rule (subplan 1, avoid leaking project existence to non-members) requires overriding that:

```csharp
public class ProjectMember404ResultHandler(IProblemDetailsService problemDetailsService) : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var failedWithProjectMember = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure!.FailedRequirements.OfType<ProjectMemberRequirement>().Any();

        if (!failedWithProjectMember)
        {
            await Default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = 404, Title = "Not Found" }
        });
    }
}
```

Registered as `services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProjectMember404ResultHandler>()`. Every other authorization failure (e.g. missing/invalid Bearer token) falls through to the default 401/403 behavior, unchanged.

This is more code than the manual-check alternative — deliberately accepted for the more standard, discoverable pattern (every project-scoped endpoint's access rule is visible as an attribute, not buried in each service method's first line).

---

## §8 Build/Implementation Sequencing

Recommended order across follow-up sessions, each roughly a session-sized chunk. Auth (steps 2–4) gates nearly everything else and is the internal critical path for this subplan, even though this subplan itself can proceed in parallel with DevOps/Frontend-scaffolding work per the master plan's Phase 1 sequencing.

1. **DTOs + read-only endpoints against existing data, no auth yet.** Stand up `ProjectsController`/`WorkItemsController`/`UsersController` GET endpoints with a temporary hardcoded/test `userId`, no `[Authorize]`. Validates DTO shapes and service/repository wiring end-to-end before auth complexity layers on.
2. **Auth: password hashing, JWT minting, register/login.** `AuthService`, `PasswordHasher<User>`, `IJwtTokenService`, `AuthController` register/login only (no refresh yet). Enables real `[Authorize]` for the rest of the API.
3. **Policy-based access control infrastructure.** `ProjectMemberRequirement`, `ProjectMemberAuthorizationHandler`, the `"ProjectMember"` policy registration, and the `ProjectMember404ResultHandler` (§7) — larger than a simple attribute flip, since it's the full custom-policy + result-handler stack. Apply `[Authorize(Policy = "ProjectMember")]` across all project-scoped controllers/actions and confirm 404-not-403 behavior for non-members.
4. **Refresh rotation + logout.** Add the rotation/reuse-detection state machine (§4) and the two new 401 exception types + handlers.
5. **Mutating work item endpoints.** Create/patch/delete, hierarchy invariant, delete-cascade and completion-cascade strategies, the `Order` column + `AddWorkItemOrder` migration + assignment-on-create logic (§6), optimistic-concurrency (`xmin`) surfaced as `rowVersion` and enforced on update.
6. **SignalR hub + MediatR wiring.** `AddMediatR` registration, `PlanItHub`, the 7 notification record types + their 7 `INotificationHandler` classes in `Application/Realtime/`, JWT-from-query-string handshake config, origin-exclusion header plumbing.
7. **Project members endpoints + membership notifications.** Depends only on auth (step 2) and the notification types (step 6) — can slot in alongside step 6 if convenient.
8. **Similar-tasks route stub.** Register the route shape now (returns 501, or is simply omitted from routing) so the URL contract is locked; actual implementation is subplan 8.

---

## §9 Git Branching Strategy for Implementation

**Problem being avoided**: PRs #8–12 (Docker Compose, persistence wiring, CORS, JWT config skeleton, exception handling/health check) were each built on their own short-lived branch off `main` and merged straight back to `main` individually, minutes apart, with no combined review point — five separate merges to `main` in under 25 minutes on 2026-08-12. That pattern is **not** to be repeated for this subplan's implementation. The user reviews once, at the point the *whole* unit of work is ready, not once per sub-step.

**Structure**: `main` ← `integration/api-contracts-backend` (integration branch) ← one short-lived branch per §8 step (or per small cluster of steps).

- **Integration branch**: `integration/api-contracts-backend`, branched from `main` once, at the start of implementation. **Named outside the `feature/` prefix deliberately** — git refs are path-like, so a branch literally named `feature/api-contracts-backend` cannot coexist with `feature/api-contracts-backend/01-...` (a ref can't be both a leaf and a directory in the same namespace); this was hit and fixed during step 1's implementation. Nothing implementing this subplan is ever branched directly from `main`, and nothing implementing this subplan is ever PR'd directly to `main`.
- **Per-step branches**: one branch per §8 step, branched from the *current tip of* `integration/api-contracts-backend` (not from `main`, not from another step's branch). Suggested names, matching §8's numbering:
  - `feature/api-contracts-backend/01-read-only-endpoints`
  - `feature/api-contracts-backend/02-auth-register-login`
  - `feature/api-contracts-backend/03-access-control-policy`
  - `feature/api-contracts-backend/04-refresh-rotation-logout`
  - `feature/api-contracts-backend/05-workitem-mutations-ordering`
  - `feature/api-contracts-backend/06-signalr-mediatr`
  - `feature/api-contracts-backend/07-project-members`
  - `feature/api-contracts-backend/08-similar-tasks-stub`
- **Dependencies resolve through the integration branch, never branch-to-branch.** Step 3 depends on step 2 (needs real `[Authorize]` to test against); step 6 depends on step 5 (broadcasts fire from the mutation endpoints); step 7 depends on steps 2 and 6. When a dependent step is ready to start, its branch is cut *after* the prerequisite step's branch has already been merged into `integration/api-contracts-backend` — never by branching off the prerequisite's still-open branch directly. This keeps every step branch's history traceable to one clean base and avoids resolving the same dependency twice (once in a branch-to-branch merge, again when that branch later merges into the integration branch).
- **Per-step PRs target `integration/api-contracts-backend`, not `main`.** These can move fast — they're checkpoints for catching mistakes early and keeping step-sized diffs reviewable, not the final gate.
- **One final PR, `integration/api-contracts-backend` → `main`,** opened once all 8 steps (or an agreed subset forming a coherent, demo-able milestone — e.g. steps 1–4 as "auth is live" before continuing) have landed on the integration branch. **This is the PR the user reviews before anything reaches `main`.** Nothing merges to `main` outside this one PR for the duration of this subplan's implementation.
- **If `main` moves during implementation** (e.g. an unrelated fix lands), rebase `integration/api-contracts-backend` onto `main` — don't merge `main` into it — to keep the eventual `main`-bound diff clean and reviewable as "what this subplan actually changed."

---

## Critical Files

- `PlanIt.Api/Program.cs`
- `PlanIt.Api/Domain/Entities/WorkItem.cs`
- `PlanIt.Api/Domain/Repositories/IWorkItemRepository.cs`
- `PlanIt.Api/Domain/Repositories/IRefreshTokenRepository.cs`
- `PlanIt.Api/Domain/Exceptions/TaskNotFoundException.cs`
- `PlanIt.Web/src/api/workItems.ts`
- `PlanIt.Web/src/types/domain.ts`

## Verification Approach

This subplan is design-only — no code lands with it. Each implementation step in §8, when built, should include its own verification (e.g. "run `dotnet test`," "hit `/auth/register` then `/auth/login` with curl/Postman and confirm the access token validates," "open two browser sessions, drag a card, confirm the second session's board updates without a refresh"). This document's own verification is that every endpoint in §1 has a DTO in §2, a service method in §3, and — for auth/realtime/ordering/access-control — the design in §4–§7 to build it against, with no unresolved "TBD."
