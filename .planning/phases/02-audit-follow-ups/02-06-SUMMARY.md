---
phase: 02-audit-follow-ups
plan: 06
subsystem: ui
tags: [startup-prompt, concurrency, observableproperty, wpf, xunit]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: plan 02-05's reachable bulk-path picker-cancel fact (Back_up_selected_with_an_unset_backup_root_…) reused as the item-2 guard-fact arrangement
provides:
  - settled-sweep flag + prompt lock: RequestStartupUnsecuredPrompt evaluates at once when the first full sweep already settled, so a sweep finishing during ReportLoad's await can no longer push the prompt to a later mid-session Refresh
  - an empty catalog's first full sweep consumes an armed prompt (no later-Refresh prompt)
  - IsBulkPreparing: the bulk run's folder-probe/picker/catalog-write window counted in IsAnyOperationRunning and blocking row Backup/Restore, Refresh, Change backup folder, the other bulk commands and window close
  - RecordingDialogService.OnPickFolder hook — the deterministic observation point while a folder picker is open
affects: [02-08 (item 14 builds on IsBulkPreparing), 02-validate]

# Actuals (#2632)
actuals:
  tokens: 6900
  tasks: 2
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Arm/settle/consume behind one lock so the arm-then-settle and settle-then-arm orders both decide the prompt exactly once"
    - "Preparation-window guard: a pre-claim flag released in a finally before the coordinator's synchronous claim, so the guard and the claim never overlap"

key-files:
  created: []
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - src/SavegameBunker/App.xaml.cs
    - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
    - tests/SavegameBunker.Tests/MainViewModelTests.cs
    - CHANGELOG.md

key-decisions:
  - "The prompt decision is made at arming time when the first sweep already settled, at settle time otherwise — one _startupPromptLock makes the arm/settle pair total (never lost, never doubled) even where OnUi runs inline on pool threads"
  - "A full sweep over zero rows counts as settled, so an empty catalog consumes the armed prompt and a later Refresh never shows it"
  - "IsBulkPreparing is a separate observable flag, not a reuse of BulkBackupTotal or an early row-busy write — the coordinator's pinned claim order (reset to Checking → counters → IsBusy) stays untouched"

patterns-established:
  - "Tests observe gated state from a RecordingDialogService.OnPickFolder hook — no timing assumption around the picker"

requirements-completed: [ITEM-1, ITEM-2, D-13]

coverage:
  - id: D1
    description: "Arming after the first full sweep settled prompts at once without another sweep (ITEM-1)"
    requirement: "ITEM-1"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs#Arming_after_the_first_full_sweep_settled_prompts_without_another_sweep"
        status: unknown
    human_judgment: false
  - id: D2
    description: "An empty catalog's first sweep consumes the armed prompt; a later Refresh after adding a game never shows it (ITEM-1, research open question 10)"
    requirement: "ITEM-1"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs#An_empty_catalogs_first_sweep_consumes_the_armed_prompt_so_a_later_refresh_never_shows_it"
        status: unknown
    human_judgment: false
  - id: D3
    description: "At most one prompt per session holds under the new arm-evaluates contract, and the remaining prompt behaviours (unarmed no dialog, no-unsecured no dialog, decline/accept routing, disabled bulk command no-op, throwing Confirm reported) hold"
    requirement: "ITEM-1"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs (all facts, suite)"
        status: unknown
    human_judgment: false
  - id: D4
    description: "While Back up selected resolves its folder, IsAnyOperationRunning is true and row Backup/Restore, Refresh, Change backup folder, Back up all changed and Restore selected cannot execute; after a cancel they come back (ITEM-2)"
    requirement: "ITEM-2"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Back_up_selected_blocks_every_other_operation_while_its_folder_picker_is_open"
        status: unknown
    human_judgment: false
  - id: D5
    description: "Full test suite green (StartupUnsecuredPromptTests, MainViewModelGuardPinTests, MainViewModelTests, BulkBackupCoordinatorTests filters + full sln run)"
    verification: []
    human_judgment: true
    rationale: "Windows-only test runs — this Linux build-only host cannot execute the net10.0-windows xUnit/WPF suite (no Microsoft.WindowsDesktop.App). dotnet build Release is verified 0 Warnings / 0 Errors here; all test runs are result: [pending] on the owner's Windows machine."

# Metrics
duration: 24min
completed: 2026-11-18
status: complete
plan_head_before: 779e601514a82a7af49e78cdd089cf0ec05f4923
plan_head_after: fb63563
---

# Phase 2 Plan 6: Items 1 and 2 — startup prompt arm race and bulk preparation guard Summary

**The startup unsecured-games prompt is now decided at arming time when the first full sweep already settled (an empty catalog consumes it), and "Back up selected" holds IsBulkPreparing over its folder probe and picker so no row action, Refresh, backup-folder change or close can slip in beside it.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-11-18T20:52Z
- **Completed:** 2026-11-18T21:16Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- ITEM-1: `_firstFullSweepSettled` records the first full sweep's settle (the zero-row branch included), `RequestStartupUnsecuredPrompt` evaluates at once when it already has, and `MarkFirstFullSweepSettled` evaluates on settle — `_startupPromptLock` makes the arm/settle/consume trio total, so the prompt can be neither lost nor shown twice; at most once per session is unchanged.
- ITEM-1: every StartupUnsecuredPromptTests fact now sets its synthetic verdicts before arming and drops the explicit `PromptUnsecuredGamesOnce` call that followed (arming evaluates); two new facts pin arm-after-settle and the empty-catalog consumption.
- ITEM-2: `IsBulkPreparing` (an `[ObservableProperty]` with the same seven `[NotifyCanExecuteChangedFor]` entries as `BulkBackupTotal`) rises before `EnsureBackupRootAsync` and is released in a `finally` before the coordinator's synchronous claim; it counts in `IsAnyOperationRunning` and in `CanBackupRow`/`CanRestoreRow`.
- ITEM-2: `RecordingDialogService.OnPickFolder` hook + `Back_up_selected_blocks_every_other_operation_while_its_folder_picker_is_open` on the plan-02-05 picker arrangement (D-13) — every gate observed at the picker moment with no timing assumption.
- CHANGELOG `[Unreleased] ### Fixed`: two bullets (startup reminder; Back up selected overlap).
- App.xaml.cs comment corrected to the new contract (comment-only change, no code).

## Task Commits

Each task was committed atomically:

1. **Task 1: Item 1 — arming after the first sweep settled prompts at once** - `f04fdf3` (fix)
2. **Task 2: Item 2 — IsBulkPreparing guards the bulk run's folder probe and picker** - `fb63563` (fix)

_Both on `dev/v1.1`; HEAD moved from `779e601` (plan base) to `fb63563`._

## Files Created/Modified
- `src/SavegameBunker/ViewModels/MainViewModel.cs` — `_startupPromptLock`, `_firstFullSweepSettled`, `IsFirstFullSweepSettled`, `MarkFirstFullSweepSettled`, locked arming/consumption, `IsBulkPreparing`, `IsAnyOperationRunning` + row-gate terms, guarded `RunBulkBackupAsync`
- `src/SavegameBunker/App.xaml.cs` — comment above `vm.RequestStartupUnsecuredPrompt()` corrected
- `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` — contract reorder + two new facts; `CreateViewModelAsync` waits for `IsFirstFullSweepSettled`
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` — `OnPickFolder` hook, new guard fact
- `CHANGELOG.md` — two new Fixed bullets

## StartupUnsecuredPromptTests facts reordered (the contract change)
Every arming fact now arms **after** its synthetic `ApplyBackupComparison`/`IsBusy` set-up and no longer calls `PromptUnsecuredGamesOnce` right after arming:

- `No_unsecured_games_means_no_dialog` — reordered, explicit call dropped
- `Declining_the_prompt_starts_no_backup` — reordered, explicit call dropped
- `Accepting_the_prompt_runs_the_bulk_backup_for_the_unsecured_games` — reordered, explicit call dropped
- `Yes_does_not_start_the_backup_when_the_bulk_command_is_disabled` — reordered, explicit call dropped
- `Prompt_is_evaluated_at_most_once_per_session` — reordered, explicit call dropped; second evaluation kept as a second `RequestStartupUnsecuredPrompt()` plus the retained explicit `PromptUnsecuredGamesOnce()` re-call, still exactly one Confirm
- `A_throwing_Confirm_reports_the_failure_and_keeps_the_app_alive` — reordered (and now waits for `vm2.IsFirstFullSweepSettled`); its explicit `PromptUnsecuredGamesOnce()` re-call retained
- `An_unset_backup_root_never_reaches_the_prompt` — reordered, explicit call dropped
- `Unarmed_prompt_shows_no_dialog_even_with_unsecured_rows` — untouched, as planned
- New: `Arming_after_the_first_full_sweep_settled_prompts_without_another_sweep`, `An_empty_catalogs_first_sweep_consumes_the_armed_prompt_so_a_later_refresh_never_shows_it`

## Suite Total

**[pending]** — this Linux build-only host cannot execute the `net10.0-windows` suite; the totals for `dotnet test … StartupUnsecuredPromptTests|MainViewModelGuardPinTests|MainViewModelTests` (Task 1), `… MainViewModelTests|MainViewModelGuardPinTests|BulkBackupCoordinatorTests|StartupUnsecuredPromptTests` (Task 2) and the full `SavegameBunker.sln` run are the owner's Windows verification of record. Two new facts expect the Total to rise by 2 from the pre-plan figure.

## Decisions Made
- Zero-row sweep settles the prompt moment (research open question 10): `StartStatusScan`'s `snapshot.Count == 0` branch calls `MarkFirstFullSweepSettled()` when `isFullSweep`.
- `PromptUnsecuredGamesOnce` takes the lock only around the shown/pending check and the two consumption writes; the unsecured count, message and `Task.Run` with `Confirm` stay outside it, unchanged.
- Spec-less probe fallback skipped as planned: the phase has no requirement IDs to probe (visible skip).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Windows-only test runs cannot execute on this host; `dotnet test` filters from both tasks are **result: [pending]** for the owner's Windows machine. Verified here instead: `dotnet build SavegameBunker.sln -c Release` = 0 Warnings / 0 Errors, both tasks' grep acceptance criteria all pass (Task 1: `_firstFullSweepSettled`=5, `OnUi(MarkFirstFullSweepSettled)`=1, non-comment `OnUi(PromptUnsecuredGamesOnce)`=0, `lock (_startupPromptLock)`=4, new-fact grep=2, `IsFirstFullSweepSettled` in tests=4, CHANGELOG=1; Task 2: declaration=1, `IsBulkPreparing`=9, `[NotifyCanExecuteChangedFor(`=25, `OnPickFolder`=4, guard fact=1, CHANGELOG=1), and `git diff --quiet 57811c1 -- BulkBackupCoordinator.cs MainViewModelGuardPinTests.cs` = untouched.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Item 2 precedes item 14 (Plan 02-08, which replaces the attribute list with `RequeryOperationGates()`) — `IsBulkPreparing` is in place with the same notify list `BulkBackupTotal` carries.
- The bulk coordinator's claim order and its guard pins are provably untouched since `57811c1`.
- No stubs introduced; shipping csproj unchanged (two PackageReferences).

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
