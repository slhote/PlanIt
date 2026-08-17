# Similar Tasks Suggestions — Semantic Embeddings (Placeholder)

**Date:** 2026-08-14
**Status:** Placeholder — not designed in depth, needs its own design session before implementation
**Depends on:** [`planit-similar-tasks-lexical-metadata.md`](planit-similar-tasks-lexical-metadata.md) (the `ISimilaritySignal`/`Prepare` seam this work plugs into) must land first.

## Context

The lexical+metadata MVP's `ISimilaritySignal` interface is deliberately extensible so a future `EmbeddingSimilaritySignal` can be added with zero controller/API changes (per [`planit-system-design-architecture.md`](planit-system-design-architecture.md) §5). This doc is not a full design — it's a seeded set of open questions and working answers from a 2026-08-14 discussion, so the eventual design session doesn't start from zero. These carry real consequences for the lexical/metadata MVP's shared seams (`ISimilaritySignal.Prepare`, candidate-pool filtering), which is why they're captured now even though implementation is deferred.

**Not yet decided:** exact model choice, exact vector dimensionality, the full background-worker/queue implementation, exact DB schema for the embedding tables, frontend surfacing of the recompute-all action, and everything about eventual deployment topology.

## Generation mechanism — build BOTH, source picked at app startup, not swappable at runtime

- **Option A: in-process via `Microsoft.ML.OnnxRuntime`** — run a pre-trained sentence-embedding model (e.g. `all-MiniLM-L6-v2`) exported to ONNX format directly inside `PlanIt.Api`. The ONNX export is a one-time offline step (commonly done with Python tooling, but that tooling isn't part of the running system — it produces a model file checked in or downloaded at build/deploy time). Runtime is pure C#, no network call, no second process.
- **Option B: a separate local Python microservice** (FastAPI + `sentence-transformers`) the API calls over internal HTTP. Genuinely a second running process — needs its own local dev setup (own venv/deps, a way to start it alongside the API, e.g. added to whatever local dev tooling already runs `PlanIt.Api` + Postgres). More mature ML tooling than the ONNX route, at the cost of that second process.
- **Startup selection**: `SimilarWorkItems:EmbeddingSource: "Onnx" | "Python"` config, read once at startup, determines which `IEmbeddingGenerator` implementation gets DI-registered. No runtime toggle — changing it means restarting the app.

## Storage: separate tables per source, write to both, read from one

Embeddings from different models live in different vector spaces — cosine similarity between an ONNX-generated vector and a Python-service-generated vector is meaningless, even at matching dimensionality. `pgvector` columns are fixed-dimension, so if the two models don't share output dimensionality (likely, since they're different underlying models), a single shared vector column literally can't hold both anyway.

**Plan:** separate tables per source (`WorkItemEmbeddingOnnx`, `WorkItemEmbeddingPython`) rather than one table with a discriminator column.

**Confirmed: write to both, read from one.** Every trigger path (event-driven, sweep, recompute-all) computes and stores into *both* tables unconditionally — not just the startup-selected source. `SimilarWorkItems:EmbeddingSource` only controls which table the live `similar-tasks` endpoint *reads* from for scoring. This is what delivers the "evaluate which one I prefer long-term" goal — both stay populated and comparable side by side the whole time; switching which one is live is a config change + restart, not a backfill.

## Local-first — deployment ramifications flagged, not designed

For local dev, Option B means a second local process alongside the API + Postgres (own dependency setup, own start/stop, some way to wire it into whatever already brings up local dev — docker-compose per the System Design doc, or a dev script). Deployment (a second container on Azure, networking between API and Python service, added CI build step, added hosting cost) is explicitly **not** designed here — but the eventual design session should call out, at each design/implementation decision point, anything with serious downstream deployment impact, rather than assuming it'll sort itself out later.

## Trigger strategy: event-driven queue + periodic sweep + a recompute-ALL on-demand action

- **Event-driven**: existing `WorkItemCreatedNotification`/`WorkItemUpdatedNotification` (MediatR) are the natural hook, but a handler can't call an embedding step directly and stay off the request's critical path — needs to enqueue onto a background worker (e.g. `Channel<T>` + `BackgroundService`), not compute inline in the handler.
- **Periodic sweep**: background job scanning for `WorkItem.UpdatedAt > Embedding.ComputedAt` (or missing embedding row), catches anything the queue missed — and because a freshly-deployed feature has no existing embeddings, its first run doubles as the one-time backfill (no separate backfill job needed).
- **On-demand**: a **"recompute all"** action (UI placement TBD) — confirmed scope is bulk, not single-item. Gives a second, manual way to trigger the same full-backlog pass on demand rather than waiting for the sweep's schedule.

## Recompute filter: Title/Description change AND Status != Completed

**Edge case identified:** an item marked Completed before ever being embedded, later reopened with no text change, may still lack an embedding. Matters only if a task that was ever completed can still be opened as the *reference* item for its own similar-tasks view (candidates already exclude Completed items regardless, per the lexical/metadata MVP design — this is a reference-side gap only).

**Confirmed fix: widen the notification trigger, not lazy-on-read.** Also fire recompute on a status transition **out of** Completed, even with no text change — push-based/eager, closing the gap the moment it happens rather than deferring to the next read. Rejected lazy-on-read (pull-based, compute inline on first similar-tasks request) because it would introduce a "read triggers a write side-effect" pattern that doesn't otherwise exist in this codebase — every other embedding trigger, and the realtime/domain-event design generally, is push-based via MediatR notifications off a mutation.

## Retry/failure handling

Only a real concern if treating the call as network-flaky — relevant to Option B (Python service call is a network call across processes, even locally) though without the cost/rate-limit dimension a paid third-party API would add. Option A (in-process ONNX) sidesteps this category differently — its failure modes are local (model fails to load at startup, malformed input during tokenization), handled by fail-fast at startup and catch-and-log-per-item in the sweep; no retry/backoff machinery needed.

## Shared-seam implication for the lexical/metadata MVP

The `Prepare(reference, candidates)` lifecycle hook on `ISimilaritySignal` (introduced in the lexical/metadata MVP for `TfIdfLexicalStrategy`'s corpus stats) is the same seam `EmbeddingSimilaritySignal` will use — `Prepare` batch-fetches precomputed vectors for the reference + candidate pool (from whichever table `SimilarWorkItems:EmbeddingSource` selects) rather than doing per-candidate lookups inside `Score`. No `ISimilaritySignal` interface change anticipated when embeddings land.

## Next steps

This doc is a seed, not a design. Before implementation: pick concrete models for both options, design the exact `WorkItemEmbeddingOnnx`/`WorkItemEmbeddingPython` schemas, design the background worker/queue shape, decide UI placement for "recompute all," and work through the deployment-ramification flags called out above.
