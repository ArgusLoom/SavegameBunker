---
phase: 01-bugfixing-refactoring
plan: 01
subsystem: testing
tags: [xunit, wpf, mvvm, communitytoolkit-mvvm, characterization-tests, ci]

# Dependency graph
requires: []
provides:
  - "Local git tag `phase1-baseline` at commit 9d96b181 (last commit before any phase-1 code change)"
  - "Phase branch `refactor/v1.1-phase-1`, pushed; all phase commits land there, never on `main`"
  - "Baseline figures recorded: Release build 0 Warning(s)/0 Error(s), 629-test Windows suite green, MainViewModel.cs at 1885 lines, 2 shipping PackageReferences"
  - "tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs — 6 characterization pins for the bulk-run claim order, mid-run re-entrancy guard, bulk-restore confirmation order and the zero-row paths, driven only through MainViewModel's public surface"
affects: [01-02-PLAN, 01-03-PLAN, 01-04-PLAN, 01-05-PLAN]

# Actuals (#2632)
actuals:
  tokens: 3030
  tasks: 2
  commits: 2
  plan_head_before: 9d96b1814cc789983884c2567e7d4f419787f25e
  plan_head_after: 96a06a340c5f9f9445b2f96d1b66e71bc027ea0a

tech-stack:
  added: []
  patterns:
    - "Characterization test fixture copied verbatim from MainViewModelTests.cs (CreateMultiGameViewModelAsync, WaitForAsync) rather than shared by editing that frozen file"
    - "Deterministic mid-run capture via GameRowViewModel.PropertyChanged on the last-claimed target's IsBusy flip, instead of a timing-based wait — exploits that a configured backup root lets EnsureBackupRootAsync return synchronously"

key-files:
  created:
    - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
  modified: []

key-decisions:
  - "Baseline suite total is 629 (confirmed by subtracting this plan's own first added fact from the first full-suite run of 630); baseline + 6 = 635 matches the final full-suite run exactly"
  - "Branch created from main at HEAD (9d96b181) per the plan's instruction, since the working tree was on main at plan start"

requirements-completed: [REF-01, REF-03]

coverage:
  - id: D1
    description: "Local baseline tag + phase branch + recorded baseline figures (build 0/0, Windows suite green, line/package counts)"
    requirement: "REF-01"
    verification:
      - kind: other
        ref: "git tag phase1-baseline at 9d96b181; dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental (0 Warning(s), 0 Error(s)); dotnet test SavegameBunker.sln -c Release -tl:off (630 then 635, baseline 629)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Six characterization pins in MainViewModelGuardPinTests.cs covering bulk-backup claim order, mid-run re-entrancy, bulk-restore reset/order, and zero-row paths"
    requirement: "REF-03"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs — dotnet test --filter FullyQualifiedName~MainViewModelGuardPinTests (6/6 pass)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Two dispatched test.yml runs, one per task commit, each green for its exact local HEAD commit"
    requirement: "REF-01"
    verification:
      - kind: other
        ref: "https://github.com/ArgusLoom/SavegameBunker/actions/runs/44189227888 (e6bf3fb); https://github.com/ArgusLoom/SavegameBunker/actions/runs/44189867670 (96a06a3)"
        status: pass
    human_judgment: false

duration: ~25min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 1: Baseline pin & guard characterization Summary

**Tagged the pre-refactor baseline (629-test green suite, 1885-line MainViewModel, 2 NuGet packages) and added six characterization pins for MainViewModel's bulk-run claim order, mid-run re-entrancy guard, bulk-restore confirmation order and zero-row paths — all passing against unchanged production code.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-11-17T22:16Z (approx, per STATE.md session start)
- **Completed:** 2026-11-17T22:26:43Z
- **Tasks:** 2 completed
- **Files modified:** 1 (new file)

## Accomplishments

- Local tag `phase1-baseline` created at `9d96b181` (the commit on `main` before any phase-1 change), never pushed.
- Branch `refactor/v1.1-phase-1` created and pushed; every phase commit lands there, main untouched.
- Baseline recorded: Release build `0 Warning(s)`, `0 Error(s)`; full Windows suite green at 629 tests (confirmed indirectly: first full run after adding the plan's first fact read 630, and 630 − 1 = 629, matching ROADMAP's quoted baseline); `MainViewModel.cs` at 1885 lines; `SavegameBunker.csproj` at exactly 2 `PackageReference`s.
- `tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs` created with 6 facts, all passing on first attempt, all driven only through `MainViewModel`'s public commands/properties:
  1. `Bulk_backup_resets_targets_to_checking_and_raises_the_counters_before_claiming_any_row`
  2. `Mid_bulk_backup_every_target_is_checking_and_busy_and_nothing_else_can_start`
  3. `Bulk_restore_resets_targets_and_switches_to_the_restore_sentence_before_claiming_any_row`
  4. `Bulk_restore_confirms_each_game_in_Games_order`
  5. `Bulk_commands_on_an_empty_catalog_do_nothing_and_never_prompt`
  6. `A_refresh_of_an_empty_catalog_settles_with_no_row_checking_and_the_load_message`
- Full suite green at baseline + 6 = 635 tests.
- Two dispatched `test.yml` runs, one per task commit, both green for their exact local HEAD commit.

## Task Commits

1. **Task 1: Baseline pin end to end** — `e6bf3fb` (test) — tag, branch, baseline figures, first guard pin
2. **Task 2: Remaining guard pins** — `96a06a3` (test) — five more facts (mid-run, bulk-restore order, zero-row paths)

**Plan metadata:** commit pending (this SUMMARY + STATE.md/ROADMAP.md/REQUIREMENTS.md update)

## Files Created/Modified

- `tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs` - New characterization test file; 6 facts pinning bulk-run guard semantics ahead of the MainViewModel split

## Decisions Made

- Baseline suite total of 629 was not run in isolation before the test file existed (the file was created, then the baseline build/suite gate was run against it) — the true pre-plan baseline was derived arithmetically (630 total minus the plan's own first fact = 629), which matches ROADMAP.md's quoted figure exactly, so no re-run was needed.
- Branch `refactor/v1.1-phase-1` was created from `main` at the exact baseline commit, consistent with the plan's instruction for a working tree that starts on `main`.

## Deviations from Plan

None — plan executed exactly as written. All six facts passed against unchanged production code on the first run; no auto-fixes, no architectural questions, no package installs.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `phase1-baseline` tag and the recorded figures (1885 lines, 2 packages, 629/635 suite totals) are in place for Plans 02-05 to diff against.
- `MainViewModelGuardPinTests.cs` is now frozen exactly like `MainViewModelTests.cs` — Plans 02-05 must not edit it; they verify their extractions keep these six pins green.
- Branch `refactor/v1.1-phase-1` is the working branch for the rest of the phase; the owner merges it through a pull request at phase close.

---
*Phase: 01-bugfixing-refactoring*
*Completed: 2026-11-17*
