# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**PlanIt** is a mobile-first task board web app for breaking project work into a Project → Feature/Task hierarchy, with drag-and-drop status columns and real-time multi-user collaboration. Portfolio project.

- **PlanIt.Api** — ASP.NET Core 10 Web API + SignalR hub (planned). Currently an unmodified `dotnet new webapi` scaffold — no domain model, no persistence, no auth, no SignalR hub, no CORS yet. See [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md).
- **PlanIt.Web** — React 19 + TypeScript + Vite frontend, mobile-first. Well past a bare scaffold: full routing, mock auth, and CRUD board pages exist against an in-memory mocked API layer (no live backend to integrate with yet). See [PlanIt.Web/CLAUDE.md](PlanIt.Web/CLAUDE.md).
- **PlanIt.Api.Tests** — xUnit test project; currently one empty placeholder test, no real coverage yet.

`PlanIt.slnx` lists `PlanIt.Api` and `PlanIt.Api.Tests`. `PlanIt.Web` is a plain npm project and is not part of the .NET solution.

Both .NET projects target .NET 10 with nullable reference types enabled.

Read [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md) before working in the API project, and [PlanIt.Web/CLAUDE.md](PlanIt.Web/CLAUDE.md) before working in the frontend — each documents that project's actual current state, stack, and conventions in detail.

## Plan Documents

**ALWAYS place every plan document for this project under `.claude/docs/plans/`** — the master plan, every subplan, every feature-planning doc brought in from elsewhere (e.g. `similar-tasks-feature-planning.md`), and anything written during a future plan-mode session. No exceptions, no ad hoc locations, nothing left stranded outside the repo (e.g. in a plan-mode scratch file, `Downloads`, or elsewhere) — if a plan doc is worth keeping, it gets copied/written into this folder as part of that same turn, not "later."

Start with `.claude/docs/plans/planit-master-plan.md` — it records the cross-cutting decisions already made (idempotency, concurrency approach, auth model, assignment model, cascade delete behavior) and the subplan breakdown/sequencing. **Its DB-engine line is stale** — the master plan itself calls that choice deferred, but the later `planit-system-design-architecture.md` subplan confirms PostgreSQL; when subplans disagree, later ones win. Read both, and [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md)'s "What's actually planned" section for the reconciled, current-as-of-last-review picture, before writing a new subplan or making a decision that conflicts with existing ones.

## Common Commands

```bash
# Run the API
dotnet run --project PlanIt.Api

# Run the frontend
cd PlanIt.Web && npm run dev

# Build everything (.NET)
dotnet build

# Run .NET tests
dotnet test
```

## Current State / Known Gaps

- Database engine is **decided (PostgreSQL)** and schema is fully designed — see `planit-system-design-architecture.md` and `planit-persistence-data-model.md` — but nothing is built yet: no `DbContext`, no migrations, no `EF Core`/`Npgsql` package references.
- No auth implemented yet. Fully designed (JWT-only, access token in memory, refresh token in `localStorage` with rotation-and-reuse-detection) but not built.
- No SignalR hub implemented yet. Fully designed (per-project groups, REST-writes/SignalR-broadcasts-only, 7-event MVP set) but not built.
- `PlanIt.Api` itself has no real endpoints yet — see [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md) for exactly what does/doesn't exist.
- `PlanIt.Web`'s API layer is fully mocked in-memory (`src/api/`) — see [PlanIt.Web/CLAUDE.md](PlanIt.Web/CLAUDE.md); nothing there talks to a real backend yet.
- `PlanIt.Api.csproj` carries a known transitive `NU1903` warning (`Microsoft.OpenApi` 2.0.0) — see README "Known issues." Not yet resolvable without breaking the OpenAPI source generator; don't try to silently suppress it, revisit when upstream ships a fix.

## Git Workflow

**Multi-step work (a subplan, a multi-session feature) does not merge to `main` piecemeal.** PRs #8–12 did — five short-lived branches, each merged straight to `main` individually within 25 minutes, no combined review point — and that's the anti-pattern to avoid going forward, not a precedent to repeat.

For any unit of work that spans more than one PR-sized change:
- Cut an integration branch off `main` for the whole unit of work (e.g. `feature/<subplan-name>`).
- Sub-steps branch off the *current tip of* the integration branch (never off `main`, never off another still-open sub-branch) and PR into the integration branch, not `main`.
- Dependent sub-steps branch only after their prerequisite has already merged into the integration branch — dependencies resolve through the integration branch, never branch-to-branch.
- Exactly one PR takes the integration branch to `main`, once the whole unit of work (or an agreed, coherent milestone within it) is ready. That is the review gate — nothing else merges to `main` for that unit of work outside it.
- If `main` moves in the meantime, rebase the integration branch onto `main`; don't merge `main` into it.

See [`planit-api-contracts-backend.md`](.claude/docs/plans/planit-api-contracts-backend.md) §9 for a concrete example of this applied to a specific subplan.

## Code Patterns & Standards

Until project-specific conventions are established, default to:
- Intention-revealing names, no `data`/`info`/`temp`/`helper`.
- SOLID principles, dependency injection over statics, testable collaborators.
- Keep files under ~500 lines; split before that.
