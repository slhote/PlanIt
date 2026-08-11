# Handoff: Build a Mocked-API Frontend for PlanIt

**Date:** 2026-08-10
**Branch:** `claude/frontend-mock-scaffold` (created from latest `main`)
**Purpose of this document:** brief a fresh Claude Code session (running locally, no memory of the
planning conversation that produced the docs below) so it can pick up this task with full context.

---

## What PlanIt is

Read `/CLAUDE.md` first — it's the project's own guidance file and covers stack, structure, and
current known gaps. Short version: PlanIt is a mobile-first task board (Project → Feature/Task
hierarchy, drag-and-drop status columns, real-time multi-user collaboration). Stack: ASP.NET Core
API + React 19/TypeScript/Vite frontend, Postgres, Azure + GitHub Pages hosting.

## What's actually been decided so far (read these, don't re-derive them)

Three planning subplans have been completed and merged to `main`, all under
`.claude/docs/plans/`:

1. **`planit-master-plan.md`** — the top-level plan: binding cross-cutting decisions (idempotency via
   client-generated GUIDs, auth model, cascade-delete rules, work-item hierarchy/routing shape), and
   the subplan breakdown/sequencing.
2. **`planit-system-design-architecture.md`** — DB engine (PostgreSQL), SignalR hub design
   (per-project groups, 7-event MVP set), CORS/auth-handshake design, and the Similar Tasks
   Suggestions groundwork (post-MVP feature, not needed for this task).
3. **`planit-persistence-data-model.md`** — the actual EF Core schema: `User`, `Project`,
   `ProjectMember` (creator gets an explicit `Owner` row), `WorkItem` (single-table Feature/Task
   discriminator, `Tags: text[]` column, `xmin`-backed optimistic concurrency), `RefreshToken`
   (rotation with reuse detection). **Read this one closely** — even though no backend exists yet,
   this schema is the best available source of truth for what fields a mocked API's DTOs should have
   (Title, Description, Status enum `ToDo`/`InProgress`/`Completed`, AssigneeId, Tags array, etc.).
4. **`planit-frontend-scaffolding.md`** — the actual task at hand. This is the primary document to
   work from. It decides:
   - **Routing**: React Router, 4 nested routes (`/project/:id`,
     `/project/:id/feature/:id`, `/project/:id/task/:id`,
     `/project/:id/feature/:id/task/:id`), work-item detail as a **dedicated route** (not a modal)
     with detail content built as its own component decoupled from page chrome.
   - **State management**: **TanStack Query** — for the mocked build, this means mock API functions
     that `useQuery`/`useMutation` call, not a hand-rolled cache.
   - **Drag-and-drop**: `dnd-kit` (chosen for native touch/pointer support — this is mobile-first).
   - **Auth token handling**: a module-level token store shape is specified — for the mock, this can
     be stubbed/simplified (see "What to mock" below), but keep the shape so it's not thrown away
     later.
   - **Landing flow**: return users auto-jump to their last-visited project via a `localStorage` key;
     first-time users see a project list with a "create your first project" empty state.
   - Proposed folder structure is in that doc's "Folder structure" section — follow it.
5. **`data-loading-architecture.md`** — has a superseded-but-still-conceptually-relevant note at the
   top pointing to doc #4 above for the actual (TanStack Query) implementation approach; the original
   content still describes the *shape* of what gets cached and when it gets invalidated.

**Not yet decided, and not needed to start this task:** the actual REST API contract (endpoint URLs,
exact DTO field casing, verbs) — that's subplan 3 (API Contracts & Backend), which hasn't happened.
Don't wait for it. **Building this mock is explicitly meant to inform that contract**, not the other
way around — see "Goal" below.

## A design skill exists — use it

`.claude/skills/mobile-app-ui-design/SKILL.md` (with `.claude/skills/mobile-app-ui-design/
references/industry-conventions.md`) is a design-guidance skill covering mobile UI/UX principles:
structure-first UX (thumb-zone placement, F-pattern layout), a visual design system (typography
limits, 60/30/10 color rule, 8-point spacing grid), emotional design (peak-end rule), and
industry-specific conventions. **Use this to actually inform how the board, task detail, and auth
screens look and feel** — not just how they're wired up. This isn't optional polish; the user
explicitly wants this skill applied.

## The goal

The user wants to **click through a working frontend against mocked data**, before the real backend
exists, specifically to:
1. Get a feel for how the pages connect (board → feature → task, navigation flow, auth screens).
2. **Discover missing functionality** — pages, states, or interactions the planning docs didn't
   anticipate. Three areas are explicitly flagged in `planit-frontend-scaffolding.md`'s scope as
   *not yet detailed at a page level* and should get fleshed out as part of building this:
   - Work-item CRUD forms (create/edit Feature or Task)
   - Assignee filter UI
   - Collaborator search/add UI
3. This mock effectively becomes the first informal draft of what the real API contract needs to
   support — so if a page needs data or an action that doesn't map cleanly to the Persistence schema,
   that's a useful finding to surface, not a blocker to route around silently.

## What to mock

Since there's no backend, build a mock API layer that TanStack Query's hooks call against instead of
real `fetch` calls — e.g. a `mockApi/` module returning `Promise`s with realistic delay, backed by an
in-memory dataset shaped like the Persistence doc's entities (a few Projects, each with some Features
and Tasks, a couple of Users, realistic Tags). This should be swappable for a real API client later
with minimal disruption — keep the mock behind the same function signatures a real `api/` module
would have (per the folder structure in `planit-frontend-scaffolding.md`).

Auth can be heavily simplified for the mock — a fake "logged in as [seeded user]" state is enough;
the real token-rotation/refresh-timer machinery doesn't need to actually function against a mock,
just don't build the UI in a way that assumes it away entirely (e.g. still have a login screen, even
if "logging in" just picks a seeded user).

SignalR/real-time can be skipped entirely for this pass — there's no live event source to mock
meaningfully yet. Note anywhere the UI would need to react to a live update (per the 7-event MVP set
in `planit-system-design-architecture.md`) as a placeholder/TODO rather than building it.

## Starting point

`PlanIt.Web/` is currently the unmodified `npm create vite` React 19 + TS scaffold — only
`react`/`react-dom` installed, no router/state/drag-and-drop libraries, no `src/` subfolders beyond
the default `App.tsx`/`main.tsx`. Confirmed via direct inspection during the planning session, should
still be accurate. Install the dependencies named in `planit-frontend-scaffolding.md`'s "Dependencies
to add" section (`react-router`, `@dnd-kit/core` + `@dnd-kit/sortable`, `@tanstack/react-query`) —
skip `@microsoft/signalr` for this mocked pass, per "What to mock" above.

## Verification

There's no automated test target for this yet — "done" for this pass means: the app runs locally
(`npm run dev`), a user can log in (mocked), land on their last/only project (or a project list if
none), see a board with drag-and-drop working, open a task/feature via its dedicated route, and the
three flagged-as-undesigned areas (work-item CRUD, assignee filter, collaborator search/add) have
*something* built for them — even a rough first pass is the point, since the goal is discovering gaps,
not shipping a polished product yet.
