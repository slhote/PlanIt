# Similar Tasks Suggestions — Lexical + Metadata MVP

**Date:** 2026-08-14
**Status:** Approved, implementation in progress
**Scope:** `ISimilaritySignal`-based scoring (tag overlap, assignee match, lexical text) for `GET /projects/{projectId}/workitems/{workItemId}/similar-tasks`

## Context

Similar Tasks Suggestions is master-plan subplan 8, sequenced last because it needs the WorkItem hierarchy, Tags, and a real API surface — all now in place. [`planit-system-design-architecture.md`](planit-system-design-architecture.md) §5 already locked in the shape: MVP = **lexical + metadata only** (no embeddings, no behavioral/collaborative signals), scored via an `ISimilaritySignal` interface so a future `EmbeddingSimilaritySignal` can be added later with zero controller/API changes. A route stub exists (`GET /projects/{projectId}/workitems/{workItemId}/similar-tasks` → 501, [`planit-api-contracts-backend.md`](planit-api-contracts-backend.md) §8 step 9) reserving the URL.

This doc supersedes the generic [`similar-tasks-feature-planning.md`](similar-tasks-feature-planning.md) framework doc for the lexical+metadata scope — that doc's decision framework (Steps 1–5) is why this scope was chosen; this doc is the concrete design against it.

**Scope split:** this doc covers lexical + metadata only. Future semantic/embeddings work is a separate placeholder doc, [`planit-similar-tasks-semantic-embeddings.md`](planit-similar-tasks-semantic-embeddings.md) — not designed in depth here, and explicitly out of this implementation.

## Decision: Jaccard vs. TF-IDF as a switchable flag

Both lexical-similarity algorithms are implemented, selected via config (`SimilarTasks:LexicalStrategy`: `"Jaccard"` | `"TfIdf"`), rather than picking one. This is a strategy swap internal to `LexicalTextSignal`, not two competing weighted signals — avoids re-balancing scorer weights every time the strategy is toggled for evaluation.

**Design wrinkle:** TF-IDF needs corpus-wide statistics (document frequency across the whole candidate pool) that pairwise `Score(candidate, reference)` doesn't have access to; Jaccard is genuinely pairwise and doesn't need this. Resolved by giving `ISimilaritySignal` an explicit `Prepare(reference, candidates)` step, called once per request before the per-candidate scoring loop — a TF-IDF strategy builds its corpus stats there; Jaccard no-ops it. Signal instances are DI-registered `Scoped` (one per HTTP request), so holding precomputed state as private fields between `Prepare` and `Score` within one request is safe.

This `Prepare` hook is deliberately general-purpose, not lexical-specific — a future `EmbeddingSimilaritySignal` uses the same seam to batch-fetch/compute vectors for the reference + candidate pool up front. See the semantic-embeddings placeholder doc.

## Architecture

New folder `PlanIt.Api/Application/Similarity/`:

```csharp
public interface ISimilaritySignal
{
    void Prepare(WorkItem reference, IReadOnlyList<WorkItem> candidates) { } // default no-op
    double Score(WorkItem candidate, WorkItem reference); // 0.0–1.0
}
```

- **`TagOverlapSignal`** — Jaccard over lowercase tag sets (`|intersection| / |union|`; 0 if either set is empty).
- **No `ProjectMatchSignal`.** The architecture doc lists project match as one of 5 MVP signals, but since the candidate repository query already scopes to the same project (see Data access below), a per-candidate project-match score would be constant `1.0` for every candidate in the pool — no discriminating power. Project match is enforced structurally by the query, not scored. Cross-project search, if ever wanted, is a scope change to the repository query, not a resurrected signal.
- **`AssigneeMatchSignal`** — `1.0` if `AssigneeId` matches and is non-null, else `0.0`.
- **`LexicalTextSignal`** — tokenizes `Title + " " + Description` (lowercase, strip punctuation, basic stopword list), delegates to an injected `ILexicalSimilarityStrategy`:
  - `JaccardLexicalStrategy` — token-set Jaccard overlap, no `Prepare` needed.
  - `TfIdfLexicalStrategy` — `Prepare` builds per-token IDF across the candidate pool + reference; `Score` computes TF-IDF vectors and cosine similarity.
  - Strategy selected at startup via `IOptions<SimilarTasksOptions>.LexicalStrategy`, resolved through a factory registration in `Program.cs`.
- **`WeightedSimilarityScorer`** — takes `IEnumerable<ISimilaritySignal>` (DI-injected collection), computes weighted sum per candidate using `SimilarTasksOptions.Weights`, ranks descending, applies `MinScoreThreshold` and `MaxResults` (default 5, per the 3–5 cap already decided).
- **`SimilarTasksService`** — orchestrates: candidate pool via repository, `scorer.Rank(reference, candidates)`, maps to DTOs. Concrete class, not interface-backed — matches `WorkItemService`/`ProjectService` convention (only repositories are interface-abstracted in this codebase).

**`SimilarTasksOptions`** (bound from `appsettings.json`, following the `JwtOptions`/`CorsOptions` pattern in `Startup/Options/`):
```json
"SimilarTasks": {
  "LexicalStrategy": "Jaccard",
  "MaxResults": 5,
  "MinScoreThreshold": 0.0,
  "Weights": { "TagOverlap": 0.35, "AssigneeMatch": 0.15, "LexicalText": 0.5 }
}
```

## Data access

`IWorkItemRepository` gets one new method: `GetSimilarityCandidatesAsync(Guid projectId, Guid excludeWorkItemId)` — returns all non-`Completed` WorkItems in the project excluding the reference item. Implemented in `WorkItemRepository` as a straightforward EF query; the existing `(ProjectId, ParentId)` index (`WorkItemConfiguration.cs`) already anticipates this scan — its comment names the Similar Tasks use case.

## DTOs

Extends the api-contracts doc's `WorkItemSummaryDto` with a score, since the planning doc's explainability principle ("similar because...") benefits from a visible ranking value:

```csharp
public record SimilarWorkItemDto(WorkItemSummaryDto WorkItem, double Score);
```

Lives in `Contracts/WorkItems/SimilarWorkItemDto.cs`. Small, intentional deviation from the api-contracts doc's bare `WorkItemSummaryDto[]` — cheap, useful for the frontend to eventually show/debug relevance, doesn't change route or auth shape.

## Controller wiring

Replaces the 501 stub in `WorkItemsController.cs`:

```csharp
[HttpGet("{id:guid}/similar-tasks")]
public async Task<ActionResult<IReadOnlyList<SimilarWorkItemDto>>> GetSimilarTasks(Guid projectId, Guid id)
    => Ok(await similarTasksService.GetSimilarAsync(projectId, id));
```

`SimilarTasksService.GetSimilarAsync` throws `TaskNotFoundException` if the reference work item doesn't exist (existing global handler covers it). Zero-match case returns `[]`.

## DI registration (`Program.cs`)

```csharp
builder.Services.Configure<SimilarTasksOptions>(builder.Configuration.GetSection("SimilarTasks"));
builder.Services.AddScoped<ISimilaritySignal, TagOverlapSignal>();
builder.Services.AddScoped<ISimilaritySignal, AssigneeMatchSignal>();
builder.Services.AddScoped<ISimilaritySignal, LexicalTextSignal>();
builder.Services.AddScoped<ILexicalSimilarityStrategy>(sp => ...); // Jaccard or TfIdf per options
builder.Services.AddScoped<WeightedSimilarityScorer>();
builder.Services.AddScoped<SimilarTasksService>();
```

## Testing

The four signal implementations and the scorer are pure functions over `WorkItem` objects — no I/O, no repository dependency. Unit-testable in `PlanIt.Api.Tests` **without Moq**, respecting the master plan's "don't add Moq preemptively" constraint while still getting real coverage. Tests cover: tag Jaccard math, assignee match, both lexical strategies against hand-written near-miss/match pairs, and weighted-scorer ranking/threshold/cap behavior. `SimilarTasksService` itself (needs the repository) stays untested for now, consistent with the rest of the service layer.

## Branching

Per repo convention ([`planit-api-contracts-backend.md`](planit-api-contracts-backend.md) §9 pattern):

- Integration branch: `integration/similar-tasks-lexical-metadata`, cut from `main`.
- Sub-step branches off the integration branch's tip:
  1. `feature/similar-tasks-lexical-metadata/01-candidate-repository-query`
  2. `feature/similar-tasks-lexical-metadata/02-metadata-signals` (Tag/Assignee)
  3. `feature/similar-tasks-lexical-metadata/03-lexical-signal-jaccard-tfidf`
  4. `feature/similar-tasks-lexical-metadata/04-scorer-service-controller-wiring`
  5. `feature/similar-tasks-lexical-metadata/05-unit-tests`
- Each PRs into the integration branch. One final PR, `integration/similar-tasks-lexical-metadata` → `main`, once all steps land.

## Critical files

- `PlanIt.Api/Controllers/WorkItemsController.cs` (replace stub)
- `PlanIt.Api/Domain/Repositories/IWorkItemRepository.cs` + `Data/Repositories/WorkItemRepository.cs`
- `PlanIt.Api/Application/Similarity/*` (new)
- `PlanIt.Api/Contracts/WorkItems/SimilarWorkItemDto.cs` (new)
- `PlanIt.Api/Startup/Options/SimilarTasksOptions.cs` (new)
- `PlanIt.Api/Program.cs`
- `PlanIt.Api/appsettings.json`
- `PlanIt.Api.Tests/`

## Verification

1. `dotnet build` succeeds.
2. `dotnet test` — new unit tests pass for both lexical strategies, tag/assignee signals, scorer ranking/threshold/cap logic.
3. Manual: seed two projects with work items (some sharing tags/assignee, some with overlapping title keywords, some unrelated); hit `GET /projects/{id}/workitems/{id}/similar-tasks` with a valid Bearer token; confirm results ranked sensibly, capped at `MaxResults`, and toggling `SimilarTasks:LexicalStrategy` changes ranking without a code change.
4. Confirm zero-candidate case returns `[]`, not an error.
5. Confirm non-member access still gets 404 (regression check on existing `ProjectMember404ResultHandler`).
