# Frontend Scaffolding Subplan

**Date:** 2026-08-11
**Status:** Design Decision (Phase 1 — parallelizable once Phase 0 landed)
**Scope:** Routing, state management, drag-and-drop, auth token handling, and landing-flow
architecture for `PlanIt.Web`

## Context

This subplan finalizes the frontend architecture for `PlanIt.Web`, informed by
[`planit-system-design-architecture.md`](planit-system-design-architecture.md) (auth model, CORS/
SignalR handshake, MVP event set) and [`data-loading-architecture.md`](data-loading-architecture.md)
(lazy-load/cache/invalidation concepts, since revised — see the State Management section below).

**Starting point:** `PlanIt.Web` is the unmodified `npm create vite` React 19 + TypeScript scaffold.
`package.json` has only `react`/`react-dom`; no router, drag-and-drop library, state-management
library, or SignalR client. No `router`/`store`/`pages`/`components`/`hooks`/`features`/`api`
folders exist. This subplan designs the architecture; actual scaffolding code is written once this
doc is implemented.

---

## Dependencies to add

```
react-router          — routing
@dnd-kit/core          — drag-and-drop (+ @dnd-kit/sortable for column/card ordering)
@tanstack/react-query   — server-state caching/invalidation
@microsoft/signalr        — SignalR client
```

No Redux Toolkit, Zustand, or other general client-state library — see State Management below for
why TanStack Query covers the actual problem without one.

---

## Routing

**Library: React Router.** The standard default for React SPA routing; no competing option
justified the switch for this project's scope.

**Route shapes** (from the master plan, binding):
```
/project/:projectId
/project/:projectId/feature/:featureId
/project/:projectId/task/:taskId
/project/:projectId/feature/:featureId/task/:taskId
```

**Work-item detail view: dedicated route, not a modal, for MVP.** Opening a Feature or Task
navigates to its own route rather than rendering as an overlay on the board. This is simpler and
uniform across mobile/desktop — on mobile, a modal typically renders as a full-screen sheet anyway,
so the "board visible behind it" benefit a modal offers on desktop mostly evaporates on this
project's primary target form factor.

**Design guideline (bake in now, not optional):** the actual detail content — form fields, edit
controls, tag input, assignee picker — must be its own component, decoupled from page-level chrome
(header, back button, layout wrapper). Concretely:

```
WorkItemDetailContent.tsx   — the actual form/edit UI, no route/page assumptions
WorkItemDetailPage.tsx      — thin wrapper: page chrome + <WorkItemDetailContent />
```

This keeps a future migration to a route-driven modal (React Router's background-location pattern —
render the board using a stored "background" location, the modal reads the real location) a small,
contained addition rather than a rewrite: `WorkItemDetailContent` is reused as-is inside a `<Modal>`
wrapper, and `WorkItemDetailPage` becomes the fallback for a cold-loaded deep link with no board to
show behind it.

### GitHub Pages SPA fallback: the `404.html` redirect trick

GitHub Pages is a static file server with no server-side rewrite rules — a hard refresh or shared
link on `/project/{id}/feature/{id}` 404s by default, since GitHub Pages has no way to know that
path should resolve to the SPA's `index.html`.

**Decision: the `404.html` redirect trick** (the well-known `rafgraph/spa-github-pages` community
pattern), not hash-based routing. Mechanism:

```
1. deploy/404.html: a small script encodes the attempted path into a query string,
   then redirects to /index.html?redirected=/project/abc/feature/xyz
2. index.html (or an early script in main.tsx): on load, checks for that query
   string, decodes it, and calls history.replaceState() to restore the real path
   before React Router mounts
3. React Router then renders normally, as if the direct navigation had worked
```

Chosen over hash-based routing (`/#/project/{id}/feature/{id}`) for clean, path-based URLs matching
how the master plan's route notation is written — a portfolio-project polish consideration. Cost:
one extra redirect hop, visible only on a cold load of a deep link (never during normal in-app
navigation, since React Router's client-side navigation never touches the server).

**Vite config implication:** `vite.config.ts` needs `base` set to match the GitHub Pages subpath
(e.g. `base: '/PlanIt/'` if served from `slhote.github.io/PlanIt/`) — flagged here for awareness,
but the concrete value and the actual GitHub Actions deploy workflow are DevOps/Hosting subplan's
job (subplan 5), not decided in this document.

---

## State management: TanStack Query

**Decision: TanStack Query, not plain React hooks/Context, and not a general client-state library
(Zustand/Redux Toolkit).**

The actual problem `data-loading-architecture.md` describes — lazy-loaded Features, a client cache,
SignalR-triggered invalidation — is a **server-state caching problem**, which TanStack Query is
purpose-built for. Using it means less custom code for the same design, not more:

```
Board mounts
  → useQuery(['project', projectId], fetchProject)
      — TanStack Query handles caching, staleness, request dedup automatically

User opens a Feature
  → useQuery(['feature', featureId], fetchFeature, { enabled: isOpen })
      — lazy: only fetches when the query actually runs

SignalR receives WorkItemUpdated { workItemId, projectId }
  → queryClient.invalidateQueries(['feature', affectedFeatureId])
      — next read of that query refetches; TanStack Query handles the
        "stale until re-opened" UX (matches the already-approved
        eventual-consistency/re-fetch-on-reopen pattern) natively

SignalR receives a structural event (WorkItemCreated/Deleted/StatusChanged/Moved)
  → queryClient.setQueryData(['project', projectId], updater)
      — direct cache update for board-visible changes that need to render
        immediately, without waiting for a re-fetch round-trip
```

This directly maps onto the System Design subplan's structural-vs-content-only event split: content-
only events (`WorkItemUpdated`) call `invalidateQueries` (lazy re-fetch later); structural events
(`WorkItemCreated`/`Deleted`/`StatusChanged`/`Moved`) call `setQueryData` for an immediate UI update,
since those can't wait for the next re-open.

**Reconciliation with `data-loading-architecture.md`:** that document's specific hook names
(`useProjectData.ts`, `useSignalR.ts`) and cache types (`CachedFeature`, `ProjectState`) are
superseded by this decision — the *concepts* (lazy load, cache, SignalR-triggered invalidation) carry
over directly, only the implementation mechanism changes from hand-rolled hooks to TanStack Query's
built-in cache. A cross-reference note has been added to that document (see below).

---

## Drag-and-drop: dnd-kit

**Decision: `@dnd-kit/core` (+ `@dnd-kit/sortable` for within-column/cross-column ordering).**

Deciding factor: mobile-first is a hard project requirement, and `dnd-kit` ships `PointerSensor`/
`TouchSensor` support natively, so drag works identically on touch and mouse with minimal setup.
Rejected `react-dnd` (defaults to the HTML5 drag API, which has poor/no native touch support without
bolting on a separate backend) and Atlassian's `pragmatic-drag-and-drop` (good, but framework-
agnostic — no React-specific hooks abstraction, meaning more manual integration work).

**Write path:** a drag-and-drop status change calls the same REST `PATCH` endpoint as any other
status edit (per System Design's request/write-flow decision — SignalR is broadcast-only, never a
write path). `dnd-kit`'s `onDragEnd` handler triggers an optimistic `setQueryData` update (card moves
immediately, no waiting for the server round-trip) followed by the actual `PATCH` call; a failure
response reverts the optimistic update and surfaces an error.

---

## Auth token handling

Per System Design's decision (proactive/timer-based refresh, not reactive to REST 401s or SignalR
reconnects):

```
authStore (a plain module-level singleton, not Context — needs to be readable
outside of React components, since both the fetch wrapper and the SignalR
accessTokenFactory need synchronous access to "the current token"):

  let accessToken: string | null = null;
  let refreshTimer: ReturnType<typeof setTimeout> | null = null;

  function setAccessToken(token: string, expiresInSeconds: number) {
    accessToken = token;
    if (refreshTimer) clearTimeout(refreshTimer);
    refreshTimer = setTimeout(refreshAccessToken, expiresInSeconds * 0.8 * 1000);
  }

  function getAccessToken(): string | null { return accessToken; }

  async function refreshAccessToken() {
    // POST /auth/refresh using the refresh token from localStorage
    // on success: setAccessToken(newToken, newExpiry)
    // on failure (revoked/expired): clear state, redirect to login
  }
```

- The REST fetch wrapper reads `getAccessToken()` for the `Authorization: Bearer` header.
- SignalR's `accessTokenFactory: () => getAccessToken()` reads the same value — one source of truth,
  no SignalR-specific refresh logic needed.
- The refresh token itself lives in `localStorage` (per the master plan's binding auth model),
  read only by `refreshAccessToken()`.

---

## Landing flow: first-time vs. return user

**Decision:** a separate `localStorage` key (distinct from the refresh token), `planit:lastProjectId`,
records the most recently visited project.

```
On login / app load:
  1. Validate/refresh the session (via the token handling above).
  2. If planit:lastProjectId is set:
       a. Attempt to navigate to /project/{lastProjectId}.
       b. If the project fetch 404s (deleted, or no longer a member) →
          clear the stale key, fall back to the project list.
  3. If no remembered project (first-time user, or the fallback above triggered) →
       render the project list, with a "create your first project" empty state
       if the user has none.

On navigating into any project (not just on login):
  → update planit:lastProjectId, so the "last visited" always reflects
    the most recent session, not just the first one.
```

This confirms and makes concrete the intent already implied by `data-loading-architecture.md`'s
stated rationale for storing the refresh token in `localStorage` ("needed to survive tab/browser
close for 'return to last project' UX").

---

## Folder structure (proposed)

```
PlanIt.Web/src/
  api/            — fetch wrapper, typed request/response functions per resource
  auth/           — authStore, refresh logic, login/logout screens
  components/     — shared, presentational components (Board, Card, TagInput, etc.)
  features/
    projects/     — project list, project board page
    workitems/    — WorkItemDetailContent, WorkItemDetailPage, create/edit forms
  hooks/          — TanStack Query hooks (useProject, useFeature, useSignalRConnection)
  router/         — route definitions, the 404.html-redirect restore logic
  types/          — shared domain types (mirrors the API's DTOs)
```

---

## Verification

This is a design decision document — no code exists yet. Verification happens when this subplan is
implemented:

1. `npm install` the dependencies listed above; `npm run build` succeeds with the new `base` path
   set in `vite.config.ts`.
2. A manual deep-link test against a GitHub Pages preview: hard-refresh on
   `/project/{id}/feature/{id}` restores the correct route via the `404.html` trick, not a blank
   404 page.
3. TanStack Query DevTools confirm cache entries are created/invalidated correctly when a SignalR
   event fires (manually trigger an event from two browser tabs, per the data-loading doc's existing
   manual test plan).
4. `dnd-kit` drag works on both a touch-emulated mobile viewport and desktop mouse input in the
   browser dev tools.
5. A round-trip auth test: log in, confirm the access-token refresh timer fires proactively before
   expiry with no REST calls in flight (simulate by leaving a SignalR-only view open and idle).
6. Landing-flow test: log in as a return user → lands on last project; manually delete that project
   from another session → next login falls back to the project list, not an error page.
