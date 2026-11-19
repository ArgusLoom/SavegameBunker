---
phase: 02-audit-follow-ups
plan: 15
subsystem: testing
tags: [xunit, test-split, mainviewmodel]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: "TestWait named constants and TempDir shared fixture (Plans 02-12/02-13/02-14)"
provides:
  - "MainViewModelTestBase: the shared fixture (_temp/_dir/_suggestedStartFolder) and the seven cross-section builder helpers (CreateViewModel, CreateViewModelWithGames, CreateNamedViewModel, CreateViewModelWithSavedCatalogAsync, SaveFilePath, SeedBackup, CreateMultiGameViewModelAsync)"
  - "Six new focused test classes (Restore, Busy, Bulk, StatusLine, RowList, StartupHealth), all `[Collection(\"MainViewModel\")]` alongside the now much-shorter MainViewModelTests"
  - "Proof that the split lost nothing: Total (739) unchanged, sorted test-name list byte-identical, per-class Fact/Theory counts sum to the pre-split 159, five consecutive full-suite runs green"
affects: [testing]

# Actuals (#2632)
actuals:
  tokens: 60939
  tasks: 3
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns: ["one abstract test base shared by several sealed test classes, all pinned to one serial xUnit collection so a multi-class split does not add parallel load"]

key-files:
  created:
    - tests/SavegameBunker.Tests/MainViewModelTestBase.cs
    - tests/SavegameBunker.Tests/MainViewModelRestoreTests.cs
    - tests/SavegameBunker.Tests/MainViewModelBusyTests.cs
    - tests/SavegameBunker.Tests/MainViewModelBulkTests.cs
    - tests/SavegameBunker.Tests/MainViewModelStatusLineTests.cs
    - tests/SavegameBunker.Tests/MainViewModelRowListTests.cs
    - tests/SavegameBunker.Tests/MainViewModelStartupHealthTests.cs
  modified:
    - tests/SavegameBunker.Tests/MainViewModelTests.cs

key-decisions:
  - "All moves were scripted line-range slices (Python, run from a scratch directory, never retyped) driven by the section markers already in the file, verified against asserted line-content checks before any write — the mechanical-move acceptance criterion from the plan's threat model (T-02-25)"
  - "The _temp field (TempDir) stayed private in the base per the plan's explicit CS0052 guard — TempDir is internal, so a protected member of the public abstract base cannot expose it; _dir and _suggestedStartFolder are protected readonly strings instead"
  - "All seven split files share one [Collection(\"MainViewModel\")] with no definition class, keeping them serial exactly as the single class was (T-02-26 — avoids parallel load raising the real-I/O WaitForAsync flake risk)"

patterns-established:
  - "Pattern: a 3,000+-line xUnit test file splits along its own `// --- ... ---` section markers into one abstract *TestBase (fixture + cross-section helpers, non-sealed for that reason) and several sealed *Tests classes, all pinned to the same named xUnit collection"

requirements-completed: [ITEM-23, D-08]

coverage:
  - id: D1
    description: "MainViewModelTestBase holds the shared fixture and all seven cross-section helpers; MainViewModelTests derives from it and joins [Collection(\"MainViewModel\")]"
    requirement: "ITEM-23"
    verification:
      - kind: unit
        ref: "grep -c \"public abstract class MainViewModelTestBase : IDisposable\" tests/SavegameBunker.Tests/MainViewModelTestBase.cs -> 1"
        status: pass
      - kind: unit
        ref: "dotnet test tests/SavegameBunker.Tests --filter FullyQualifiedName~MainViewModelTests (Task 1, mid-split) -> 159/159 passed"
        status: pass
    human_judgment: false
  - id: D2
    description: "The RestoreCommand, busy-scoping/busy-under-concurrency, and Back-up-selected/Restore-selected sections moved into MainViewModelRestoreTests, MainViewModelBusyTests and MainViewModelBulkTests respectively, all deriving from MainViewModelTestBase and sharing the collection"
    requirement: "ITEM-23"
    verification:
      - kind: unit
        ref: "grep -l '[Collection(\"MainViewModel\")]' tests/SavegameBunker.Tests/*.cs | wc -l -> 4 (after Task 2)"
        status: pass
      - kind: integration
        ref: "dotnet test tests/SavegameBunker.Tests --filter FullyQualifiedName~MainViewModelTests|...RestoreTests|...BusyTests|...BulkTests -> 159/159 passed"
        status: pass
    human_judgment: false
  - id: D3
    description: "The free-space-advisory/composed-StatusLine, sorted-insert/drop-folder, and startup-backup-root-health sections moved into MainViewModelStatusLineTests, MainViewModelRowListTests and MainViewModelStartupHealthTests; MainViewModelTests.cs now keeps only the opening section (556 lines, down from 3,272 at the plan's baseline commit)"
    requirement: "ITEM-23"
    verification:
      - kind: unit
        ref: "grep -l '[Collection(\"MainViewModel\")]' tests/SavegameBunker.Tests/*.cs | wc -l -> 7; grep -c '^    // --- ' MainViewModelTests.cs -> 0; wc -l MainViewModelTests.cs -> 556 (<= 800)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The split lost nothing: full-suite Total is 739 before and after; the sorted public-test-name list across all test files is byte-identical (678 names, diff empty); the Fact/Theory counts of the seven MainViewModel classes sum to the pre-split MainViewModelTests.cs count of 159"
    requirement: "ITEM-23"
    verification:
      - kind: integration
        ref: "dotnet test SavegameBunker.sln --no-build -> 739/739 passed, before and after the three-commit split"
        status: pass
      - kind: other
        ref: "diff names-before.txt names-after.txt (grep -ohE \"public (async Task|void) [A-Za-z0-9_]+\\(\" tests/SavegameBunker.Tests/*.cs | sort) -> empty"
        status: pass
      - kind: other
        ref: "sum of grep -cE \"^\\s*\\[Fact\\]|^\\s*\\[Theory\\]\" across the seven MainViewModel*.cs files -> 159"
        status: pass
    human_judgment: false
  - id: D5
    description: "The suite passes five consecutive full-suite runs after the split (D-08: dev/v1.1 stays green; no src/ file changed by this plan)"
    requirement: "D-08"
    verification:
      - kind: integration
        ref: "dotnet test SavegameBunker.sln -c Release --no-build (5 consecutive runs) -> 739/739 passed every time"
        status: pass
      - kind: other
        ref: "git diff --stat <commit before Task 1>..<Task 3 commit> -- src/ -> empty"
        status: pass
    human_judgment: false

duration: 55min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 15: MainViewModel Test Split Summary

**MainViewModelTests.cs (3,272 lines at the phase's `57811c1` baseline) is split along its existing section markers into one abstract `MainViewModelTestBase` plus six focused classes, all sharing `[Collection("MainViewModel")]` so they stay serial; nothing was lost (Total 739, 678 test names, 159 Fact methods — all identical before and after) and five consecutive full-suite runs pass.**

## Performance

- **Duration:** 55 min
- **Started:** 2026-11-18T22:10:00Z (approx.)
- **Completed:** 2026-11-18T23:05:19Z
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments
- Created `MainViewModelTestBase` (abstract, `IDisposable`): the fixture (`TempDir _temp` stays private per CS0052; `_dir`/`_suggestedStartFolder` are `protected readonly`) and the seven cross-section helpers (`CreateViewModel`, `CreateViewModelWithGames`, `CreateNamedViewModel`, `CreateViewModelWithSavedCatalogAsync`, `SaveFilePath`, `SeedBackup`, `CreateMultiGameViewModelAsync`), each changed only from `private` to `protected`/`protected static` — no logic touched.
- Split `MainViewModelTests.cs` (3,272 lines at baseline) into seven classes, all `[Collection("MainViewModel")]` and `: MainViewModelTestBase`:
  - `MainViewModelTests` (556 lines) — the opening section: fixture construction facts, Find Games, scan gating.
  - `MainViewModelRestoreTests` (1,006 lines) — RestoreCommand, the busy gate, confirmation copy, distinct errors.
  - `MainViewModelBusyTests` (472 lines) — busy scoping and busy state under concurrency.
  - `MainViewModelBulkTests` (636 lines) — "Back up selected" and "Restore selected".
  - `MainViewModelStatusLineTests` (230 lines) — the free-space advisory and the composed StatusLine.
  - `MainViewModelRowListTests` (177 lines) — sorted insert on screen and dropping a folder into Add Game.
  - `MainViewModelStartupHealthTests` (81 lines) — startup backup-root health.
- Every move was a scripted, line-range slice (Python, run from a scratch directory outside the repo) driven by the file's own `// --- ... ---` markers — never retyped. Each cut was preceded by an assertion of the exact expected line content at every boundary; the scripts refused to run on a mismatch.
- Proved nothing was lost: full-suite Total is 739 both before (baseline) and after the three commits; the sorted list of `public (async Task|void)` test method names across all test files (678 entries) diffs empty; the Fact/Theory counts of the seven MainViewModel classes (31+46+25+31+12+10+4) sum to the pre-split count of 159.
- Ran the mandatory five-consecutive-run flake check after the final commit: all five runs green, `739/739` every time.

## Task Commits

Each task was committed atomically:

1. **Task 1: MainViewModelTestBase end to end** - `c8db4ef` (test)
2. **Task 2: Move the restore, busy and bulk sections into their own classes** - `9349860` (test)
3. **Task 3: Move the status-line, row-list and startup-health sections; prove the split lost nothing** - `9e4289c` (test)

_No separate plan-metadata commit — `commit_docs: false` in `.planning/config.json`; per the dispatch instruction for this plan, STATE.md/ROADMAP.md updates are owned by the orchestrator._

## Pre/Post Proof

| Measure | Before (baseline, pre-Task-1) | After (post-Task-3) |
|---|---|---|
| Full-suite Total | 739 | 739 |
| Sorted test-name list (all files, 678 entries) | saved to scratch | diff against saved list: **empty** |
| MainViewModelTests.cs Fact/Theory count | 159 / 0 | split across 7 classes: 31+46+25+31+12+10+4 = **159** |
| MainViewModelTests.cs line count | 3,272 (at `57811c1`) / 3,238 (at session start, post Plans 02-07..02-14) | 556 |
| Five consecutive full-suite runs | n/a | **5/5 green**, 739/739 each run |

## Files Created/Modified
- `tests/SavegameBunker.Tests/MainViewModelTestBase.cs` - new abstract base: fixture + 7 cross-section helpers
- `tests/SavegameBunker.Tests/MainViewModelRestoreTests.cs` - new: the RestoreCommand section
- `tests/SavegameBunker.Tests/MainViewModelBusyTests.cs` - new: busy scoping + busy state under concurrency
- `tests/SavegameBunker.Tests/MainViewModelBulkTests.cs` - new: "Back up selected" + "Restore selected"
- `tests/SavegameBunker.Tests/MainViewModelStatusLineTests.cs` - new: free-space advisory + composed StatusLine
- `tests/SavegameBunker.Tests/MainViewModelRowListTests.cs` - new: sorted insert + drop folder into Add Game
- `tests/SavegameBunker.Tests/MainViewModelStartupHealthTests.cs` - new: startup backup-root health
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` - reduced to the opening section only (3,272 → 556 lines); now derives from `MainViewModelTestBase`

## Decisions Made
- `_temp` (the `TempDir` fixture field) stays `private` in the base class rather than becoming `protected`, because `TempDir` is `internal` and a `protected` member of a `public` type cannot have a less-accessible type (CS0052). `_dir` and `_suggestedStartFolder` (both `string`) are `protected readonly` instead, and the base's constructor/`Dispose()` own `_temp` entirely — no derived class ever touches it directly.
- All moves used scripted, mechanical line-range extraction (never hand-retyped), with boundary assertions checked against the live file content before each cut — this is what "reviewed with git diff --color-moved" meant in practice: the diff stat for every commit shows insertions roughly equal to deletions plus only the new files' scaffolding (usings/doc comment/class declaration/closing brace), confirming test bodies moved, not were rewritten.
- Section pairs that were non-contiguous in the original file (busy scoping + busy-under-concurrency; back-up-selected + restore-selected; free-space-advisory + composed-StatusLine; sorted-insert + drop-folder) were concatenated into their single target file in file order, exactly as the plan's interface table specified.

## Deviations from Plan

None - plan executed exactly as written. All three tasks' acceptance criteria and the plan's own `<verification>` block passed on the first attempt per task; no auto-fix, blocker, or architectural deviation was needed.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ITEM-23 closed: seven focused MainViewModel test classes share one base and one serial collection; nothing was lost.
- D-08 satisfied for this plan: `dev/v1.1` stays green (739/739, five consecutive runs), no `src/` file was touched by this plan. This was the last plan in Phase 02 (audit-follow-ups) — CI on the pull request to `main` remains the owner's next gate, as the phase's `<success_criteria>` specifies.
- No blockers.

## Self-Check: PASSED

- FOUND: tests/SavegameBunker.Tests/MainViewModelTestBase.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelRestoreTests.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelBusyTests.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelBulkTests.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelStatusLineTests.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelRowListTests.cs
- FOUND: tests/SavegameBunker.Tests/MainViewModelStartupHealthTests.cs
- FOUND: commit c8db4ef (ancestor of HEAD)
- FOUND: commit 9349860 (ancestor of HEAD)
- FOUND: commit 9e4289c (ancestor of HEAD)

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
