# PlanIt

Keep track of the work and plans you have with PlanIt! PlanIt helps you organize your projects and break down the work into more manageable features and tasks so you don't get overwhelmed with everything that needs to get done.

A mobile-first task board web app for breaking project work into a **Project → Feature/Task → Subtask** hierarchy, with drag-and-drop status columns and real-time multi-user collaboration. Built as a portfolio project.

## Stack

- **PlanIt.Api** — ASP.NET Core 10 Web API (backend, SignalR for real-time updates)
- **PlanIt.Web** — React 19 + TypeScript + Vite (mobile-first frontend)
- **PlanIt.Api.Tests** — xUnit tests for the API
- Database — TBD, see the persistence subplan in `.claude/docs/plans/`
- Hosting — API + DB on Azure, frontend on GitHub Pages, Docker for the API and local dev

## Project status

Early scaffolding stage. See `.claude/docs/plans/planit-master-plan.md` for the overall plan, decisions made so far, and the subplan breakdown (system design, persistence, API contracts, frontend, DevOps, testing).

## Running locally

### Backend

```bash
dotnet run --project PlanIt.Api
```

### Frontend

```bash
cd PlanIt.Web
npm install
npm run dev
```

### Tests

```bash
dotnet test
```

## Known issues

- `PlanIt.Api` currently pulls a transitively vulnerable `Microsoft.OpenApi` 2.0.0 (`NU1903`, GHSA-v5pm-xwqc-g5wc) via `Microsoft.AspNetCore.OpenApi` 10.0.10 — the patched 3.x line of `Microsoft.OpenApi` is not yet compatible with that package's XML-comment source generator. Revisit when a compatible `Microsoft.AspNetCore.OpenApi` release ships.
