---
phase: 02-audit-follow-ups
plan: 08
subsystem: ui
tags: [mvvm, communitytoolkit, observableproperty, bulk-progress, xunit]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: plan 02-06's IsBulkPreparing flag (same notify-list shape RequeryOperationGates replaces)
provides:
  - "RequeryOperationGates(): one private method replacing four drifted NotifyCanExecuteChangedFor attribute lists on BulkBackupTotal, IsBulkPreparing, IsBackupRunning and IsRestoreRunning"
  - "A busy row now re-queries its own BackupCommand/RestoreCommand (previously missing from RegisterRow's IsBusy branch)"
  - "BulkBackupCoordinator.BackupAsync reports progress through Action<int, string> onItem routed via _onUi — the same shape RestoreAsync already used"
  - "Bulk backup's late-arrival progress guard and its test wait are gone; the counters settle deterministically inside the run"
affects: [02-validate]

# Actuals (#2632)
actuals:
  tokens: 5425
  tasks: 2
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "One private requery method per gate-family, hooked from partial void On*Changed, instead of per-property NotifyCanExecuteChangedFor attribute lists that can drift independently"
    - "Progress callback shape unified across bulk backup and bulk restore: Action<int, string> invoked through the same onUi marshaller as every other row write in the loop"

key-files:
  created: []
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
    - tests/SavegameBunker.Tests/MainViewModelTests.cs
    - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs

key-decisions:
  - "RequeryOperationGates() notifies exactly the seven commands whose CanExecute reads an operation flag or row.IsBusy (Refresh, ChangeBackupRoot, BackupAllChanged, BackupSelected, RestoreSelected, Backup, Restore); IsScanRunning keeps its own single FindGamesCommand notification since CanFindGames reads only that flag"
  - "RegisterRow's IsBusy branch now calls RequeryOperationGates() instead of five hand-written NotifyCanExecuteChanged calls, which also fixes the pre-existing gap where BackupCommand/RestoreCommand were never notified on a row going busy even though CanBackupRow/CanRestoreRow read row.IsBusy"
  - "BulkBackupCoordinator.BackupAsync's onItem is invoked via _onUi before that row's write begins, removing the need for MainViewModel's late-arrival guard (if (!IsBulkBackupRunning) return;) entirely"

patterns-established:
  - "A gate-family's CanExecuteChanged notification lives in one method, called from every partial void On*Changed hook for that family's flags — not duplicated per [ObservableProperty]"

requirements-completed: [ITEM-14, ITEM-15]

coverage:
  - id: D1
    description: "One RequeryOperationGates() replaces four drifted NotifyCanExecuteChangedFor lists (ITEM-14)"
    requirement: "ITEM-14"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Setting_row_IsBusy_raises_CanExecuteChanged_on_the_row_Backup_and_Restore_commands"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Setting_row_IsBusy_raises_CanExecuteChanged_on_the_toolbar_commands"
        status: pass
    human_judgment: false
  - id: D2
    description: "A busy row now re-queries its own BackupCommand/RestoreCommand (the one deliberate enablement-only behavior change of ITEM-14)"
    requirement: "ITEM-14"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Setting_row_IsBusy_raises_CanExecuteChanged_on_the_row_Backup_and_Restore_commands"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every existing MainViewModelGuardPinTests pin (claim order, re-entrancy, zero-row paths) stays green unchanged, and the file is byte-identical to baseline 57811c1"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs (all facts, suite)"
        status: pass
      - kind: other
        ref: "git diff --quiet 57811c1 -- tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs"
        status: pass
    human_judgment: false
  - id: D4
    description: "Bulk backup reports each game's progress before writing its archive, via onItem routed through _onUi, matching restore's existing shape (ITEM-15)"
    requirement: "ITEM-15"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs#BackupAsync_reports_each_game_before_writing_its_archive"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs#BackupAsync_routes_every_loop_row_write_through_onUi"
        status: pass
    human_judgment: false
  - id: D5
    description: "The late-arrival progress guard and its non-deterministic test wait are gone; the bulk-progress counters clear deterministically with no WaitForAsync needed"
    requirement: "ITEM-15"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Bulk_backup_clears_its_progress_state_before_reporting_its_outcome"
        status: pass
    human_judgment: false
  - id: D6
    description: "Full test suite green after both items, including the human-observable window-level progress panel behavior during a real bulk backup run"
    verification:
      - kind: unit
        ref: "dotnet test SavegameBunker.sln -c Release (738 total)"
        status: pass
      - kind: manual_procedural
        ref: "Run the app against 3+ games including one with a 500MB+ save folder and click Back up all changed"
        status: unknown
    human_judgment: true
    rationale: "The task's <human-check> ('the window-level progress panel counts up game by game... disappears before the outcome message') requires visual/interactive observation on a running Windows app with a dispatcher; no human was available in this execution session, so this item is noted as not run rather than silently skipped."

# Metrics
duration: 32min
completed: 2026-11-18
status: complete
plan_head_before: a3e858c9f1f83bdb7dee9f5d5711c23935539cef
plan_head_after: b122f1306af16ab6d1a0debf0afb7c197a63a48c
---

# Phase 2 Plan 8: Items 14 and 15 — one operation-gate requery and one bulk-progress callback shape Summary

**MainViewModel's four drifted `[NotifyCanExecuteChangedFor]` attribute lists collapse into one `RequeryOperationGates()` method (and a busy row now correctly re-queries its own Backup/Restore commands), and `BulkBackupCoordinator.BackupAsync` reports progress through the same `Action<int, string>` onUi-routed callback shape bulk restore already used, removing the late-arrival guard entirely.**

## Performance

- **Duration:** 32 min
- **Started:** 2026-11-18T21:23:56Z
- **Completed:** 2026-11-18T21:56Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- ITEM-14: `RequeryOperationGates()` notifies the seven operation-gated commands (Refresh, ChangeBackupRoot, BackupAllChanged, BackupSelected, RestoreSelected, Backup, Restore); `BulkBackupTotal`, `IsBackupRunning`, `IsRestoreRunning` and `IsBulkPreparing` call it from partial `On…Changed` hooks and carry no `[NotifyCanExecuteChangedFor]` list any more.
- ITEM-14: `RegisterRow`'s `IsBusy` branch now calls `RequeryOperationGates()` too, so `BackupCommand`/`RestoreCommand` re-query when a row goes busy — the one deliberate, enablement-only behavior change this item makes (pinned by a new fact).
- ITEM-14: `IsScanRunning` keeps its single `[NotifyCanExecuteChangedFor(nameof(FindGamesCommand))]`; the narrower `ComparisonKind`, `Games.CollectionChanged` and `IsSelected` notifications are untouched.
- ITEM-15: `BulkBackupCoordinator.BackupAsync` reports each game through an `Action<int, string> onItem` invoked via `_onUi`, the same marshaller every other row write in the loop uses; `MainViewModel.RunBulkBackupAsync` passes `SetBulkProgress` directly and the late-arrival guard (`if (!IsBulkBackupRunning) return;`) is gone.
- ITEM-15: bulk-progress state stays in `MainViewModel` (XAML still binds it) and both empty-target checks stay in place.
- ITEM-15: a new coordinator fact (`BackupAsync_reports_each_game_before_writing_its_archive`) pins that a progress report arrives before that row's own archive exists on disk; the claim-order pins in `MainViewModelGuardPinTests` stay green unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: ITEM-14 end to end — one RequeryOperationGates, reached from every operation flag and from a row going busy** - `e343991` (refactor)
2. **Task 2: ITEM-15 — bulk backup reports progress through Action<int, string> on _onUi** - `b122f13` (refactor)

_Both on `dev/v1.1`; HEAD moved from `a3e858c` (plan base) to `b122f13`._

## Files Created/Modified
- `src/SavegameBunker/ViewModels/MainViewModel.cs` — `RequeryOperationGates()`, four `partial void On*Changed` hooks, `RegisterRow`'s `IsBusy` branch simplified, `RunBulkBackupAsync`'s `Progress<(int,string)>` instance and late-arrival guard removed
- `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs` — `BackupAsync`'s `progress` parameter replaced with `Action<int, string> onItem`, routed through `_onUi`
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` — new IsBusy→Backup/Restore fact; `Bulk_backup_clears_its_progress_state_before_reporting_its_outcome` no longer waits on the count/name counters
- `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs` — `RecordingIndexProgress` deleted; every `BackupAsync` call site migrated to the new signature; new fact pins the report-before-write order; `BackupAsync_routes_every_loop_row_write_through_onUi`'s expected count raised from 6 to 8

## Suite Total

**738 passed, 0 failed** (`dotnet test SavegameBunker.sln -c Release -tl:off`). Release build: 0 Warnings / 0 Errors. Filtered runs also green:
- Task 1 filter (`MainViewModelTests|MainViewModelGuardPinTests|StartupUnsecuredPromptTests`): 174/174
- Task 2 filter (`BulkBackupCoordinatorTests|MainViewModelTests|MainViewModelGuardPinTests`): 174/174
- Baseline before this plan (same Task 1 filter): 173/173

## Decisions Made
- `RequeryOperationGates()` is the one list of commands whose `CanExecute` reads an operation flag or a row's busy state, replacing four attribute lists that had already drifted to 7/5/5/1 entries (and were missing `BackupCommand`/`RestoreCommand` entirely from the row-`IsBusy` branch).
- `IsScanRunning`'s own `FindGamesCommand` notification is deliberately not folded into `RequeryOperationGates()` — `CanFindGames` reads only `IsScanRunning`, so it stays its own narrow notification.
- `BulkBackupCoordinator.BackupAsync`'s `onItem` callback is invoked through `_onUi` exactly like the loop's other row writes, which is what makes the late-arrival guard unnecessary rather than merely redundant.
- Spec-less probe fallback skipped as planned: the phase has no requirement IDs to probe (visible skip).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None. Both tasks' acceptance criteria all passed on the first implementation (no retries needed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ITEM-14 and ITEM-15 are closed; `MainViewModelGuardPinTests.cs` remains byte-identical to baseline `57811c1`, confirming the bulk coordinator's claim order was never touched.
- No stubs introduced; shipping csproj unchanged (two PackageReferences).
- The human-check in Task 2's `<verify>` block (visual confirmation of the window-level progress panel during a real bulk backup run against a 500MB+ save folder) was **not run** — no interactive Windows session was available in this execution. This is informational only, per the dispatch instructions, and does not block the plan.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*

## Self-Check: PASSED
- FOUND: .planning/phases/02-audit-follow-ups/02-08-SUMMARY.md
- FOUND: e343991 (ancestor of HEAD)
- FOUND: b122f13 (ancestor of HEAD)
