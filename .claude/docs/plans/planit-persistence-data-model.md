# Persistence / Data Model Subplan

**Date:** 2026-08-11
**Status:** Design Decision (Phase 0 — foundational, gated on System Design & Architecture)
**Scope:** EF Core entity schema, relationships, constraints, migration strategy for PlanIt.Api

## Context

This subplan finalizes the database schema for PlanIt, informed by the decisions locked in
[`planit-system-design-architecture.md`](planit-system-design-architecture.md): PostgreSQL as the
engine, `xmin`-backed optimistic concurrency, the Similar Tasks Suggestions groundwork (tag shape,
scope), and the strict layering requirement.

**Starting point:** the codebase is a genuine blank slate. `PlanIt.Api` has no Models/Entities
folder, no `DbContext`, no EF Core or Npgsql package references, and no `ConnectionStrings` entry in
`appsettings.json`. `PlanIt.Api.Tests` has no Moq reference (Moq strategy belongs to the Testing
subplan, not here). This subplan designs the schema; wiring up the actual `DbContext`, migrations,
and DI registration is part of the API Contracts & Backend subplan (subplan 3) that follows.

---

## Entities

### User

```
User
  Id                Guid        PK, client-generated
  Username           varchar(50) NOT NULL, UNIQUE
  Email               varchar(254) NOT NULL, UNIQUE
  PasswordHash        varchar NOT NULL
  CreatedAt            timestamptz NOT NULL, default now()
```

Standard auth fields. `PasswordHash` uses a standard adaptive hash (e.g. bcrypt/Argon2, decided at
implementation time by subplan 3 — not a persistence-layer concern beyond "it's a string column").

### Project

```
Project
  Id               Guid         PK, client-generated
  Name              varchar(100) NOT NULL
  Description        varchar(1000) NULL
  CreatedByUserId      Guid         NOT NULL, FK -> User.Id
  CreatedAt             timestamptz NOT NULL, default now()
```

`CreatedByUserId` records provenance, but is **not** the access-control mechanism — see
`ProjectMember` below, which is the single source of truth for "who can access this project."

### ProjectMember

```
ProjectMember
  ProjectId   Guid   NOT NULL, FK -> Project.Id, ON DELETE CASCADE
  UserId       Guid   NOT NULL, FK -> User.Id
  Role          smallint NOT NULL   -- enum: Owner = 0, Member = 1
  JoinedAt       timestamptz NOT NULL, default now()

  PK (ProjectId, UserId)
```

**Decision: the project creator gets an explicit row here too** (`Role = Owner`), rather than being
tracked only via `Project.CreatedByUserId`. This means every "can this user access this project?"
check — the 404-vs-403 gate, the SignalR group-join authorization, the collaborator-list query — is
a single uniform query against `ProjectMember`, never a repeated `OR`/`UNION` against
`Project.CreatedByUserId` scattered across call sites. Creator-only actions (delete project, manage
membership) additionally check `Role == Owner`.

**Insert-time responsibility:** when a Project is created, the service layer must insert both the
`Project` row and its creator's `ProjectMember` row (`Role = Owner`) in the same transaction — this
isn't automatic from the FK relationship alone, and must be called out explicitly to whoever
implements project creation in subplan 3.

### WorkItem

```
WorkItem
  Id            Guid          PK, client-generated
  WorkItemType    smallint      NOT NULL   -- discriminator enum: Feature = 0, Task = 1
  ProjectId        Guid          NOT NULL, FK -> Project.Id, ON DELETE CASCADE
  ParentId          Guid          NULL, FK -> WorkItem.Id (self-referencing)
                                  -- NULL = direct child of Project
                                  -- non-NULL = child of a Feature (only valid when WorkItemType = Task)
  Title              varchar(200)  NOT NULL
  Description          varchar(4000) NULL
  Status                smallint      NOT NULL   -- enum: ToDo = 0, InProgress = 1, Completed = 2
  AssigneeId             Guid          NULL, FK -> User.Id
  Tags                    text[]        NOT NULL, default '{}'
                                        CHECK (cardinality(tags) <= 3)
  CreatedAt                 timestamptz   NOT NULL, default now()
  UpdatedAt                   timestamptz   NOT NULL, default now()
```

**Single-table design** (EF Core TPH — table-per-hierarchy) with a `WorkItemType` discriminator, per
the master plan's working assumption. `Feature` and `Task` are the only two types (`Subtask` was cut
from scope entirely in an earlier session). A `Feature` can only be a direct child of a `Project`
(`ParentId == null`); a `Task` can be either a direct child of a `Project` or a child of a `Feature`.
This constraint (Feature can't have `ParentId` set; Task's parent, if set, must be a Feature not
another Task) is **not** expressible cleanly as a single DB-level CHECK across two nullable/FK
columns in a maintainable way — it's enforced at the service layer, with a documented invariant
comment on the entity.

**Concurrency:** no explicit `RowVersion` column — Postgres's native `xmin` system column is mapped
directly as the EF Core concurrency token (`.UseXminAsConcurrencyToken()` on the Npgsql provider),
per the System Design decision. Nothing to add to the migration for this; `xmin` exists on every
Postgres row automatically.

**Tags:** native `text[]` column, not a junction table. Tags have no independent identity (no global
tag list, no cross-work-item tag lookup needed for MVP), so the array column avoids an unnecessary
join on every work-item read, and `CHECK (cardinality(tags) <= 3)` is a genuine native Postgres
constraint — a junction-table equivalent would need a trigger instead, since CHECK constraints can't
count sibling rows. Case-insensitive tag matching (for `TagOverlapSignal`) is done via
lowercase-normalized comparison at write time (store tags lowercased) rather than a
case-insensitive query operator, keeping read-path queries simple.

**Cascade delete:** `ProjectId` FK is `ON DELETE CASCADE` (deleting a Project deletes all its
WorkItems). The Feature→Task cascade (`ParentId` FK) is **also** `ON DELETE CASCADE` at the DB
level as a safety net, but the *primary* delete-cascade logic (confirming the "this will delete N
tasks" count to the frontend, and the extensible per-child-type strategy named in System Design)
lives in the service layer — the DB cascade is defense-in-depth, not the mechanism the app relies on
for its UX/confirmation flow.

**Cascade completion:** *not* a DB-level cascade (Postgres has no clean way to express "cascade this
specific field's value on update, but only when it enters one specific state, and don't cascade
back on reversal" as a constraint or trigger without real complexity). This logic lives entirely in
the service layer, as already decided in System Design.

### RefreshToken

```
RefreshToken
  Id                  Guid         PK, client-generated
  UserId                Guid         NOT NULL, FK -> User.Id
  TokenHash              varchar      NOT NULL, UNIQUE
                                     -- the raw token is never stored, only its hash
  ExpiresAt                 timestamptz  NOT NULL
  RevokedAt                   timestamptz  NULL
  ReplacedByTokenId             Guid         NULL, FK -> RefreshToken.Id (self-referencing)
  CreatedAt                       timestamptz  NOT NULL, default now()
```

**Decision: rotation with reuse detection**, not bare rotation. On each refresh:
1. Look up the presented token by `TokenHash`.
2. If `RevokedAt` is already set → this token was already used once and rotated away. Presenting it
   again can only mean it was stolen and is being replayed. **Revoke every active token for that
   `UserId`** (set `RevokedAt` on all of them), forcing re-login on every device.
3. If `RevokedAt` is `NULL` and not expired → valid refresh. Issue a new access token + new refresh
   token, set the old row's `RevokedAt = now()` and `ReplacedByTokenId = newToken.Id`, insert the
   new row.

This is what actually delivers the "mitigate XSS exposure" goal the master plan states for
refresh-token rotation — bare rotation (just replacing the token each time, no reuse check) gives no
real detection signal if a token is stolen and used before the legitimate user's next refresh.

---

## Constraint philosophy: defense-in-depth

**The database enforces basic invariants as a second layer, alongside app-layer validation at the
API boundary** — not app-layer-only. Every column definition above states its `NOT NULL` and length
constraints explicitly; these mirror the same rules the API validates on input, so a bug or a future
code path that bypasses the service layer (a seed script, an admin tool, a careless bulk import)
still can't silently write invalid data. A violated constraint throws a loud, immediate DB error
instead of corrupting data quietly. This is cheap to set up now, at initial-migration time, and
expensive to retrofit onto a live table later — so it's stated as a standing principle for any future
schema additions, not just the columns listed here.

---

## Migration strategy

**EF Core Migrations**, as confirmed in System Design. Conventions for subplan 3 (which actually
writes the `DbContext` and generates migrations):

- Migrations live in `PlanIt.Api/Migrations/`, checked into source control.
- Each migration is named for the schema change it makes (e.g.
  `AddWorkItemTagsCheckConstraint`), not generically (`Migration1`, `Update1`).
- Local dev and production run the **same** Postgres major version (per System Design), so a
  migration that applies cleanly in Docker applies cleanly against the managed Azure instance.
- No seed-data migration is included here — seed/demo data generation is the Testing subplan's job
  (subplan 6), which also serves Similar Tasks Suggestions' need for realistic volume.

## Indexes

Beyond the PKs/FKs above (which get indexes automatically), the following query patterns justify
explicit indexes, to be added when subplan 3 writes the actual EF Core configuration:

- `WorkItem(ProjectId, ParentId)` — board-view queries (fetch all work items for a project, grouped
  by parent) and the Similar Tasks same-project candidate scan.
- `WorkItem(AssigneeId)` — "my tasks" filter views.
- `ProjectMember(UserId)` — "my projects" listing (all projects a user belongs to).
- `RefreshToken(TokenHash)` — already UNIQUE, so indexed by default; this is the hot lookup path on
  every refresh request.

---

## Verification

This is a design decision document — no code exists yet. Verification happens when subplan 3
implements it:

1. `DbContext` and entity configurations (`IEntityTypeConfiguration<T>`) match the shapes above.
2. `dotnet ef migrations add InitialCreate` produces a migration that applies cleanly against a local
   Postgres Docker container.
3. `dotnet ef database update` succeeds; manually verify the `CHECK (cardinality(tags) <= 3)`
   constraint rejects a 4-tag insert, and that `xmin` is usable as a concurrency token in a
   round-trip update-conflict test.
4. A round-trip test: create a Project (verify both the `Project` row and the creator's `Owner`
   `ProjectMember` row are inserted together), create a Feature and a Task under it, delete the
   Feature, confirm the Task is gone (cascade).
