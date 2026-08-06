# Feature Planning: "Similar Tasks" Suggestions

**Feature summary:** When a user opens a task, show 3-5 similar tasks alongside it.

---

## Step 1: Define "Similar" Before Touching Any Tech

This is the actual crux of the feature — it's tempting to jump straight to "add NLP," but that only
addresses one flavor of similarity. For a task board, "similar" could mean:

- **Lexical** — overlapping keywords in title or description
- **Structural/metadata** — same tags, labels, project, or assignee
- **Semantic** — related meaning even with different words (e.g., "fix login bug" vs. "auth error on signin")
- **Behavioral/co-occurrence** — tasks frequently completed together or in sequence
- **Collaborative** — tasks other users viewed after viewing this one (like "customers also bought")

These aren't mutually exclusive, but they lead to very different implementations.

**Action item for planning session:** Write down 5-10 example task pairs (real or imagined) that
*should* count as similar, plus a few near-misses that shouldn't. This becomes an informal test set
for evaluating any approach later, and it forces the team to agree on a definition before building.

---

## Step 2: Audit What Data You Actually Have

Similarity approaches are constrained by the existing schema. Inventory what's on a task:

- Title, description
- Tags/labels
- Project/board
- Assignee
- Due date, status
- Comments, attachments
- Creation date

**Why this matters:** metadata-based similarity (same tags + same project + close due dates) is often
surprisingly effective and nearly free — a SQL query with a scoring formula, no ML required. It's
worth explicitly considering as a baseline before reaching for NLP because it's:
- Cheap to build
- Fast to serve
- Explainable to users ("similar because: same tags")
- Easy to debug

---

## Step 3: Decide Where NLP Actually Earns Its Complexity

If titles/descriptions carry the real signal (e.g., tagging is inconsistent but descriptions are
rich), text similarity becomes worth it. Climb this ladder — start at the bottom, only go further if
the simpler option fails:

1. **Keyword overlap / TF-IDF** — no ML model, fast, explainable, decent baseline
2. **Embeddings** (sentence-transformers, OpenAI/Anthropic embeddings, etc.) — captures semantic
   meaning, but adds infra: generating embeddings, storing vectors, nearest-neighbor search
3. **Fine-tuned/task-specific models** — usually overkill unless at large scale or very
   domain-specific vocabulary

Embeddings are the sweet spot for most apps like this: mature tooling, no need to train your own
model, and cosine similarity is simple to reason about.

---

## Step 4: System Design Questions (Not Just the Algorithm)

- **When is similarity computed?** On-demand at task-open time vs. precomputed/cached (e.g.,
  recompute embeddings on task create/update, store in a vector index — pgvector, Pinecone, or an
  in-memory index at small scale)
- **How fresh does it need to be?** Frequently edited tasks risk stale embeddings returning bad matches
- **What's the scale?** 500 tasks vs. 500,000 tasks determines whether "loop and compare" is fine or
  an actual vector DB/ANN index is needed
- **Should signals be combined?** Most real systems blend a metadata score + a text similarity score
  into one ranked list rather than relying on a single signal

---

## Step 5: Evaluation & Edge Cases to Plan For

- What happens with zero good matches — show nothing, or a fallback like "explore related tags"?
- Exclude completed/archived tasks, or include them for context?
- How do we know it's working? Validate against the hand-written test pairs from Step 1 before shipping.

---

## Recommended Build Order

1. Write concrete "similar" examples for this domain (test set)
2. Ship a metadata-based version first (tag/project/assignee overlap) — cheap, validates the UX fast
3. If text similarity is clearly needed, add embeddings + cosine similarity, precomputed and indexed
4. Combine scores, rank, cap results at 3-5
5. Handle empty-state and staleness
6. Watch real usage/feedback to see if "similar" matches user intuition, then iterate

**Key principle:** the ML/NLP component should be the *last* decision, justified by what the simpler
baseline can't do — not the first thing reached for.
