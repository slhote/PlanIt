# Data Loading Architecture: Project → Features → Tasks

**Date:** 2026-08-08  
**Status:** Design Decision  
**Scope:** Core data-fetching strategy for project board view

## Context

PlanIt is designed for multi-user real-time collaboration on project hierarchies (Project → Features → Tasks). Users frequently revisit features while working on other items, and real-time updates must be reflected across all connected clients. We need a data-loading strategy that:

- Works well with small projects today (0-100 items, fully loadable in one request)
- Scales to large projects in the future (100+ items) without architecture changes
- Minimizes latency and API load through intelligent caching
- Handles real-time invalidation when other users make changes
- Keeps UX predictable and transparent (users know when data is stale/updating)

## Decision: Lazy-Load Features + Client Cache + SignalR Invalidation

### Data Loading Flow

1. **Project view opens:**
   - Fetch project metadata + shallow feature list (id, name, order, description only)
   - Fetch all top-level tasks by projectId (tasks not in any feature)
   - Display board immediately with populated top-level tasks

2. **User opens a feature:**
   - Check client cache for feature details + its tasks
   - **If cached AND not invalidated:** Display instantly from cache
   - **If cache miss or invalidated:** Fetch feature details + tasks by featureId, store in cache
   - Cache marked as invalidated by SignalR when anyone changes that feature/its tasks

3. **User closes feature:**
   - Leave data in cache (revisit pattern: users frequently reopen features)

4. **Another user updates a task:**
   - SignalR broadcasts change event: `{ type: "task-updated", taskId, featureId?, projectId }`
   - Receiving clients mark affected feature's cache as invalidated
   - **Phase 1:** User sees stale data; next time they open that feature it re-fetches (Option A: lazy re-fetch)
   - **Phase 2 (future):** Auto-refresh in background + visual indicators (Option C with visibility)

### Why This Approach

| Scenario | Benefit |
|----------|---------|
| Small projects (now) | Add `?includeAll=true` to eager-load entire project in one call; cache stays valid longer |
| Large projects (future) | Lazy loading already baked in; pagination-ready endpoints; no architecture change needed |
| Revisiting features | Client cache + SignalR invalidation = instant access for valid data, automatic refresh when stale |
| Multi-user real-time | SignalR ensures cache stays consistent; no silent stale-data surprises |
| Mobile / poor connectivity | Client cache reduces requests; Service Worker / IndexedDB can persist cache across sessions (Phase 2+) |

## API Design (Scalability Valve)

These endpoints support both small eager-load and large lazy-load scenarios:

```
GET /api/projects/{id}
  ?includeFeatures=true
  &includeTasksByProject=true
  &includeTasksByFeature=false
  Response: { id, name, features: [...], tasksByProject: [...] }

GET /api/features/{id}
  ?includeTasks=true
  Response: { id, name, description, projectId, tasks: [...] }

GET /api/tasks
  ?projectId={id} | ?featureId={id}
  &pageSize=50
  &pageNumber=0
  Response: { items: [...], totalCount, hasNextPage }
```

**Logic:**
- Small projects: Client calls GetProject with `includeAll=true` → entire dataset in one request
- Large projects: Client lazy-loads features one at a time via GetFeature
- No code change needed; just flip the query param or skip eager load if item count > threshold

## Frontend State Management

> **Note:** The hand-rolled hooks/types below (`CachedFeature`, `ProjectState`,
> `useProjectData.ts`, `useSignalR.ts`) are superseded by
> [`planit-frontend-scaffolding.md`](planit-frontend-scaffolding.md)'s decision to use **TanStack
> Query** instead of plain hooks/Context for state management — it's purpose-built for exactly this
> server-state-caching-with-event-driven-invalidation problem, and needs less custom code for the
> same design. The *concepts* below (lazy load, cache, `invalidated` flag set by SignalR, re-fetch on
> next open) all carry over directly onto TanStack Query's `useQuery`/`invalidateQueries`/
> `setQueryData` model — see that document's "State management: TanStack Query" section for the
> concrete mapping. Only the implementation mechanism changes; nothing here is factually wrong, just
> superseded.

```typescript
type CachedFeature = {
  data: {
    id: string;
    name: string;
    description: string;
    projectId: string;
    tasks: Task[];
  };
  loadedAt: number;
  invalidated: boolean;  // Set to true by SignalR → triggers re-fetch on next open
};

type ProjectState = {
  projectId: string;
  features: Feature[];  // Shallow: id, name, order, description
  tasksByProject: Task[];  // Top-level tasks
  cachedFeatures: Map<string, CachedFeature>;
  loading: {
    projectOverview: boolean;
    feature: Record<string, boolean>;  // Track which features are currently loading
  };
};
```

## SignalR Integration

> **Note:** The event names below (`TaskUpdated`, `FeatureUpdated`) are illustrative. The finalized
> MVP event set — 7 typed events split into structural (board updates immediately: `WorkItemCreated`,
> `WorkItemDeleted`, `WorkItemStatusChanged`, `WorkItemMoved`) vs. content-only (cache-invalidation
> ping: `WorkItemUpdated`) vs. membership (`ProjectMemberAdded`/`Removed`) — is defined in
> [`planit-system-design-architecture.md`](planit-system-design-architecture.md#3-real-time-collaboration-signalr-architecture),
> section 3. That doc also confirms the per-project group design and the domain-event-based
> broadcast flow (service → domain event → notifier → hub) referenced here.

Hub broadcasts when data changes:

```csharp
// On task/feature update, notify all users in this project
hub.Clients.Group($"project-{projectId}")
  .SendAsync("FeatureUpdated", new { featureId, projectId, changedBy });

hub.Clients.Group($"project-{projectId}")
  .SendAsync("TaskUpdated", new { taskId, featureId, projectId, changedBy });
```

Frontend listens and invalidates cache:

```typescript
connection.on("TaskUpdated", ({ taskId, featureId, projectId, changedBy }) => {
  // Mark the affected feature's cache as stale
  if (featureId && cachedFeatures[featureId]) {
    cachedFeatures[featureId].invalidated = true;
  }
  // Optional: show toast "Task updated by {changedBy}" for visibility
});
```

## UX: Transparent Cache Invalidation (Phase 1 → Phase 2)

**Phase 1 (Now):** Lazy re-fetch on access
- User sees stale data until they close/reopen the feature
- Simple, reliable, easy to reason about
- Downside: Brief "loading..." when re-opening stale feature

**Phase 2 (Future):** Auto-refresh with visibility
- Background refresh when SignalR invalidates cache
- Show visual feedback on changed rows (fade highlight animation)
- Display timestamp: "Updated 2s ago" so users understand data is fresh
- If user is *editing* a field that changed remotely → conflict dialog: "Keep my changes? Use remote? Show diff?"
- **Key:** Always make updates visible (animation, timestamp, conflict dialog) so users never think the app is broken

## Scaling Path (No Breaking Changes)

### Today: Small Projects (0-100 items)
```
GET /api/projects/{id}?includeAll=true
→ Returns entire project with features + all tasks in one request
→ Eager-load everything, long cache validity
```

### Tomorrow: Large Projects (100+ items)
```
1. Metrics: If projectId has >50 features, disable includeAll
2. Client switches to lazy-load mode automatically
3. API pagination: GetTasks() uses offset/cursor pagination
4. No code refactor needed; just feature flags
```

### Future: Very Large Projects (1000+ items)
```
1. Add virtual scrolling to feature/task lists
2. Implement infinite-scroll pagination backend (cursor-based)
3. Cache strategies: Redis for shared access, Service Worker for offline
4. Advanced: incremental sync (load tasks 10 at a time, auto-fetch next batch as user scrolls)
```

## Verification & Testing

### Unit/Integration Tests
- [ ] Test cache invalidation: SignalR event → feature marked stale
- [ ] Test cache retrieval: Cached feature loads instantly, invalid feature re-fetches
- [ ] Test pagination: GetTasks handles pageSize/pageNumber correctly
- [ ] Test conflict detection: Two concurrent edits to same field show conflict dialog

### Manual Testing (Project Board View)
- [ ] Open project, verify all features + top-level tasks load
- [ ] Open a feature, verify tasks display
- [ ] Close feature, reopen → should be instant (from cache)
- [ ] In separate browser tab: edit a task in that feature
- [ ] First tab: SignalR event fires, cache marked invalid
- [ ] First tab: Reopen feature → re-fetches and shows updated data
- [ ] All users in project group receive SignalR broadcast (group subscription works)

### Performance Baseline
- [ ] Measure: Time to display project board (feature list + top-level tasks)
- [ ] Measure: Time to open a feature (first load = fetch, second = cache)
- [ ] Target: Project board < 500ms, feature open < 300ms (cached) or < 800ms (first load)

## Critical Files (to be created/modified)

- `PlanIt.Api/Hubs/ProjectHub.cs` — SignalR hub for broadcasts
- `PlanIt.Api/Controllers/ProjectsController.cs` — Endpoints with `includeAll` param
- `PlanIt.Api/Controllers/FeaturesController.cs` — Feature detail endpoint
- `PlanIt.Api/Controllers/TasksController.cs` — Pagination-ready endpoints
- `PlanIt.Web/hooks/useProjectData.ts` — Client data fetching + cache management
- `PlanIt.Web/hooks/useSignalR.ts` — SignalR listener + cache invalidation
- `PlanIt.Web/types/domain.ts` — `CachedFeature`, `ProjectState` types

## Open Questions / Deferred

1. **Cache expiry:** Should features expire after X hours? Or only via SignalR invalidation?
   - *Decision*: SignalR invalidation only (no TTL), to prevent unnecessary re-fetches of fresh data
   
2. **Offline support:** Should we persist cache to IndexedDB for offline access?
   - *Decision*: Phase 2+; start with in-memory cache
   
3. **Conflict resolution:** How granular should conflict detection be? (field-level, task-level, feature-level?)
   - *Decision*: Field-level for editing conflicts; task-level for position conflicts (drag-drop)

## Success Criteria

✅ Small projects (0-100 items) load in <500ms  
✅ Revisiting features loads instantly from cache  
✅ Real-time updates invalidate cache correctly  
✅ API design supports lazy-loading without code changes  
✅ Users always know when data is stale/updating (via timestamps, animations, conflict dialogs)
