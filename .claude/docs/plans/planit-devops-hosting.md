# DevOps / Hosting Subplan

**Date:** 2026-08-13  
**Status:** Plan — not yet implemented  
**Scope:** Get PlanIt running in production: Azure API + DB, GitHub Pages frontend, CD pipelines

## Context

Both the API and frontend are fully built and integrated. The Dockerfile is production-ready. CI (build/test) workflows exist. What's entirely missing: CD, Azure resource provisioning, and GitHub Pages deploy configuration. This subplan covers the remaining DevOps work to go from "runs locally" to "runs in production."

---

## What already exists

- `PlanIt.Api/Dockerfile` — multi-stage, SDK:10.0 → aspnet:10.0, exposes port 8080, `ASPNETCORE_URLS=http://+:8080`. Production-ready as-is.
- `docker-compose.yml` — runs Postgres only (the API is explicitly noted as "DevOps/Hosting subplan's job"). No compose-based full-stack local integration yet.
- `.github/workflows/backend-ci.yml` — dotnet restore/build/test on push/PR to `main`. No deploy step.
- `.github/workflows/frontend-ci.yml` — npm ci / lint / build on push/PR. No deploy step.
- `appsettings.json` — `Cors:AllowedOrigins: ["https://slhote.github.io"]`, `ConnectionStrings:DefaultConnection: "[replaced by env var in deployed]"`. Already production-shaped.
- `appsettings.Development.json` — `Cors:AllowedOrigins: ["http://localhost:5173"]`.

---

## Decisions

### 1. Azure hosting: Container Apps (not App Service)

**Azure Container Apps** over App Service. Reasoning:
- Scale-to-zero on consumption plan — lowest cost for a portfolio project with sporadic traffic.
- Native container deployment model — matches the Dockerfile, no extra "web app for containers" config.
- SignalR sticky sessions: Container Apps supports session affinity headers out of the box; adequate for a single-revision deployment (no backplane needed).
- App Service is slightly simpler to provision but charges for idle time on a Basic/Standard tier; Container Apps free tier covers low-traffic usage.

**Database:** Azure Database for PostgreSQL Flexible Server, **Burstable B1ms** (cheapest tier — 1 vCore, 2 GiB RAM). Fine for portfolio-scale traffic.

### 2. Container registry: GitHub Container Registry (GHCR)

No new Azure resource required. Images pushed to `ghcr.io/slhote/planit-api` and pulled by Container Apps. GitHub Actions already has read access to GHCR within the same repo context.

### 3. GitHub → Azure auth: OIDC federated credentials

No long-lived secrets stored in GitHub. An Azure service principal with federated credentials trusts GitHub's OIDC token for the `slhote/PlanIt` repo on `main`. This eliminates the `AZURE_CREDENTIALS` JSON secret rotation problem.

Required GitHub secrets (one-time setup, no rotation):
- `AZURE_CLIENT_ID` — service principal application (client) ID
- `AZURE_TENANT_ID` — Azure tenant ID  
- `AZURE_SUBSCRIPTION_ID` — Azure subscription ID

App secrets stored as Container Apps secrets (not GitHub secrets):
- `ConnectionStrings__DefaultConnection` — Postgres connection string
- `Jwt__SigningKey` — JWT HS256 signing key

### 4. IaC: Bicep

One small Bicep file (`infra/main.bicep`) provisioning:
- Azure Container Apps Environment
- Azure Container App (PlanIt.Api), pulling from GHCR
- Azure Database for PostgreSQL Flexible Server (Burstable B1ms)

No azd/azure.yaml wrapper — the Bicep file is straightforward enough to run directly with `az deployment group create`. azd adds tooling overhead not worth the complexity here.

**Key Bicep parameters:** `containerImage` (the GHCR tag), `dbAdminPassword`, `jwtSigningKey`. DB password and JWT key never go in source control — passed as secure parameters at deploy time or stored as Key Vault references once Key Vault is added (post-MVP).

### 5. GitHub Pages frontend deploy

GitHub Pages project site at `https://slhote.github.io/PlanIt/`. This determines `vite.config.ts`'s `base` value.

Frontend deploy triggers when `PlanIt.Web/**` or the deploy workflow changes on `main`.

---

## Build sequence

### Step 1: IaC — provision Azure resources

Write `infra/main.bicep` with:
- Container Apps Environment (Consumption)
- Container App: image from GHCR, env vars `ConnectionStrings__DefaultConnection` and `Jwt__SigningKey` from Container Apps secrets, port 8080, ingress external, session affinity enabled
- PostgreSQL Flexible Server: Burstable B1ms, `PlanIt` database, firewall rule allowing Azure services

Run once manually to provision (or add a one-off GitHub Actions workflow for it):
```
az deployment group create --resource-group planit-rg --template-file infra/main.bicep --parameters ...
```

### Step 2: Run database migrations on first deploy

The Dockerfile's entrypoint runs the app, not migrations. Two options:
- **Preferred:** Add a startup migration in `Program.cs` behind an env var (`APPLY_MIGRATIONS=true`) — runs `dbContext.Database.MigrateAsync()` before the app starts. Set this env var in the Container App's secrets/env for the first deploy, then remove it. Safe because the API is single-instance and migrations are idempotent.
- Alternative: a one-shot migration job/init container (more complex, not worth it here).

### Step 3: GHCR package visibility

The `ghcr.io/slhote/planit-api` package must be set to **public** (or the Container App must be given a GHCR pull secret) so Azure can pull it without a registry credential. Easiest option: public package. If privacy matters, add a Container App registry credential with a GitHub PAT (read:packages scope).

### Step 4: GitHub Actions — backend CD workflow

New file `.github/workflows/backend-deploy.yml`:

Trigger: push to `main`, paths `PlanIt.Api/**` or `PlanIt.Api.Tests/**` or `Dockerfile` (in addition to the existing CI trigger — or merge the two).

Steps:
1. `actions/checkout@v4`
2. OIDC login — `azure/login@v2` with `client-id`, `tenant-id`, `subscription-id`
3. Log in to GHCR — `docker/login-action@v3` with `registry: ghcr.io`, username `${{ github.actor }}`, password `${{ secrets.GITHUB_TOKEN }}`
4. Build and push Docker image — `docker/build-push-action@v6`, tag with `ghcr.io/slhote/planit-api:${{ github.sha }}` and `:latest`
5. Deploy to Container Apps — `azure/container-apps-deploy-action@v2` (or `az containerapp update --image ...`)

The existing `backend-ci.yml` (dotnet build/test) should still run on PRs. CD only runs on push to `main` after CI passes — either gate it via `needs:` referencing the CI job, or split into separate files with the deploy job depending on the CI job.

### Step 5: GitHub Pages — vite.config.ts base path

Set `base: '/PlanIt/'` in `PlanIt.Web/vite.config.ts`. This is required for all asset paths to resolve correctly under the project-site subpath.

```ts
// vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/PlanIt/',
})
```

Also set `VITE_API_BASE_URL` for the production build — this should point to the Azure Container App's ingress URL (e.g. `https://planit-api.{region}.azurecontainerapps.io`). Set it as a GitHub Actions environment variable in the deploy workflow (not a secret — it's a public URL).

### Step 6: GitHub Pages — SPA 404 fallback

Create `PlanIt.Web/public/404.html` with the `rafgraph/spa-github-pages` redirect script (encodes the path + query into a session storage entry, redirects to `/PlanIt/`). Add the complementary decode snippet to `PlanIt.Web/src/main.tsx` before React Router mounts, calling `history.replaceState()`.

This enables direct navigation to deep links like `https://slhote.github.io/PlanIt/project/{id}`.

### Step 7: GitHub Actions — frontend CD workflow

New file `.github/workflows/frontend-deploy.yml`:

Trigger: push to `main`, paths `PlanIt.Web/**`.

Steps:
1. `actions/checkout@v4`
2. Setup Node 22, `npm ci`
3. Build — `npm run build` with `VITE_API_BASE_URL` set
4. `actions/upload-pages-artifact@v3` — upload `PlanIt.Web/dist`
5. `actions/deploy-pages@v4` — deploy to GitHub Pages

Requires enabling GitHub Pages in the repo settings (Source: GitHub Actions, not a branch).

### Step 8: Add API service to docker-compose.yml

Add a `planit-api` service to `docker-compose.yml` for local full-stack integration testing:

```yaml
planit-api:
  build:
    context: .
    dockerfile: PlanIt.Api/Dockerfile
  ports:
    - "8080:8080"
  environment:
    - ConnectionStrings__DefaultConnection=Host=planit-postgres;Database=PlanIt;Username=postgres;Password=postgres
    - Jwt__SigningKey=${JWT_SIGNING_KEY}
    - Cors__AllowedOrigins__0=http://localhost:5173
    - ASPNETCORE_ENVIRONMENT=Development
  depends_on:
    planit-postgres:
      condition: service_healthy
```

Update `dev.ps1` to support an `--compose` flag that brings up both services together, as an alternative to the current native `dotnet run` flow.

---

## Environment variables reference

| Name | Where | Value |
|------|-------|-------|
| `ConnectionStrings__DefaultConnection` | Container App secret | Full Npgsql connection string to Azure Postgres |
| `Jwt__SigningKey` | Container App secret | HS256 signing key (32+ bytes, base64 or random string) |
| `Cors__AllowedOrigins__0` | Container App env var | `https://slhote.github.io` |
| `Jwt__Issuer` | Container App env var | `PlanIt` (matches appsettings.json) |
| `Jwt__Audience` | Container App env var | `PlanIt` |
| `VITE_API_BASE_URL` | GitHub Actions env (build-time) | Azure Container App ingress URL |
| `APPLY_MIGRATIONS` | Container App env var (first deploy only) | `true` |

---

## Verification

1. **Provision:** `az deployment group create` completes without error; Container App and Postgres visible in Azure portal.
2. **API health:** `curl https://{container-app-url}/health` returns `200 Healthy`.
3. **Frontend:** `https://slhote.github.io/PlanIt/` loads the login page. Deep link (e.g. `/PlanIt/projects`) works after a browser refresh (SPA fallback working).
4. **Auth round-trip:** Register and login via the deployed frontend; JWT refresh fires at ~80% TTL without a page reload.
5. **SignalR:** Open the same project board in two browser tabs; creating a work item in one tab appears in the other within ~1 second.
6. **CORS:** No CORS errors in the browser console when the frontend calls the API.
