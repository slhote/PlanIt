# PlanIt — Master Plan

## Context

PlanIt is a new, standalone portfolio project (unrelated to TrashAnimal): a mobile-first task board web app for breaking project work into a Project → Feature/Task → Subtask hierarchy, with drag-and-drop status columns, multi-user real-time collaboration, and modal-based drill-down navigation between work items. Stack: ASP.NET Core (API) + React (frontend, mobile-first) + a still-to-be-chosen relational-shaped database, hosted on Azure (API/DB) and GitHub Pages (frontend), containerized via Docker. A new GitHub repo (`PlanIt`) will host it.

This document is the top-level plan. It exists to (a) capture the cross-cutting decisions already made so subplans don't re-litigate them, (b) explicitly flag what's still open and where it gets decided, and (c) break the work into subplans with a sequencing/dependency order so implementation can be parallelized across multiple agent sessions without them blocking on each other or producing incompatible contracts.

## Decisions made (binding for all subplans)

1. **Idempotency**: not a blanket hard rule. `PUT`/`PATCH`/`DELETE` must be naturally idempotent (standard REST semantics). Creates use **client-generated GUIDs** as the resource ID with server-side upsert — this makes retried creates genuinely idempotent without a separate idempotency-key subsystem. The full Stripe-style `Idempotency-Key` header pattern is **out of scope for the baseline** and noted as an optional advanced-topics exercise for later (see Deferred/Advanced Topics below).
2. **Concurrency (simultaneous multi-user edits)**: default/tentative approach is **optimistic concurrency** (RowVersion/ETag on work items; stale writes rejected with a conflict response; client refetches). However, this is **not finalized** — the concurrency subplan must fully design *both* optimistic and pessimistic (item-locking) approaches side by side, and a learning session (via the `/learn` skill or equivalent) should happen before either is implemented. Treat the "optimistic" choice as reversible until that comparison + learning pass happens.
3. **Auth**: JWT-only, no cross-site cookies. Short-lived access token (~15 min) held in memory, sent as `Authorization: Bearer`. Longer-lived refresh token returned in the response body and stored in `localStorage` (needed to survive tab/browser close for "return to last project" UX), with refresh-token rotation and server-side revocation (store a hash, support invalidation) to mitigate XSS exposure. SignalR authenticates via its `accessTokenFactory` mechanism. This sidesteps the GitHub Pages ↔ Azure cross-origin cookie complexity (`SameSite=None`/CORS-credentials tuning) entirely.
4. **Work item assignment**: single nullable assignee per work item (no many-to-many join table). Unassigned = null.
5. **Cascade delete**: deleting a Feature or Task deletes all descendant work items. The frontend must confirm with an explicit count ("this will also delete N tasks and M subtasks") before committing the delete.
6. **Database engine**: **deferred** — the persistence subplan will lay out PostgreSQL (Azure Database for PostgreSQL Flexible Server) vs Azure SQL Database vs Cosmos DB with concrete trade-offs and make/confirm the call there. Working assumption for planning purposes only: relational (Postgres or Azure SQL), since the domain is a strict tree and maps cleanly to foreign keys — Cosmos would be a bigger modeling detour. Docker is fine for **local dev** DB regardless of engine; production should use the managed Azure service, not a DB-in-a-container, for durability/backup.

## Deferred / advanced topics (not baseline scope, revisit later)

- Full `Idempotency-Key` header + response-cache pattern (Stripe-style) — optional stretch exercise on 1–2 endpoints once the baseline API works.
- Pessimistic locking as a live alternative to optimistic concurrency — fully designed in the concurrency subplan, decided after a learning session, possibly implemented as a comparison/swap-in later rather than at initial launch.
- Cookie-based auth (option B) — documented as the road not taken; revisit only if a concrete reason to want httpOnly-cookie-level XSS protection on the refresh token comes up.

## Open items each subplan must still resolve

These weren't blocking enough to decide right now, but each owning subplan must make an explicit call (not leave implicit):
- Drag-and-drop library (lean: `dnd-kit`, since `react-beautiful-dnd` is unmaintained) — Frontend subplan.
- GitHub Pages SPA routing fallback (404.html redirect trick vs hash routing) for deep-linkable routes like `project/{id}/feature/{id}` — Frontend subplan.
- Whether a completed Feature/Task auto-cascades status to children, or status is purely independent per item — Persistence + API subplan.
- Exact shape of the "modal navigation stack" (how it maps parent→child drill-down onto browser history / `pushState`, back/forward, swipe gestures) — Frontend subplan.
- Unauthorized project access: confirm plan is "404, not 403" (don't leak existence of a project the user can't see) vs an explicit "access denied" page as originally described — API + Frontend subplans should agree on this together.

## Subplan breakdown

1. **System Design & Architecture** — component diagram (API, DB, SignalR hub, frontend, hosting), request/data flow, finalizes DB engine choice, confirms hosting topology. Foundational; informs everything else.
2. **Persistence / Data Model** — schema for Users, Projects, ProjectMembers (creator + collaborators), WorkItems (likely single table with a `WorkItemType` discriminator + nullable `ParentId`/`ProjectId` enforcing the Project→Feature/Task→Subtask shape at the app layer), status, assignment, RowVersion columns for optimistic concurrency, migration strategy.
3. **API Contracts & Backend** — REST endpoint surface, DTOs, auth endpoints (register/login/logout/refresh), SignalR hub design and event payloads, access-control rules (creator-only project CRUD/membership, member CRUD on work items), user search/lookup endpoint (paginated, filter-as-you-type), idempotent-create implementation, concurrency implementation once decided.
4. **Frontend** — mobile-first React app: routing (incl. the six nested route shapes), first-time vs return-user flows, board grid + drag-and-drop, modal drill-down navigation synced with browser history, work-item CRUD UI, assignee filter, collaborator search/add UI, auth token handling, SignalR client wiring, mockups.
5. **DevOps / Hosting** — Dockerfile for the API, docker-compose for local dev (API + DB), GitHub Actions CI/CD (build/test/deploy), Azure hosting choice for the API (App Service vs Container Apps), GitHub Pages deploy workflow for the frontend, CORS + environment/secrets config across the two origins.
6. **Testing** — unit tests (domain rules, concurrency conflict handling), API integration tests, frontend component/interaction tests, and an end-to-end pass covering the multi-user collaboration + modal-navigation flows specifically, since those are the highest-risk-of-regression areas.
7. **Concurrency Learning + Decision** (small, standalone) — the `/learn`-style deep dive on optimistic vs pessimistic concurrency control, done alongside subplan 3's design work, ending in a final decision recorded back into subplan 3 before that part of the backend is implemented.

## Sequencing for parallel agent work

Not everything can start at once — frontend and backend both depend on the contracts subplan, and the contracts subplan depends on the data model, which depends on the DB engine choice from System Design. Recommended order:

- **Phase 0 (sequential, decide-first)**: System Design & Architecture → Persistence/Data Model. These are small, decision-heavy, and gate everything else — do these before spinning up parallel implementation agents.
- **Phase 1 (parallelizable once Phase 0 lands)**: API Contracts & Backend implementation, DevOps/Hosting setup (Dockerfile, CI skeleton, empty Azure resources), and Frontend scaffolding (routing shell, auth screens, mockups) can all proceed in parallel — the frontend scaffold work doesn't need a live backend yet if it's building against the agreed contract shape from Phase 0/1.
- **Phase 2**: Frontend integration against the real, running API (board CRUD, SignalR live updates, drag-and-drop wired to status changes) — depends on Phase 1's backend being functional.
- **Phase 3**: Concurrency Learning + Decision, threaded in during Phase 1 so the concurrency-sensitive endpoints (work item move/edit) are implemented once, correctly, rather than built optimistic-only and reworked later.
- **Testing** work happens continuously within each subplan (unit/integration tests land with their feature), with a final end-to-end pass as its own step after Phase 2.

## Immediate next steps once this plan is approved

1. Create the `PlanIt` GitHub repository (confirm with you before creating/pushing anything, per standing repo-creation norms).
2. Write the System Design & Architecture subplan under `.claude/docs/plans/` in the new repo (finalizes DB engine, hosting topology).
3. Write the Persistence/Data Model subplan, informed by (2).
4. From there, spin up the parallelizable Phase 1 subplans.

## Verification approach

Since this is a planning document with no code yet, there's nothing to run. Each subplan, when written, should include its own verification section (e.g., "run `dotnet test`," "load the board in the browser and drag a card," "open two browser sessions and confirm SignalR propagates a status change"). The master plan's own "verification" is that every subplan it lists has a clear owner, a clear dependency position in the sequencing above, and no unresolved cross-cutting decision left implicit.
