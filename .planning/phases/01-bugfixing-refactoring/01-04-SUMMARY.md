---
phase: 01-bugfixing-refactoring
plan: 04
subsystem: ui
tags: [wpf, mvvm, refactoring, coordinator, xunit, scan, warnings]

# Dependency graph
requires:
  - phase: 01-03
    provides: "BulkBackupCoordinator shape (internal sealed, OnUi handed in as Action<Action>, every Progress<T> constructed by the caller); MainViewModel at 1774 lines"
provides:
  - "StatusSweepCoordinator — internal sealed engine owning Reset/WalkAsync/ComputeAdvisoryAsync and the backup-root reachability memo"
  - "SaveStatus.SkippedPaths carried on Found/NoSaves results, through the same DirectoryWalk onSkipped channel BackupService already uses"
  - "DirectoryWalk.FormatSkippedList — the one shared skipped-path display shape for Backup and Scan"
  - "str_game_unreadablePaths row sentence (D-P04-01: per-row, on the Game cell, not an aggregate sweep-level message)"
  - "CHANGELOG [Unreleased] Fixed entry; DECISIONS.md Backup-and-restore table row"
affects: [01-05-PLAN]

# Actuals (#2632)
actuals:
  tokens: 11500   # chars/4 over the realized diff (~46000 chars across the three commits)
  tasks: 3
  commits: 3
  plan_head_before: 920191526077b9fc969d7b1c631d0a7e46252668
  plan_head_after: 8d3569a5fb5ab4eeaca9b558af4bf4b034ae8ade

tech-stack:
  added: []
  patterns:
    - "Status-sweep extraction: same internal sealed / constructor-injected / Action<Action> onUi shape BulkBackupCoordinator established in Plan 03, applied to a second coordinator"
    - "Shared skip-list formatter (DirectoryWalk.FormatSkippedList) so two independent warning surfaces (Backup's OperationResult message, Scan's per-row sentence) cannot drift apart"

key-files:
  created:
    - src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs
    - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - src/SavegameBunker/Models/SaveStatus.cs
    - src/SavegameBunker/Services/SaveStatusScanner.cs
    - src/SavegameBunker/Services/DirectoryWalk.cs
    - src/SavegameBunker/Services/BackupService.cs
    - src/SavegameBunker/ViewModels/GameRowViewModel.cs
    - src/SavegameBunker/Themes/Strings.en.xaml
    - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
    - tests/SavegameBunker.Tests/GameRowViewModelTests.cs
    - CHANGELOG.md
    - docs/DECISIONS.md

key-decisions:
  - "D-P04-01 (recorded in 01-04-PLAN.md): REF-04b surfaces per row, on the Game cell — not an aggregate sweep-level StatusMessage warning. StatusMessage is the outcome surface Report() overwrites; a sweep-level warning would either flicker away or stomp a backup's own result message. The per-row surface needed only GameRowViewModel.ApplyStatus's body — no public member added, so the REQUIREMENTS.md scope fence on GameRowViewModel holds."
  - "SPEC.md unchanged — REF-04b implements its existing Error Handling rule ('No silent failures – every failed action must give visible feedback'), not a new behaviour contract."

requirements-completed: [REF-02, REF-03, REF-04]

coverage:
  - id: D1
    description: "StatusSweepCoordinator owns the sweep engine (Reset, WalkAsync, ComputeAdvisoryAsync, the reachability memo); MainViewModel keeps the status-line announce/restore, the follow-up full sweep, the advisory debounce, the startup-prompt trigger"
    requirement: "REF-02"
    verification:
      - kind: other
        ref: "acceptance greps: coordinator has no WPF/CommunityToolkit/Dispatcher/ObservableObject/new Progress</ConfigureAwait; MainViewModel has zero Parallel.ForEachAsync/_lastBackupRootReachable/SaveStatusScanner.Scan; _sweep constructed as new StatusSweepCoordinator(_backup, OnUi); RecomputeSpaceAdvisoryAsync signature unchanged; MainViewModel.cs 1663 lines (<=1680); frozen suites (MainViewModelTests.cs, MainViewModelGuardPinTests.cs, StartupUnsecuredPromptTests.cs) byte-identical vs 96a06a3; dotnet build Release 0 Warning(s)/0 Error(s)"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs — 7 facts (Reset x2, WalkAsync x3, ComputeAdvisoryAsync x2)"
        status: unknown
    human_judgment: false
  - id: D2
    description: "Every row write the sweep applies goes through the onUi delegate with the per-row token check made inside that callback, exactly as before; no Progress<T> or WPF type lives in the coordinator"
    requirement: "REF-03"
    verification:
      - kind: unit
        ref: "StatusSweepCoordinatorTests: WalkAsync_settles_every_row_and_routes_each_write_through_onUi (4 onUi calls for 2 rows), WalkAsync_leaves_a_row_alone_when_a_newer_scan_bumped_its_token"
        status: unknown
    human_judgment: true
    rationale: "The facts were authored and the build compiled, but the xUnit host (net10.0-windows) cannot execute on this Linux machine. The threading discipline (OnUi marshalling onto the real WPF dispatcher) additionally needs the running app; the owner's Windows test run is the verification of record."
  - id: D3
    description: "SaveStatusScanner.Scan collects every unreadable path through the same onSkipped channel BackupService uses, carries it on SaveStatus.SkippedPaths, and GameRowViewModel's Game cell shows it in warning colour via the shared DirectoryWalk.FormatSkippedList shape (D-P04-01)"
    requirement: "REF-04"
    verification:
      - kind: other
        ref: "acceptance greps: SaveStatus.SkippedPaths present, SaveStatusScanner passes onSkipped, BackupService.DescribeSkippedPathsWarning calls FormatSkippedList, DirectoryWalk.FormatSkippedList is public static, str_game_unreadablePaths present in both Strings.en.xaml and GameRowViewModel.cs, GameRowViewModel's public/[ObservableProperty] surface diff-empty vs phase1-baseline, IDialogService.cs/MainWindow.xaml diff-empty vs phase1-baseline, DECISIONS.md names 'cannot fully read', no SaveStatus factory signature changed, tests/SavegameBunker.Tests numstat shows additions only vs phase1-baseline; dotnet build Release 0/0"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/SaveStatusScannerTests.cs (5 new facts) and GameRowViewModelTests.cs (5 new facts)"
        status: unknown
    human_judgment: true
    rationale: "dotnet test cannot execute on this Linux host. The plan's own Task 3 human-check (a real deny-ACL folder, Refresh twice, then back up) additionally needs the running app and joins the end-of-phase UAT batch."

duration: ~50min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 4: StatusSweepCoordinator & Scan Skip-Surfacing Summary

**Moved the status-sweep engine (Reset/WalkAsync/the free-space advisory's own filesystem reads) into an internal sealed `StatusSweepCoordinator`, with the per-row token check, the onUi marshalling and the reachability-flip memo preserved verbatim, and closed REF-04b: a save folder's unreadable paths now surface on the game's own row, in warning colour, with the same list format Backup's own warning already uses.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-11-17T23:05Z
- **Completed:** 2026-11-17T23:55Z
- **Tasks:** 3
- **Files modified:** 11 (2 new, 9 modified)

## Accomplishments

- `src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs` (158 lines) — `internal sealed class` taking `BackupService` and `Action<Action> onUi`; `Reset` bumps every row's scan token and resets both axes to Checking, returning the snapshot; `WalkAsync` reads reachability once, runs the bounded-concurrency walk, and applies every write through `_onUi` with the token check inside the callback, returning whether the backup root just became reachable on the actual false-to-true flip; `ComputeAdvisoryAsync` is the free-space advisory's filesystem work, run inside the caller's own `Task.Run`. No WPF type, no `Progress<T>` construction, no `ConfigureAwait`.
- `MainViewModel.cs` — `_sweep` field constructed as `new StatusSweepCoordinator(_backup, OnUi)` right after `_bulk`; the `_lastBackupRootReachable` memo field and the 20-line sweep doc block both deleted (moved with the logic); `StartStatusScan` is now `_sweep.Reset(...)` → the four completion steps unchanged → one `Task.Run` awaiting `_sweep.WalkAsync(...)`; `RecomputeSpaceAdvisoryAsync` keeps its internal signature and awaits `_sweep.ComputeAdvisoryAsync(...)`. 1774 → 1663 lines.
- `tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs` (new, 7 facts) — `Reset` token-bump and empty-snapshot facts; `WalkAsync` settle-and-route-through-onUi (4 calls for 2 rows), stale-token isolation, reachability-flip-only-once; `ComputeAdvisoryAsync` no-segments and selected-total facts.
- `src/SavegameBunker/Models/SaveStatus.cs` — `SkippedPaths` (init-only `IReadOnlyList<string>`, empty default) added to the record body, not a positional parameter — every existing factory call site and comparison is unchanged.
- `src/SavegameBunker/Services/DirectoryWalk.cs` — `public static string FormatSkippedList(...)`: the one shared "first five, comma-joined, plus `, and N more`" shape, now called by both `BackupService.DescribeSkippedPathsWarning` and `SaveStatusScanner`'s consumer.
- `src/SavegameBunker/Services/SaveStatusScanner.cs` — `Scan` passes a collecting `onSkipped: skipped.Add` delegate to `DirectoryWalk.ForEachFile` and attaches `SkippedPaths` via `with { }` only when non-empty, so a complete scan still returns the bare factory value.
- `src/SavegameBunker/Services/BackupService.cs` — `DescribeSkippedPathsWarning` now calls `DirectoryWalk.FormatSkippedList` instead of duplicating the take-5-and-count-the-rest logic; output byte-identical.
- `src/SavegameBunker/ViewModels/GameRowViewModel.cs` — `ApplyStatus`'s `NoSaves`/`Found` branches show `str_game_unreadablePaths` in `RowSeverity.Warning` when `status.SkippedPaths.Count > 0`, overriding that branch's own sentence; no public member added/removed, `PathError`/`Checking` untouched.
- `src/SavegameBunker/Themes/Strings.en.xaml` — `str_game_unreadablePaths`: "`{0} path(s) could not be read and are not counted here or backed up: {1}`".
- `CHANGELOG.md` / `docs/DECISIONS.md` — `[Unreleased]` Fixed entry and a new Backup-and-restore table row (D-P04-01's reasoning).

## Task Commits

1. **Task 1: StatusSweepCoordinator end to end** — `48e1c4b` (refactor(01-04))
2. **Task 2: REF-04b carry — Scan collects unreadable paths through the walk's existing onSkipped channel** — `1adf9cd` (fix(01-04))
3. **Task 3: REF-04b surface — the row says when part of its save folder could not be read** — `8d3569a` (fix(01-04))

**Plan metadata:** the closing docs commit (this SUMMARY + STATE.md/ROADMAP.md tracking updates).

## Files Created/Modified

- `src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs` - new coordinator (158 lines)
- `tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs` - new; 7 facts
- `src/SavegameBunker/ViewModels/MainViewModel.cs` - sweep loop replaced by coordinator calls; 1774 → 1663 lines
- `src/SavegameBunker/Models/SaveStatus.cs` - `SkippedPaths` init-only property
- `src/SavegameBunker/Services/SaveStatusScanner.cs` - passes `onSkipped`, attaches `SkippedPaths`
- `src/SavegameBunker/Services/DirectoryWalk.cs` - `FormatSkippedList`
- `src/SavegameBunker/Services/BackupService.cs` - `DescribeSkippedPathsWarning` reuses `FormatSkippedList`
- `tests/SavegameBunker.Tests/SaveStatusScannerTests.cs` - 5 new facts
- `src/SavegameBunker/ViewModels/GameRowViewModel.cs` - `ApplyStatus` shows the skipped-paths sentence
- `src/SavegameBunker/Themes/Strings.en.xaml` - `str_game_unreadablePaths`
- `tests/SavegameBunker.Tests/GameRowViewModelTests.cs` - 5 new facts
- `CHANGELOG.md` / `docs/DECISIONS.md` - Fixed entry, decision table row

## Decisions Made

- D-P04-01 (already recorded in the plan): per-row surface on the Game cell, not an aggregate sweep-level message — see key-decisions above.
- SPEC.md intentionally left unchanged: REF-04b implements its existing "no silent failures" rule rather than introducing new behaviour.

## Deviations from Plan

### Test-host adaptation (binding for this run)

- **dotnet test not run.** This Linux host builds `net10.0-windows` + WPF but cannot execute the test suite. Every `<verify>` test leg in the plan (StatusSweepCoordinatorTests, frozen MainViewModel suites, SaveStatusScannerTests/BackupServiceTests/ServiceContractTests, GameRowViewModelTests/StringsTests/ResourceKeyTests, the full-suite leg) is deferred — see Deferred Verification below. `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` returning **0 Warning(s) / 0 Error(s)** (run after each task) is the executed gate.
- **Dispatched test.yml leg and push not performed** — this run's git rules forbid touching the remote.
- **Task 3's human-check** (real deny-ACL folder, Refresh twice, then back up, observe the row sentence and the backup's matching warning) joins the end-of-phase UAT batch.
- **TDD RED not separately observed for Tasks 2/3.** Both tasks' RED state (failing to compile against the missing members) was authored before GREEN and is implicit in the diff; the build only ever ran after GREEN on this host.

### No production-code deviations

All production work (StatusSweepCoordinator extraction, SaveStatus/DirectoryWalk/SaveStatusScanner/BackupService/GameRowViewModel changes) executed exactly as the plan specified. The 20-line sweep doc block move and the OnUi doc-split (plan step 5) were applied in full.

---

**Total deviations:** 4 documented (all test-host adaptations; no production-code deviations).
**Impact on plan:** All production work executed as written; only test execution and CI dispatch moved to the owner's environment.

## Issues Encountered

None beyond the adaptations above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `StatusSweepCoordinator` is in place alongside `BulkBackupCoordinator`, both following the identical `internal sealed` / constructor-injected / `Action<Action> onUi` shape Plan 05's `RowActionsCoordinator` will copy.
- `MainViewModel.cs` is at 1663 lines, carrying only the per-row actions (BackupAsync/RestoreAsync/OpenSaveFolderAsync/ChangeSaveFolderAsync/RemoveGameAsync) that Plan 05 extracts next.
- `SaveStatus.SkippedPaths` and `DirectoryWalk.FormatSkippedList` are available to any future caller that needs the same shape.
- The Windows run at phase close must show: full suite green, total = 01-03's 660 + this plan's 7 (coordinator) + 5 (SaveStatusScanner) + 5 (GameRowViewModel) = 677.

## Deferred Verification

Every item below must run on the owner's Windows machine at phase close:

| # | Item | Command | Result |
|---|------|---------|--------|
| 1 | Coordinator + frozen MainViewModel suites | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~StatusSweepCoordinatorTests\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~MainViewModelGuardPinTests\|FullyQualifiedName~StartupUnsecuredPromptTests"` | [pending — owner Windows machine] |
| 2 | Scan skip-carry + backup/contract suites | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~SaveStatusScannerTests\|FullyQualifiedName~BackupServiceTests\|FullyQualifiedName~ServiceContractTests\|FullyQualifiedName~BackupComparerTests"` | [pending — owner Windows machine] |
| 3 | Row surface suites | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~GameRowViewModelTests\|FullyQualifiedName~StringsTests\|FullyQualifiedName~ResourceKeyTests"` | [pending — owner Windows machine] |
| 4 | Full suite | `dotnet test SavegameBunker.sln -c Release -tl:off` (green; total = 660 + 17 = 677) | [pending — owner Windows machine] |
| 5 | CI leg | push branch + `gh workflow run test.yml --ref refactor/v1.1-phase-1`, watch green for HEAD `8d3569a` | [pending — owner Windows machine; this run may not touch the remote] |
| 6 | Windows human-check (Task 3 UAT) | one catalog game with a readable file plus a deny-ACL subfolder; start the app, Refresh twice, back up that game — row shows "1 path(s) could not be read…" in warning colour after each Refresh; backup succeeds with the matching "NOT in this backup" warning; other rows unchanged | [pending — owner Windows machine] |

## Self-Check: PASSED

Verification commands actually run on this host:

- `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → **Build succeeded. 0 Warning(s), 0 Error(s)** (after Task 1, Task 2 and Task 3).
- `test -f src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs && grep -c "internal sealed class StatusSweepCoordinator" …` → 1 (EXPECTED ≥1).
- `grep -v -E "^\s*//" MainViewModel.cs | grep -c "Parallel.ForEachAsync"` → 0; `grep -c "_lastBackupRootReachable"` → 0; `grep -c "SaveStatusScanner.Scan"` → 0 (all EXPECTED ==0).
- `grep -v -E "^\s*//" StatusSweepCoordinator.cs | grep -c -E "System\.Windows|CommunityToolkit|Dispatcher|ObservableObject|new Progress<|ConfigureAwait"` → 0 (EXPECTED ==0).
- `grep -c "_sweep = new StatusSweepCoordinator(_backup, OnUi)" MainViewModel.cs` → 1; `grep -c "internal async Task RecomputeSpaceAdvisoryAsync()"` → 1 (both EXPECTED ≥1).
- `wc -l < MainViewModel.cs` → 1663 (EXPECTED ≤1680).
- `git diff --quiet 96a06a3 -- tests/…/MainViewModelTests.cs tests/…/MainViewModelGuardPinTests.cs tests/…/StartupUnsecuredPromptTests.cs` → exit 0 (frozen-suite diff target measured against 96a06a3, per 01-03-SUMMARY.md's documented reconciliation — `phase1-baseline` predates `MainViewModelGuardPinTests.cs`).
- `grep -c "SkippedPaths" Models/SaveStatus.cs` → 1; `grep -c "onSkipped" SaveStatusScanner.cs` → 2; `grep -c "FormatSkippedList" BackupService.cs` → 1; `grep -c "public static string FormatSkippedList" DirectoryWalk.cs` → 1; `grep -c "A_fully_readable_directory_reports_no_skipped_paths" SaveStatusScannerTests.cs` → 1 (all EXPECTED ≥1).
- `git diff phase1-baseline -- Models/SaveStatus.cs | grep -c -E "^-[[:space:]]+public static SaveStatus"` → 0 (no factory signature changed).
- `git diff --numstat phase1-baseline -- tests/SavegameBunker.Tests | awk '$2 != 0 && $2 != "" {bad=1} END {exit bad}'` → exit 0 (additions only).
- `grep -c "str_game_unreadablePaths" Strings.en.xaml` → 1; `grep -c "str_game_unreadablePaths" GameRowViewModel.cs` → 2 (both EXPECTED ≥1).
- `git diff phase1-baseline -- GameRowViewModel.cs | grep -c -E "^[-+][[:space:]]*(public|\[ObservableProperty\])"` → 0 (public surface unchanged).
- `git diff --quiet phase1-baseline -- IDialogService.cs MainWindow.xaml` → exit 0.
- `grep -c "cannot fully read" docs/DECISIONS.md` → 1 (EXPECTED ≥1).
- Commits `48e1c4b`, `1adf9cd`, `8d3569a` are HEAD-ancestors on `refactor/v1.1-phase-1`, each committed with the ArgusLoom identity; `git status --short` shows only this SUMMARY plus the pre-existing untracked `IDEA.md`.
