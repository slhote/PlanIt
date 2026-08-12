---
name: visually-inspect-ui
description: Verify PlanIt.Web UI changes by rendering them in a real browser via Playwright and inspecting the result. Use whenever front-end code is added or modified — new components, layout changes, styling changes, new pages/routes — before declaring the work done. Not for backend-only or non-visual logic changes, and not for a full regression sweep of the whole app; verify only what changed. Never use the Browser pane's screenshot action for this — always drive Playwright directly via the project's screenshot script.
---

# Visually Inspect Web UI with Playwright

PlanIt.Web changes must be rendered and inspected in a real browser before being reported as
done — not assumed correct from reading the code. This is a **verify-and-iterate loop**, not a
single screenshot-and-describe step: write/adjust code → render it → capture what was actually
produced → inspect for problems → fix → repeat until correct → deliver final screenshots with a
report.

**Never use the Browser pane's `mcp__Claude_Browser__computer` screenshot action (or any
Browser-pane screenshot tool) for this** — it has been unreliable for UI verification in this kind
of environment. Use the project's own Playwright script instead, which is already set up
(`@playwright/test` is a devDependency of `PlanIt.Web`, Chromium is installed).

## The script

`PlanIt.Web/scripts/screenshot-ui.mjs` is a pre-written, reusable capture tool — don't author
Playwright code from scratch for a normal verification run. It captures a URL across the standard
viewport matrix (desktop, iPhone 14 portrait/landscape, Pixel 8 portrait/landscape) using
Playwright's real device descriptors.

**No-interaction case (most changes)** — run it directly from `PlanIt.Web/`:

```bash
node scripts/screenshot-ui.mjs --url http://localhost:5173/some-page --out .playwright-screenshots --name <change-name>
```

- `--url` — the page to capture (must be reachable; see "Reaching the UI" below).
- `--out` — output directory (use `.playwright-screenshots`, already gitignored).
- `--name` — filename prefix for this batch, e.g. `board-column-reorder`.
- `--viewports` (optional) — comma list to restrict to a subset of `desktop`,
  `iphone-14-portrait`, `iphone-14-landscape`, `pixel-8-portrait`, `pixel-8-landscape`. Omit to
  capture all five; only narrow this down if the user explicitly asks to focus on or exclude a
  specific device/orientation.
- `--color-scheme` (optional) — `light` (default), `dark`, or `both`. Use `dark` or `both` when
  the change touches anything theme-aware (colors, `prefers-color-scheme` CSS, a theme toggle). If
  the app doesn't have a dark theme yet, light-only is fine.
- `--baseline` (optional) — a directory of previously saved screenshots to pixel-diff each new
  capture against, matched by filename (`<name>-<viewport>[-<colorScheme>].png`). Use this for
  changes that are supposed to be visually inert (refactors, non-visual logic changes touching
  shared components) to catch accidental regressions fast, instead of eyeballing every screenshot.
  Point it at a screenshot directory saved from a prior run (see "Baselines" below).
- `--wait-for-selector` (optional) — CSS selector to wait for after navigation before capturing.
  Prefer this over hoping the default wait catches a slow-loading page.
- `--wait-until` (optional) — navigation wait strategy passed to `page.goto` (`load` by default,
  or `domcontentloaded` / `networkidle` / `commit`). **Do not use `networkidle` once the app has a
  SignalR hub, WebSocket, or polling connection** — that keeps the network non-idle indefinitely
  and the capture will hang or time out. `load` + `--wait-for-selector` is the reliable
  combination for pages with a live connection.
- `--timeout` (optional) — timeout in ms for navigation and `--wait-for-selector` (default: 15000).

This prints the saved file paths, one per line, followed by any console errors, page errors, or
baseline diffs found for that capture. **The script exits non-zero if any console errors, page
errors, or baseline diffs were detected** — treat that as a signal something needs attention, not
just noise to scroll past.

**Interaction case** (the change requires a click, drag, form fill, or waiting on async content
before the UI is in the state you need to capture) — don't try to force it through CLI flags.
Instead write a tiny one-off script that imports the exported `captureUI` function and passes a
real `interact` callback with the full Playwright `page` API:

```js
import { captureUI } from './scripts/screenshot-ui.mjs'

const paths = await captureUI({
  url: 'http://localhost:5173/board',
  out: '.playwright-screenshots',
  name: 'card-drag-state',
  interact: async (page) => {
    await page.getByRole('button', { name: 'Add task' }).click()
    await page.waitForSelector('[role="dialog"]')
  },
})
console.log(paths)
```

Run it the same way (`node --input-type=module --eval "$(cat script.mjs)"` from `PlanIt.Web/`, or
save it as a temp `.mjs` file and `node` it directly — either works since it imports from the
project's own `node_modules`). Delete the one-off script when done; `screenshot-ui.mjs` itself
stays.

## Baselines

To diff against a prior state, keep a copy of a known-good screenshot set somewhere stable (e.g.
`.playwright-baselines/<change-or-component-name>/`, gitignored like `.playwright-screenshots/`)
and pass its path via `--baseline`. A typical flow for a refactor with no intended visual change:

1. Before touching code, capture the current UI once and copy that output directory aside as the
   baseline.
2. Make the code changes.
3. Capture again with `--baseline <path-to-step-1-copy>`.
4. Any reported `diff` means something visually changed — inspect the generated `*-diff.png` (it
   highlights the differing pixels) and the two source screenshots side by side to decide if it's
   expected or a regression.

Don't bother with a baseline for changes that are *supposed* to look different (new features,
intentional restyles) — diffing there just reports the expected change as noise. It earns its
keep specifically for "this should look identical" changes.

## Reaching the UI

1. Make sure the dev server is running: use the Browser pane's `preview_start` with
   `{name: "planit-web"}` (config already exists in `.claude/launch.json`, port 5173).
2. If the changed component isn't reachable through a normal user flow (e.g. a new modal, an
   in-progress page), add a temporary route or a small props harness that renders it directly —
   delete this before the final commit.

## Inspect

Two things to check per run, not just one:

1. **Console/network errors.** Check the script's stdout for `console errors` / `page errors`
   lines under each capture — these come from the real browser (`console.error`, uncaught
   exceptions, failed requests), not from reading the screenshot. A component can render pixel-
   perfect while silently failing underneath (a broken fetch, a React error swallowed by an error
   boundary) — this is the only way this skill catches that, so don't skip it even when the
   screenshots look fine.
2. **Visual state.** Read every saved screenshot (the `Read` tool renders images) and evaluate
   layout, content, and visual state for each viewport/scheme captured. Explicitly call out:
   - Broken or misaligned elements
   - Content cut off or pushed out of the visible viewport
   - Overlapping or eclipsing components
   - Dark-mode-specific issues if `--color-scheme dark`/`both` was used (unreadable contrast,
     elements that didn't pick up the theme, hardcoded light-mode colors)
   - Anything else that looks unexpected for that viewport/orientation

If a `--baseline` diff was requested, also open any `*-diff.png` produced and judge whether the
highlighted change was intended.

If something's wrong, fix the source and re-run capture + inspect. Repeat until the goal is met.

## Deliver

Once the UI is correct, send the final screenshot set to the user via `SendUserFile` — never just
describe it in prose — along with a short report of what changed and what was verified. This is a
standing expectation every time PlanIt.Web UI work is reported as done, scoped to what actually
changed rather than the whole app.

## Clean up

Before any commit (commits themselves remain user-triggered, never automatic):
- Delete any temporary route/harness added solely to reach the UI for verification.
- Delete any one-off interaction script written for the `captureUI` interaction case.
- Leave `PlanIt.Web/scripts/screenshot-ui.mjs` in place — it's a permanent project tool, reused
  across verifications.
