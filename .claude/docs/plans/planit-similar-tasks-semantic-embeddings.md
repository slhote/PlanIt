# Similar Tasks Suggestions — Semantic Embeddings

**Date:** 2026-08-17
**Status:** Designed, ready for implementation review
**Depends on:** [`planit-similar-tasks-lexical-metadata.md`](planit-similar-tasks-lexical-metadata.md) — **landed** (`ISimilaritySignal`/`Prepare` seam, `WeightedSimilarityScorer`, `SimilarWorkItemsService`, all merged to `main` via PR #29, plus follow-up fixes in PRs #30-37).

## Context

This doc supersedes the previous placeholder version of the same filename (dated 2026-08-14), which deliberately left the generation mechanism, storage, and deployment questions open pending "its own design session." This is that session. It also revisits one placeholder assumption — **build both ONNX and a Python microservice** — against what's actually decided elsewhere in the plan set, and narrows scope.

## Generation mechanism: build both, source picked at app startup

Per explicit instruction, this keeps the placeholder's original decision: build **both** an in-process ONNX path and a separate Python microservice, rather than ONNX only. Revisited the tradeoff (a second process reintroduces some of the operational complexity the Postgres/pgvector choice was meant to avoid) but that tradeoff is accepted deliberately here — the goal is a real side-by-side comparison of the two generation sources, not just shipping one.

- **Option A — in-process via `Microsoft.ML.OnnxRuntime`**: `sentence-transformers/all-MiniLM-L6-v2`, ONNX-exported. The export is a one-time offline step; the `.onnx` model file (~90MB) is checked into the repo or pulled at build/deploy time, not generated at runtime. Pure C#, no network call, no second process. Dimensionality: 384.
- **Option B — separate local Python microservice** (FastAPI + `sentence-transformers`), called over internal HTTP. A genuinely second running process: own venv/deps, wired into local dev tooling only (added as a `docker-compose.yml` service — see Local dev below). Model: `sentence-transformers/all-mpnet-base-v2` (768-dim) — deliberately a *different* model from the ONNX path, since the point of running both is comparing distinct approaches, not two copies of the same model through different runtimes.
- **Startup selection**: `SimilarWorkItems:EmbeddingSource: "Onnx" | "Python"` config, read once at startup, determines which `IEmbeddingGenerator` implementation gets DI-registered for the *live scoring path*. No runtime toggle — changing it means restarting the app.
- **Runtime failure modes**:
  - Option A: local only — model fails to load at startup (fail-fast, app won't start) or malformed input during tokenization (catch-and-log per item in the sweep, skip that item). No retry/backoff — no network call to be flaky.
  - Option B: the HTTP call to the Python service is a network call across processes, even locally. Treated as flaky: bounded retry (e.g. 3 attempts, exponential backoff) inside the background worker's per-item processing, catch-and-log-and-skip if all retries fail — never blocks the queue on one bad item.

## Storage: separate tables per source, write to both, read from one

Embeddings from different models live in different vector spaces — cosine similarity between an ONNX-generated vector and a Python-service-generated vector is meaningless, even at matching dimensionality, and here they don't even share dimensionality (384 vs 768). `pgvector` columns are fixed-dimension, so a single shared column can't hold both anyway.

**`WorkItemEmbeddingOnnx`** and **`WorkItemEmbeddingPython`** — separate tables, not one table with a discriminator:

| Column | Type | Notes |
|---|---|---|
| `WorkItemId` | `uuid` PK, FK → `WorkItem.Id` `ON DELETE CASCADE` | one row per work item per table |
| `Vector` | `vector(384)` / `vector(768)` respectively | via `Pgvector.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL` vector plugin |
| `SourceText` | `text` | the exact `Title + " " + Description` string that was embedded — lets the sweep detect drift without re-tokenizing every scan |
| `ComputedAt` | `timestamptz` | for staleness queries and observability |

**Write to both, read from one.** Every trigger path (event-driven, sweep, recompute-all) computes and stores into *both* tables unconditionally, regardless of which source `SimilarWorkItems:EmbeddingSource` currently selects — that's what makes the two comparable side by side at any point in time, not just after a backfill triggered by a config flip. `EmbeddingSource` only controls which table the live `similar-tasks` scoring path reads from.

Migration adds `CREATE EXTENSION IF NOT EXISTS vector;` plus both tables — additive, no changes to `WorkItem`. Matches the "no pre-built pgvector column on `WorkItem`" principle from the system-design doc; these are new tables, not a retrofit.

## Read path: `EmbeddingSimilaritySignal`

Implements `ISimilaritySignal`, plugs into the existing seam with zero controller/scorer changes:

- `Prepare(reference, candidates)` — one query fetching rows from whichever table `SimilarWorkItems:EmbeddingSource` selects (`WorkItemEmbeddingOnnx` or `WorkItemEmbeddingPython`) for the reference + all candidate IDs (`WHERE WorkItemId = ANY(@ids)`), held as a private dictionary for the request's lifetime (signals are `Scoped`, same pattern `TfIdfLexicalStrategy` already uses for its corpus stats). The repository method takes the table choice as a parameter rather than the signal knowing about both tables directly.
- `Score(candidate, reference)` — cosine similarity between the two held vectors, computed in C# over the already-fetched pair (not a second DB round-trip via pgvector's `<=>` operator — the candidate pool here is already small and in memory, consistent with how every other signal scores in-process).
- **Missing-embedding candidates** (not yet computed, or computed then the sweep hasn't caught up) score `0.0` for this signal rather than being excluded from the pool — they simply don't benefit from this one signal's contribution, the same way a candidate with no tags scores `0.0` on `TagOverlapSignal` rather than being dropped.
- Registered in `Program.cs` alongside the other three signals; gets a weight in `SimilarWorkItemsOptions.Weights` (e.g. `"Embedding": 0.4`, rebalancing the existing three — exact split is a tuning question for after it's running against real data, not blocking implementation).

## Trigger strategy

Same three-part strategy the placeholder proposed, now concrete:

1. **Event-driven**: `WorkItemCreatedNotification`/`WorkItemUpdatedNotification` MediatR handlers enqueue `(WorkItemId)` onto a `Channel<Guid>` consumed by a new `EmbeddingBackgroundService : BackgroundService`. Handler stays off the critical path — it enqueues and returns immediately, doesn't await the embedding computation. The consumer computes via **both** `IEmbeddingGenerator` implementations for each dequeued item and writes to both tables — not just the startup-selected one.
2. **Periodic sweep**: same `BackgroundService`, on a timer (e.g. every 5 minutes), queries for `WorkItem`s where `SourceText <> (Title + ' ' + Description)` or no row exists yet in *either* table, and enqueues those. This is the backfill mechanism too — a fresh deploy has zero embeddings in either table, so the first sweep run enqueues everything. No separate backfill job.
3. **On-demand recompute-all**: `POST /projects/{projectId}/similar-tasks/recompute` (owner-only, `[Authorize(Policy = "ProjectMember")]` won't be enough by itself — needs the existing Owner-vs-Member role check, matching how other destructive/bulk project actions are gated) enqueues every work item in the project. **UI placement: Project Settings page**, alongside other project-level admin actions, as a button with a short explanatory line ("Recomputes AI-based similarity for all tasks in this project") — this is a low-frequency maintenance action, not a task-detail-page control.

**Recompute filter, per the already-confirmed edge case in the prior version of this doc:** the sweep's `SourceText` comparison only catches text changes. Status transitions **out of** `Completed` with no text change (an item embedded, then completed, then reopened unchanged) need a second explicit trigger — `WorkItemStatusChangedNotification`'s handler also enqueues when the new status isn't `Completed` and the old one was. Push-based, consistent with every other trigger here; no lazy-on-read.

## Deployment: explicitly out of scope

This feature is being built for **local use only**. No deployment/CD work is in scope here, and none of the choices above should be read as pre-designing it — `pgvector`-on-Azure, shipping the ONNX model file in a deployed image, and a second hosted service for the Python microservice are all real questions, but they belong to `planit-devops-hosting.md` (and CD work generally, which per the root `CLAUDE.md` hasn't started at all) if and when this ever needs to run somewhere other than a dev machine. Not designing them now, not flagging them as blocking — just noting the line so a future session doesn't assume they were decided here.

## Local dev

- `docker-compose.yml`'s `postgres` service switches from `postgres:16` to a `pgvector/pgvector:pg16` image (or runs `CREATE EXTENSION` via an init script) — the stock Postgres image doesn't ship the extension.
- Add a new `embedding-service` entry to `docker-compose.yml` (Python/FastAPI, its own `Dockerfile` under a new `PlanIt.EmbeddingService/` or similar directory), on the same `full-stack` profile as `api`, with `api`'s `IEmbeddingGenerator` (Python variant) pointed at it via an internal URL (e.g. `EmbeddingService__BaseUrl: "http://embedding-service:8000"`).
- Running the API standalone without Docker Compose (the common local loop per root `CLAUDE.md`'s `dotnet run --project PlanIt.Api`) means the Python service isn't running either — the background worker's Option B calls fail and get caught/logged/skipped per the retry policy above; Option A (ONNX) keeps working regardless since it's in-process. Acceptable for day-to-day dev; run compose's `full-stack` profile when testing the Python path specifically.
- **`dev.ps1` update**: the script's normal loop runs the API via `dotnet run` directly (not through the `full-stack` compose profile), so it needs its own logic to bring up the Python embedding service only when needed. Read `SimilarWorkItems:EmbeddingSource` from `PlanIt.Api`'s config (`dotnet user-secrets` / `appsettings.Development.json`) before launching the API; if it's `"Python"`, also start the `embedding-service` compose entry (`docker compose ... up -d embedding-service`) and wait for it to be healthy, the same pattern already used for Postgres. If the source is `"Onnx"`, skip that step entirely — no behavior change for the common case.

## Testing

`EmbeddingSimilaritySignal.Score`'s cosine-similarity math is a pure function over two vectors — unit-testable without Moq or a DB, same as the existing signals, and identical regardless of which table/source it's reading from. The ONNX inference call, the Python HTTP client, and the background worker/sweep are I/O-bound and out of scope for the no-Moq unit test layer per the Testing subplan's boundaries — left for that subplan (still not started) to decide how repository/worker/HTTP-client-level tests get covered.

## Branching

Per repo convention: cut `integration/similar-tasks-embeddings` off `main`, sub-steps branch off its tip as `feature/similar-tasks-embeddings/NN-...`, one PR takes the integration branch to `main` at the end. Suggested step breakdown:

1. `pgvector` migration + `WorkItemEmbeddingOnnx`/`WorkItemEmbeddingPython` entities/config + repository methods to batch-fetch by IDs per table
2. ONNX model wiring (`Microsoft.ML.OnnxRuntime` DI registration, model file, `IEmbeddingGenerator` abstraction + tokenizer) — Option A generator
3. Python microservice scaffold (FastAPI + `sentence-transformers`) + HTTP client `IEmbeddingGenerator` implementation + retry policy — Option B generator
4. `EmbeddingSimilaritySignal` + `Program.cs`/options wiring (`SimilarWorkItems:EmbeddingSource`), rebalanced weights
5. `EmbeddingBackgroundService` (channel consumer computing via both generators + periodic sweep) wired to the three MediatR notification handlers
6. Recompute-all endpoint + Project Settings UI button
7. `docker-compose.yml` wiring for the Python service
8. `dev.ps1` update — conditionally launch `embedding-service` when `SimilarWorkItems:EmbeddingSource` is `"Python"`
9. Unit tests for the signal's cosine-similarity math

## Open items deferred to implementation

- Exact rebalanced `Weights` values — start with a reasonable split, tune after seeing it run against real project data.
- Sweep interval (proposed 5 minutes) — adjust based on observed staleness tolerance.
- Whether `EmbeddingBackgroundService`'s channel needs bounded capacity/backpressure handling — likely not at this project's scale, revisit if it becomes a real queue depth concern.
- Python microservice's own project structure/conventions (it's a new language/runtime in this repo) — not specified beyond "FastAPI + sentence-transformers"; first implementation step should establish its shape.
- Deployment topology for either generator is explicitly deferred — see Deployment section above.
