# CLAUDE.md

This file provides guidance to Claude Code when working with code in `PlanIt.Web`. See the [repo-root CLAUDE.md](../CLAUDE.md) for backend/domain context. The architecture decisions referenced throughout this file come from four subplans under `.claude/docs/plans/`, in order of authority (later ones supersede earlier ones where they overlap):

1. [`planit-master-plan.md`](../.claude/docs/plans/planit-master-plan.md) — top-level binding decisions.
2. [`planit-system-design-architecture.md`](../.claude/docs/plans/planit-system-design-architecture.md) — auth/token-refresh model, SignalR event set, CORS.
3. [`planit-frontend-scaffolding.md`](../.claude/docs/plans/planit-frontend-scaffolding.md) — routing, state management (TanStack Query), drag-and-drop, auth token handling, landing flow, GitHub Pages SPA fallback — **all confirmed decisions**, not open questions.
4. [`frontend-mock-build-handoff.md`](../.claude/docs/plans/frontend-mock-build-handoff.md) — the handoff doc that actually produced the current mock build described below.

**Note:** the repo-root `CLAUDE.md`'s "Known Gaps" section currently says the frontend is "a bare Vite template... no routing, pages, or API integration yet." That's stale — as described below, the app now has full routing, auth scaffolding, and CRUD pages against a mocked API layer. Flag/update that line if you're touching the root doc.

## Project Overview

**PlanIt.Web** is the mobile-first React client for the PlanIt task board. It is a plain Node/npm project — **not** part of `PlanIt.slnx` (the .NET solution) — and builds/runs independently via `npm`. It now talks to the **real `PlanIt.Api` backend** via a real fetch client (`src/api/httpClient.ts`), with SignalR live updates wired through `src/realtime/`. The old in-memory mock layer (`mockClient.ts`, `seedData.ts`) has been replaced — `src/api/*.ts` now call `apiFetch()` against `VITE_API_BASE_URL`.

## Stack

- **Vite** (v8) — dev server + build tooling. `vite.config.ts` is bare-bones: just the `react()` plugin, no aliasing, no proxy config.
- **React 19** + **TypeScript** (~6.0, `target: es2023`, `moduleResolution: bundler`)
- **oxlint** — linting (Rust-based; not ESLint). Config in `.oxlintrc.json`: `react`, `typescript`, `oxc` plugins, `react/rules-of-hooks: error`, `react/only-export-components: warn`.
- **React Router** (`react-router`, v8 — note the package name, not `react-router-dom`; this is the merged v7+ package)
- **TanStack Query** (`@tanstack/react-query`) — server-state caching, wraps the mock API calls in `src/hooks/`
- **`@dnd-kit`** (`core`/`sortable`/`utilities`) — drag-and-drop for the board, per the master plan's confirmed choice (`react-beautiful-dnd` is unmaintained)
- **Plain CSS with custom properties** — see Styling below. No Tailwind, no CSS Modules, no styled-components/emotion.
- **`@playwright/test`, `pixelmatch`, `pngjs`** — devDependencies powering `scripts/screenshot-ui.mjs`, the UI-verification tool (see the [visually-inspect-ui skill](../.claude/skills/visually-inspect-ui/SKILL.md)). **Not a test runner** — there is currently no Vitest/Jest, no `.test`/`.spec` files, and no `playwright.config.ts` anywhere in this project. Don't assume test infrastructure exists; if asked to add tests, that's greenfield work (see master plan subplan 6 for the intended overall testing strategy).

## Common Commands

Run from within `PlanIt.Web/`:

```bash
npm install       # install dependencies
npm run dev       # start dev server (http://localhost:5173)
npm run build     # type-check (tsc -b) and build to dist/
npm run lint      # run oxlint
npm run preview   # serve the production build locally
```

## Directory Structure

```
src/
  App.tsx, main.tsx
  api/            — real fetch-based API layer (see below)
  assets/
  auth/           — real JWT auth (see below)
  components/     — AppShell, Modal, TagInput, WorkItemCard, icons, initials helper
  features/
    projects/     — Board (dnd-kit), ProjectListPage, ProjectBoardPage, CreateProjectModal,
                     CollaboratorsModal, LandingRedirect, lastProject.ts (localStorage helper)
    workitems/    — WorkItemForm (shared create/edit), CreateWorkItemModal, WorkItemDetailPage,
                     WorkItemDetailContent
  hooks/          — mutations.ts, queries.ts (TanStack Query wrappers over api/)
  realtime/       — signalrClient.ts, connectionId.ts, useProjectRealtime.ts (SignalR live updates)
  router/         — AppRoutes.tsx
  styles/         — theme.css (single global stylesheet)
  types/          — domain.ts (shared TS types/DTOs)
```

## Routing (`src/router/AppRoutes.tsx`)

Matches the master plan's hierarchy decision exactly:

- `/login` — `LoginPage` (public)
- Everything else behind `RequireAuth` → `AppShell` (renders `<Outlet/>`):
  - `/` — `LandingRedirect` (redirects to the last-visited project board via `localStorage`, falls back to `/projects`)
  - `/projects` — `ProjectListPage`
  - `/project/:projectId` — `ProjectBoardPage`
  - `/project/:projectId/feature/:featureId` — `WorkItemDetailPage kind="feature"`
  - `/project/:projectId/task/:taskId` — `WorkItemDetailPage kind="task"`
  - `/project/:projectId/feature/:featureId/task/:taskId` — `WorkItemDetailPage kind="task"` (nested)
  - `*` — inline `NotFound`

**GitHub Pages SPA fallback is decided, not yet implemented**: the `404.html` redirect trick (`rafgraph/spa-github-pages` pattern) — a `deploy/404.html` encodes the attempted deep-link path into a query string and redirects to `index.html`, which decodes it and calls `history.replaceState()` before React Router mounts. Chosen over hash-based routing for clean path-based URLs. **Not yet built** — no `404.html`, no restore logic in `main.tsx`, and `vite.config.ts` still needs its `base` set to the GitHub Pages subpath (e.g. `'/PlanIt/'`). These land in the DevOps/Hosting subplan — see `planit-devops-hosting.md`.

## The API Layer (`src/api/`)

**This is a real fetch-based client** targeting `VITE_API_BASE_URL`. `httpClient.ts` is the transport — `apiFetch<T>()` sets `Authorization: Bearer`, `Content-Type: application/json`, and the `X-SignalR-Connection-Id` header (so the hub can exclude the originating client from its broadcast). A 401 on an authenticated call clears the session and lets `RequireAuth` redirect to `/login`.

- **`httpClient.ts`** — `apiFetch<T>(path, options)` with `skipAuth` flag for register/login.
- **`auth.ts`** — `register`, `login`, `refreshToken`, `logout` against real JWT endpoints.
- **`projects.ts`** — `fetchProjects`, `fetchProjectBoard`, `createProject`.
- **`projectMembers.ts`** — `fetchProjectMembers`, `addProjectMember`, `removeProjectMember`.
- **`users.ts`** — `fetchUsers`, `fetchUser`, `searchUsers`.
- **`workItems.ts`** — `fetchWorkItem`, `fetchFeature`, `createWorkItem`, `updateWorkItem`, `countCascadeDeletions`, `reorderWorkItems`, `deleteWorkItem` (returns `{ deletedIds }`).

The function signatures here are the contract shape the backend satisfies — don't change them without a corresponding API change.

## Types (`src/types/domain.ts`)

`Guid = string`, `User`, `Project`, `ProjectMember` / `ProjectMemberRole` (`Owner`|`Member`), `WorkItemType` (`Feature`|`Task`), `WorkItemStatus` (`ToDo`|`InProgress`|`Completed`, plus a `WORK_ITEM_STATUSES` array), `WorkItem` (single-table with `workItemType` discriminator, nullable `parentId`/`assigneeId` — matches the intended real schema shape), `MAX_TAGS_PER_WORK_ITEM = 3`. Tags are `string[]` directly on `WorkItem` — this already matches the decided backend schema (a native Postgres `text[]` column, per-project scoped, `CHECK (cardinality(tags) <= 3)`, no separate Tag entity/junction table; see [`PlanIt.Api/CLAUDE.md`](../PlanIt.Api/CLAUDE.md)), not a placeholder awaiting a future redesign.

## Auth (`src/auth/`)

Real JWT auth — not a mock:

- **`authStore.ts`** — plain module-level singleton (not React Context, so the SignalR `accessTokenFactory` can read the token synchronously outside the component tree). Holds `accessToken`/`currentUser` in memory; `localStorage` persists only the refresh token and last user id for auto-resume. Proactive refresh timer fires at ~80% of the 15-minute TTL.
- **`useAuth.ts`** — `useSyncExternalStore` hook over the singleton.
- **`RequireAuth.tsx`** — redirects to `/login` if unauthenticated.
- **`LoginPage.tsx`** — real username/password form against `POST /auth/login`.

## Real-time (`src/realtime/`)

- **`signalrClient.ts`** — creates and manages the SignalR `HubConnection`, delivering `access_token` via query-string `accessTokenFactory`.
- **`connectionId.ts`** — exposes the current connection ID so `httpClient.ts` can set the `X-SignalR-Connection-Id` header on mutations, enabling `GroupExcept` on the server.
- **`useProjectRealtime.ts`** — React hook that joins a project group on mount and wires the 7 hub events to TanStack Query cache invalidations.

## Styling (`src/styles/theme.css`)

Plain hand-written CSS using **CSS custom properties** — not Tailwind, not CSS Modules, not styled-components/emotion. One global stylesheet, imported once in `main.tsx`. Documents its own system in comments: a 60/30/10 color system, one font family with 4 sizes/2 weights, an 8-point spacing grid, radius/shadow scales. Utility-ish class names (`.stack`, `.row`, `.row-between`, `.card`, `.btn.btn-primary`, `.field`, `.empty-state`, etc.) are applied directly via `className`; inline `style={{ ... }}` is used ad hoc for one-off spacing, referencing the same CSS vars (`var(--space-4)`). New component styling should follow this existing system rather than introducing a second styling approach.

## Root Wiring

- **`main.tsx`** — creates a `QueryClient` (`retry: 1`, no refetch-on-focus), wraps `<App/>` in `QueryClientProvider` + `BrowserRouter` + `StrictMode`, imports `theme.css` globally.
- **`App.tsx`** — on mount, checks for a remembered user id; if present, silently re-establishes a session (`fetchUser` then `mockLogin`) for the auto-resume UX, shows a spinner until bootstrapped, then renders `<AppRoutes/>`.

## UI Verification

Any front-end change (new component, layout change, styling change, new route) should be verified with the [visually-inspect-ui skill](../.claude/skills/visually-inspect-ui/SKILL.md) before being reported done — never the Browser pane's screenshot action. See that skill file for the full render → capture → inspect → fix → deliver loop and the reusable `scripts/screenshot-ui.mjs` tool.
