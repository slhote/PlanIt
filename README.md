# PlanIt

Keep track of the work and plans you have with PlanIt! PlanIt helps you organize your projects and break down the work into more manageable features and tasks so you don't get overwhelmed with everything that needs to get done.

A mobile-first task board web app for breaking project work into a **Project → Feature/Task** hierarchy, with drag-and-drop status columns and real-time multi-user collaboration along with a custom MCP server you can integrate locally with your AI agent to interact with the PlanIt API.

## Learning & Examples
- [Similar-Tasks Scoring Fixtures](https://slhote.github.io/PlanIt/similar-tasks-scoring-fixtures.html) — 43 seeded work items comparing Jaccard vs. TF-IDF lexical strategies, threshold behavior, and structural exclusions

## Running locally

### Quick start (Windows)

```powershell
.\dev.ps1
```

One command brings up the full local stack. It's safe to re-run any time:

- Starts Docker Desktop if it isn't running
- Starts Postgres and the Python embedding service (Docker Compose) and waits for both to be healthy
- Generates local-only JWT signing secrets on first run (dotnet User Secrets, never committed)
- Applies EF Core migrations
- Opens the API (http://localhost:5223) and the frontend (http://localhost:5173) each in its own PowerShell window

Or run the pieces manually:

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

### MCP server
See the mcp [README](mcp/README.md)   

### Python EmbeddingService
See the EmbeddingService [README](PlanIt.EmbeddingService/README.md)

## Stack

- **PlanIt.Api** — ASP.NET Core 10 Web API (backend, SignalR for real-time updates)
- **PlanIt.Web** — React 19 + TypeScript + Vite (mobile-first frontend)
- **PlanIt.Api.Tests** — xUnit tests for the API
- Database — PostgreSQL (via EF Core + Npgsql)
- Hosting — API + DB on Azure, frontend on GitHub Pages, Docker for the API and local dev

## Project status

**Backend and frontend are fully built and integrated.** Testing (`PlanIt.Api.Tests` has no real tests yet) and production DevOps/deployment workflows are not yet implemented. See `.claude/docs/plans/planit-master-plan.md` for the full plan and subplan breakdown.

## Known issues

- `PlanIt.Api` currently pulls a transitively vulnerable `Microsoft.OpenApi` 2.0.0 (`NU1903`, GHSA-v5pm-xwqc-g5wc) via `Microsoft.AspNetCore.OpenApi` 10.0.10 — the patched 3.x line of `Microsoft.OpenApi` is not yet compatible with that package's XML-comment source generator. Revisit when a compatible `Microsoft.AspNetCore.OpenApi` release ships.
