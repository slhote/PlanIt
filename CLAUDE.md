# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**PlanIt** is a mobile-first task board web app for breaking project work into a Project → Feature/Task → Subtask hierarchy, with drag-and-drop status columns and real-time multi-user collaboration. Portfolio project.

- **PlanIt.Api** — ASP.NET Core 10 Web API + SignalR hub. No persistence layer wired up yet.
- **PlanIt.Web** — React 19 + TypeScript + Vite frontend, mobile-first. Bare Vite template as of this writing — no routing, pages, or API integration yet.
- **PlanIt.Api.Tests** — xUnit tests for the API.

`PlanIt.slnx` lists `PlanIt.Api` and `PlanIt.Api.Tests`. `PlanIt.Web` is a plain npm project and is not part of the .NET solution.

Both .NET projects target .NET 10 with nullable reference types enabled.

## Plan Documents

**ALWAYS place every plan document for this project under `.claude/docs/plans/`** — the master plan, every subplan, every feature-planning doc brought in from elsewhere (e.g. `similar-tasks-feature-planning.md`), and anything written during a future plan-mode session. No exceptions, no ad hoc locations, nothing left stranded outside the repo (e.g. in a plan-mode scratch file, `Downloads`, or elsewhere) — if a plan doc is worth keeping, it gets copied/written into this folder as part of that same turn, not "later."

Start with `.claude/docs/plans/planit-master-plan.md` — it records the cross-cutting decisions already made (idempotency, concurrency approach, auth model, assignment model, cascade delete behavior, deferred DB engine choice) and the subplan breakdown/sequencing. Read it before writing a new subplan or making a decision that conflicts with it.

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

- Database engine not yet chosen — see the master plan's persistence subplan placeholder. No `DbContext`, no migrations yet.
- No auth implemented yet. Master plan specifies JWT-only (access token in memory, refresh token in `localStorage` with rotation) once built.
- No SignalR hub implemented yet.
- Frontend has no routing, pages, or API client yet — bare `create-vite react-ts` scaffold.
- `PlanIt.Api.csproj` carries a known transitive `NU1903` warning (`Microsoft.OpenApi` 2.0.0) — see README "Known issues." Not yet resolvable without breaking the OpenAPI source generator; don't try to silently suppress it, revisit when upstream ships a fix.

## Code Patterns & Standards

Until project-specific conventions are established, default to:
- Intention-revealing names, no `data`/`info`/`temp`/`helper`.
- SOLID principles, dependency injection over statics, testable collaborators.
- Keep files under ~500 lines; split before that.
