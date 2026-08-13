# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

**PlanIt** is a mobile-first task board web app for breaking project work into a Project → Feature/Task hierarchy, with drag-and-drop status columns and real-time multi-user collaboration. Portfolio project.

- **PlanIt.Api** — ASP.NET Core 10 Web API + SignalR hub. Fully implemented: EF Core + PostgreSQL, all CRUD endpoints, JWT auth with refresh rotation, MediatR-dispatched SignalR events, policy-based access control. See [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md).
- **PlanIt.Web** — React 19 + TypeScript + Vite frontend, mobile-first. Fully implemented and integrated against the real backend: real fetch client, JWT auth with proactive refresh, SignalR live updates. See [PlanIt.Web/CLAUDE.md](PlanIt.Web/CLAUDE.md).
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

**Both the backend and frontend are fully built and integrated.** The `CLAUDE.md` files have been updated (2026-08-13) to reflect the actual code state. The high-level picture:

- **`PlanIt.Api`** — complete: EF Core + Npgsql + PostgreSQL, two migrations, all CRUD controllers (Auth, Projects, ProjectMembers, Users, WorkItems), JWT auth with refresh-token rotation, policy-based `ProjectMember` access control, SignalR hub with MediatR-dispatched events, global RFC 7807 exception handling, health endpoint. See [PlanIt.Api/CLAUDE.md](PlanIt.Api/CLAUDE.md) for full detail.
- **`PlanIt.Web`** — complete: real fetch-based API client, real JWT auth with proactive refresh, SignalR live updates wired via `src/realtime/`. See [PlanIt.Web/CLAUDE.md](PlanIt.Web/CLAUDE.md).
- **`PlanIt.Api.Tests`** — empty placeholder test only. The Testing subplan (master plan subplan 6) has not started.

**What's not done yet:**
- **DevOps / deploy to production** — Dockerfile exists and is production-ready; CI (build/test) workflows exist. CD is entirely absent: no Azure provisioning, no GitHub Pages deploy workflow, no `vite.config.ts` base path, no `404.html` SPA fallback. See [planit-devops-hosting.md](.claude/docs/plans/planit-devops-hosting.md).
- **Testing** — `PlanIt.Api.Tests` has no Moq and no real tests. Master plan subplan 6, not yet started.
- **Similar Tasks Suggestions** — route stub only (`GET .../similar-tasks`). Master plan subplan 8, post-MVP.
- **Concurrency Learning session** — deliberately deferred (master plan subplan 7).
- `PlanIt.Api.csproj` carries a known transitive `NU1903` warning (`Microsoft.OpenApi` 2.0.0) — not resolvable without breaking the OpenAPI source generator; don't silently suppress it, revisit when upstream ships a fix.

## Git Workflow

**Multi-step work (a subplan, a multi-session feature) does not merge to `main` piecemeal.** PRs #8–12 did — five short-lived branches, each merged straight to `main` individually within 25 minutes, no combined review point — and that's the anti-pattern to avoid going forward, not a precedent to repeat.

For any unit of work that spans more than one PR-sized change:
- Cut an integration branch off `main` for the whole unit of work (e.g. `integration/<name>`). Keep it out of the `feature/` prefix used by sub-step branches below — git refs are path-like, so a branch named `feature/<name>` can't coexist with `feature/<name>/01-...` (a ref can't be both a leaf and a directory in the same namespace).
- Sub-steps branch off the *current tip of* the integration branch (never off `main`, never off another still-open sub-branch), named `feature/<name>/<step>`, and PR into the integration branch, not `main`.
- Dependent sub-steps branch only after their prerequisite has already merged into the integration branch — dependencies resolve through the integration branch, never branch-to-branch.
- Exactly one PR takes the integration branch to `main`, once the whole unit of work (or an agreed, coherent milestone within it) is ready. That is the review gate — nothing else merges to `main` for that unit of work outside it.
- If `main` moves in the meantime, rebase the integration branch onto `main`; don't merge `main` into it.

See [`planit-api-contracts-backend.md`](.claude/docs/plans/planit-api-contracts-backend.md) §9 for a concrete example of this applied to a specific subplan.

## Code Patterns & Standards

Until project-specific conventions are established, default to:
- Intention-revealing names, no `data`/`info`/`temp`/`helper`.
- SOLID principles, dependency injection over statics, testable collaborators.
- Keep files under ~500 lines; split before that.
