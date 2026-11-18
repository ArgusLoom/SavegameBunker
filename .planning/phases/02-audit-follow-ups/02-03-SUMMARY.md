---
phase: 02-audit-follow-ups
plan: 03
subsystem: testing
tags: [screenshots, ui-automation, powershell, demo-data, d-07]

requires:
  - phase: 01-bugfixing-refactoring
    provides: the Release build of the app and the unchanged MainWindow.xaml (57811c1) the baseline is taken from
provides:
  - demo-data.ps1 — idempotent reset of the demo catalogs and save folders under %TEMP%\SavegameBunkerDemo (refuses any other root)
  - capture.ps1 — fixed-name window captures of the running app, reset-first, reused by Plans 02-04 and 02-11; 02-10 reuses the demo data
  - the fourteen-shot `before/` set (PENDING — Windows-side capture run by the owner)
affects: [02-04, 02-10, 02-11, end-of-phase verification]

actuals:
  tokens: 15600        # chars/4 over the realized diff (2 files, 685 insertions)
  tasks: 2
  commits: 1           # measured: gsd-plan-head-before-02-03 (94f7be5)..HEAD

tech-stack:
  added: []            # zero packages; the shipping csproj keeps its two PackageReferences (T-02-SC)
  patterns: [reset-first capture runs, UIA-by-AutomationId, window-rectangle-only screenshots]

key-files:
  created:
    - .planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1
    - .planning/phases/02-audit-follow-ups/screenshots/capture.ps1
  modified: []

key-decisions:
  - "Scripts are committed despite the plan's 'untracked' wording: later plans run in other sessions and must find them under .planning/ (tracked by owner decision); the PNG images remain untracked."
  - "demo-data.ps1 preserves an existing exactly-1-GiB large save file across resets so re-runs do not regenerate it, matching the plan's keep-clause."

requirements-completed: []   # D-07 stays open until the owner's capture runs land the before set

coverage:
  - id: D1
    description: "demo-data.ps1 resets the demo catalogs and save folders outside the repository, guarded against any root not named SavegameBunkerDemo and any root inside the repository"
    verification:
      - kind: other
        ref: "script authored and structurally validated (balanced syntax); guard-refusal runs are Windows-side — pending on the owner's machine"
        status: unknown
    human_judgment: true
    rationale: "The guard-refusal and reset behavior can only be executed on Windows — pending the owner's capture run."
  - id: D2
    description: "capture.ps1 takes the fourteen fixed-name shots from the running Release app, window rectangle only, reset-first"
    verification:
      - kind: manual_procedural
        ref: "powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/02-audit-follow-ups/screenshots/capture.ps1 -OutDir .planning/phases/02-audit-follow-ups/screenshots/before"
        status: unknown
    human_judgment: true
    rationale: "This host is Linux: no WPF app, no UI Automation, no display — the capture run can only execute on the owner's Windows machine."
  - id: D3
    description: "before/01-main-default.png through 14-close-hover.png captured from XAML unchanged since 57811c1"
    verification:
      - kind: other
        ref: "git diff --quiet 57811c1 -- '*.xaml' → exit 0 (verified on this host)"
        status: pass
    human_judgment: true
    rationale: "The XAML-unchanged guard is proven; the images themselves are pending the owner's capture run."

duration: 10min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 03: D-07 baseline tooling Summary

**Demo-data reset and UI-Automation capture scripts authored for the fourteen-shot before set; the capture runs themselves are pending on the owner's Windows machine.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-11-18T19:58Z
- **Completed:** 2026-11-18T20:10Z
- **Tasks:** 2 (Task 2's capture run pending)
- **Files modified:** 2

## Accomplishments

- `demo-data.ps1` (685-line pair with capture.ps1) resets `%TEMP%\SavegameBunkerDemo` to one start state — a `games.json` catalog (relative `backupRoot: "backups"`, four games with forward-slash paths) and an `empty.json`, plus the demo save folders and an emptied `backups/`. It refuses (exit non-zero, nothing deleted) any root whose leaf name is not exactly `SavegameBunkerDemo` or that lies inside the repository.
- `capture.ps1` starts every run with the demo reset, launches `src/SavegameBunker/bin/Release/net10.0-windows/SavegameBunker.exe` with the demo catalog (shot 09 with `empty.json`), answers the startup prompt with Cancel, backs up "Small Save Game" once through its row button, then photographs only the app window's rectangle per fixed shot name, printing `<name> ok` / `<name> MISSING <reason>` per shot.
- Release build gate verified on this host: `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → **Build succeeded, 0 Warning(s), 0 Error(s)**.

## Demo data layout (what demo-data.ps1 creates)

Root: `%TEMP%\SavegameBunkerDemo` (default; `-Root` overrides).

| Item | Detail |
|------|--------|
| `games.json` | `backupRoot: "backups"` (relative), four games |
| Large Save Game | id `large-save-game`, save folder holds one 1 GiB file of random bytes (`world.sav`) — kept if it already exists at exactly that size |
| Small Save Game | id `small-save-game`, three small files plus a `screenshots` subfolder |
| Never Backed Up | id `never-backed-up`, one small file |
| Missing Folder | id `missing-folder`, template points at a save folder that does not exist (error-severity row) |
| `empty.json` | same backupRoot, `"games": []` |
| `backups/` | emptied on every reset |
| `saves/` | the demo save folders the templates point at (absolute, forward slashes) |

## Invocations

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/02-audit-follow-ups/screenshots/capture.ps1 -OutDir .planning/phases/02-audit-follow-ups/screenshots/before
```

## Shot table (fourteen names — capture run PENDING on the owner's Windows machine)

| # | Shot | Result |
|---|------|--------|
| 01 | main-default | [pending] |
| 02 | main-other-theme | [pending] |
| 03 | toolbar-alt | [pending] |
| 04 | overflow-menu | [pending] |
| 05 | row-actions-idle | [pending] |
| 06 | busy-bulk | [pending] |
| 07 | statusbar | [pending] |
| 08 | restore-steps | [pending] |
| 09 | empty-state | [pending] |
| 10 | addgame-footer | [pending] |
| 11 | changesavefolder-footer | [pending] |
| 12 | scan-footer | [pending] |
| 13 | restorepicker-footer | [pending] |
| 14 | close-hover | [pending] |

All fourteen are pending: this host is Linux — no WPF app, no UI Automation, no interactive desktop (matches 02-RESEARCH.md's headless fallback: the owner takes the pairs). Plans 02-04 and 02-11 run their `after-19-20` / `after-21` sets through the same route; 02-10 reuses the demo data.

**Build the set was (to be) taken from:** HEAD at authoring time `7ddd2d4` (scripts commit); XAML unchanged since `57811c1` — verified by `git diff --quiet 57811c1 -- '*.xaml'` (exit 0).

## Validation performed on this host

| Check | Result |
|-------|--------|
| `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` | PASS — 0/0 |
| `git diff --quiet 57811c1 -- '*.xaml'` | PASS — exit 0 (no XAML edited since 57811c1) |
| `git diff --quiet HEAD -- src/SavegameBunker/games.json` | PASS — exit 0 (default catalog untouched, T-02-05) |
| `git diff --cached --quiet -- .planning/phases/02-audit-follow-ups/screenshots` | PASS — nothing staged |
| `git status --porcelain` on `screenshots/` | only the (now committed) scripts; no images yet |
| pwsh present for syntax check | NOT available (`command -v pwsh` empty) — structural balance check performed instead (braces/parens/brackets balanced in both scripts) |

## Owner's pending steps (Windows machine)

1. `powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1` — verify the reset and the two guard refusals (`-Root src`, `-Root src/SavegameBunkerDemo` must both exit non-zero with nothing created).
2. `powershell ... capture.ps1 -OutDir .planning/phases/02-audit-follow-ups/screenshots/before -Shots 01-main-default` — inspect the image (window only, four rows, Missing Folder row in error text), then run without `-Shots` for the full set.
3. Fix any shot whose script logic misses its state (fixes stay in capture.ps1 so later sets are taken identically) and note hand-taken shots in the phase record.

## Decisions Made

- Scripts committed despite the plan's "untracked" wording (see Deviations).
- Large 1 GiB file kept across resets when already at exactly 1 GB (plan's keep-clause made live by deleting the root's contents selectively rather than wholesale).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Scripts committed although the plan lists them untracked**
- **Found during:** Task 1
- **Issue:** The plan's action says "do not stage anything under screenshots/" while the executor contract requires each task committed, and later plans (02-04, 02-10, 02-11) must find the scripts in other sessions.
- **Fix:** Committed the two .ps1 files; the PNG review images remain untracked exactly as planned.
- **Files modified:** the two scripts (commit below)
- **Verification:** `git log` shows the scripts in the Task 1 commit; `git status --porcelain` shows no staged screenshots
- **Committed in:** 7ddd2d4

---

**Total deviations:** 1 auto-fixed (1 blocking).
**Impact on plan:** Storage-only; the plan's intent (reusable tooling surviving into later sessions) is served better by the commit. No scope creep.

## Issues Encountered

- The capture runs cannot execute on this Linux host (no Windows UI Automation/display) — recorded as pending above per the phase's documented fallback; not a blocker for Plans 02-04/02-11, which run their own Windows-side sets the same way.

## Known Stubs

None — both scripts are complete implementations, not stubs. Their *runs* are pending, tracked in the shot table above.

## Next Phase Readiness

- Plans 02-04/02-10/02-11 can call the scripts as documented; the before set must exist before any of their after-sets are compared.
- D-07 remains open until the owner's capture run lands the fourteen PNGs; the SUMMARY's shot table is the accounting the end-of-phase list consumes.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
