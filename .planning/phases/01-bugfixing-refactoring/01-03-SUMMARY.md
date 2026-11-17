---
phase: 01-bugfixing-refactoring
plan: 03
subsystem: ui
tags: [wpf, mvvm, refactoring, coordinator, xunit, tdd]

# Dependency graph
requires:
  - phase: 01-02
    provides: "BulkRunRules (BackupOutcome/RestoreOutcome/SelectedTargets/ChangedTargets), 1847-line MainViewModel, ServiceContractTests"
provides:
  - "BulkBackupCoordinator — internal sealed orchestrator for both bulk-backup commands and Restore selected, holding the REF-03 claim/release re-entrancy guard verbatim"
  - "BulkBackupRun / BulkRestoreRun result records"
  - "REF-04a: BackupService names the rename-retry limit with its own failure message (MaxRenameRetries = 100), boundary unchanged"
  - "CHANGELOG [Unreleased] Fixed entry for the new message"
affects: [01-04-PLAN, 01-05-PLAN]

# Actuals (#2632)
actuals:
  tokens: 13300   # chars/4 over the realized diff (53179 chars)
  tasks: 3
  commits: 3
  plan_head_before: 5bb9d4ac4aa3001cdbbff2f2bfd09d08c487ac7b
  plan_head_after: 70e0458e25d89aedbe3d9736fc94a14d6c5aae47

tech-stack:
  added: []
  patterns:
    - "Coordinator extraction behind an unchanged command surface: internal sealed class, constructor-injected, private readonly field, OnUi handed in as Action<Action>, every Progress<T> constructed by the caller (GameActions precedent, Pattern B)"

key-files:
  created:
    - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
    - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - src/SavegameBunker/Services/BackupService.cs
    - tests/SavegameBunker.Tests/BackupServiceTests.cs
    - CHANGELOG.md

key-decisions:
  - "REF-04 boundary reconciled to the code, not the probe text: the shipped ladder tries the bare name plus -1…-100, so 101 occupied names trip the cap and 100 occupied names still succeed as -100.zip; the tests pin exactly that (behavior block in the plan stated the same numbers)"
  - "Test-host adaptation (Linux): dotnet test and the dispatched test.yml leg deferred to the owner's Windows machine; Release build 0/0 is the only Linux-verifiable gate"

requirements-completed: [REF-01, REF-02, REF-03, REF-04]

coverage:
  - id: D1
    description: "Bulk backup and bulk restore orchestration moved into BulkBackupCoordinator; MainViewModel keeps bound state, root resolution, Progress construction, rescan and report"
    requirement: "REF-02"
    verification:
      - kind: other
        ref: "acceptance greps: coordinator has no WPF/CommunityToolkit/Dispatcher/ObservableObject, no new Progress<, no ConfigureAwait; MainViewModel no _backup.Backup(, no TryPlanNewestOrdinaryAsync, exactly three new Progress< sites, 1774 <= 1780 lines; one Task.Run in the coordinator (inside BackupAsync); frozen suites byte-identical vs 96a06a3; dotnet build Release 0 Warning(s)/0 Error(s)"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs — 9 facts (6 backup, 3 restore)"
        status: unknown
    human_judgment: false
  - id: D2
    description: "The REF-03 claim/release re-entrancy order is preserved exactly and pinned at coordinator level"
    requirement: "REF-03"
    verification:
      - kind: unit
        ref: "BulkBackupCoordinatorTests: resets-to-checking-before-claim, release-in-onFinished, release-on-throw facts"
        status: unknown
    human_judgment: true
    rationale: "The facts were authored and the build compiled, but the xUnit host (net10.0-windows) cannot execute on this Linux machine; the threading behavior (Progress<T> on the UI thread, OnUi marshalling) additionally needs the real app's dispatcher — the plan's own human-check in Task 2 joins the end-of-phase UAT batch."
  - id: D3
    description: "REF-04a: at the rename cap the backup fails with a message naming the retry limit, the staging file is deleted and nothing pre-existing is touched; one name below the cap still succeeds as -100.zip"
    requirement: "REF-04"
    verification:
      - kind: other
        ref: "acceptance greps: MaxRenameRetries declared and used by both catch filters, no 'attempt < 100' literal, both fact names present, CHANGELOG [Unreleased] present; dotnet build Release 0/0"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#Backup_one_name_below_the_rename_retry_limit_still_succeeds_as_the_last_suffix and #Backup_at_the_rename_retry_limit_reports_the_limit_not_a_generic_disk_error"
        status: unknown
    human_judgment: true
    rationale: "TDD RED/GREEN could not be executed on this Linux host (net10.0-windows test target); both facts must run on the owner's Windows machine. RED (the cap fact failing on the old generic message) was not observed on this host."

duration: ~25min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 3: BulkBackupCoordinator & REF-04a Summary

**Moved both bulk-backup commands and Restore selected into an internal sealed `BulkBackupCoordinator` (claim/loop/release order — the REF-03 re-entrancy guard — preserved verbatim; MainViewModel 1847 → 1774 lines) and closed REF-04a: the 101-name rename cap now fails with its own "rename retry limit reached" message while the 100-name boundary below it stays pinned — build 0/0; test execution deferred to the owner's Windows machine.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-11-17T~22:24Z
- **Completed:** 2026-11-17T22:49Z
- **Tasks:** 3
- **Files modified:** 6 (2 new, 4 modified)

## Accomplishments

- `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs` (272 lines) — `internal sealed class` taking `BackupService`, `GameActions` and `Action<Action> onUi`; `BackupAsync` runs the bulk-backup loop in one `Task.Run` with every bound row write routed through `_onUi`; `RestoreAsync` runs the restore loop sequentially on the caller's own context (Confirm needs the dispatcher); both reset every target to Checking → `onStarted` → IsBusy claim, and release in a `finally` (backup also zeroes the byte counters) before `onFinished`. No WPF type, no `Progress<T>` construction, no `ConfigureAwait`. Plus `BulkBackupRun` / `BulkRestoreRun` records.
- `MainViewModel.cs` — `_bulk` field constructed as `new BulkBackupCoordinator(_backup, _gameActions, OnUi)` right after `_gameActions`; `BeginBulkProgress` / `SetBulkProgress` / `EndBulkProgress` / `CreateByteProgress` / `CreateStepProgress`; `RunBulkBackupAsync` and `RestoreSelectedAsync` are now thin wrappers (root resolution → progress dictionaries → `_bulk.*` → `BulkRunRules.*` outcome report), the single-row `BackupAsync`/`RestoreAsync` reuse the two Create*Progress factories. `new Progress<` sites: exactly 3. 1847 → 1774 lines. Every `[RelayCommand]` attribute, generated name and `Can*` predicate byte-identical.
- `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs` (360 lines, 9 facts) — claim order (checking reset before claim, none busy at onStarted), release inside onFinished, release-on-throw via a missing byteProgress entry (KeyNotFoundException), zero-targets null, failure collection + in-order progress `(1, first)` → `(2, second)`, onUi routing count 6, restore item order with one restored / one declined, restore zero-targets, missing-archive failure continues.
- `BackupService.cs` — `private const int MaxRenameRetries = 100;` with a doc naming what happens at the limit; the collision filter uses the constant (semantics unchanged); a second, narrower catch at `attempt == MaxRenameRetries && File.Exists(candidate)` deletes the verified staging file and returns `Backup failed for {game}: rename retry limit reached — …`. The outer generic catch is untouched.
- `BackupServiceTests.cs` — two boundary facts (additions only): 100 occupied names still succeed as `-100.zip` with nothing overwritten; 101 occupied names fail with a message starting `Backup failed for Ōkami Test:` and containing `rename retry limit`, no `*.zip.tmp` left, zip count and pre-existing bytes unchanged.
- `CHANGELOG.md` — `## [Unreleased]` / `### Fixed` entry for the new message.

## Task Commits

1. **Task 1: BulkBackupCoordinator end to end for bulk backup** — `8ae6c23` (refactor(01-03))
2. **Task 2: Bulk restore into BulkBackupCoordinator.RestoreAsync** — `fb95a15` (refactor(01-03))
3. **Task 3: REF-04a rename-retry limit message** — `70e0458` (fix(01-03))

**Plan metadata:** the closing docs commit (this SUMMARY + STATE.md/ROADMAP.md tracking updates).

## Files Created/Modified

- `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs` - new coordinator + two result records (272 lines)
- `src/SavegameBunker/ViewModels/MainViewModel.cs` - bulk loops replaced by coordinator calls; 1847 → 1774 lines
- `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs` - new; 9 facts over both coordinator methods
- `src/SavegameBunker/Services/BackupService.cs` - MaxRenameRetries const + the limit's own catch branch
- `tests/SavegameBunker.Tests/BackupServiceTests.cs` - two new boundary facts (additions only)
- `CHANGELOG.md` - [Unreleased] Fixed entry

## Decisions Made

- REF-04 boundary numbers pinned to the code's real behavior (101 occupied names fail, 100 succeed as -100.zip), matching the plan's own flagged-assumption reconciliation — the probe text's "at exactly 100 colliding filenames" wording was not followed.
- Reused the existing public `RecordingDialogService` from MainViewModelTests.cs in the new coordinator tests instead of duplicating an IDialogService fake; the frozen file itself is untouched.
- The new failure message is composed inline in plain English (the file's own convention for adjacent messages), not via a Strings.en.xaml key, per the plan's flagged assumption.

## Deviations from Plan

### Test-host adaptation (binding for this run)

- **dotnet test not run.** Every `<verify>` test leg (coordinator tests, frozen MainViewModel suites, BackupServiceTests, ServiceContractTests, full suite) is deferred; `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` returning **0 Warning(s) / 0 Error(s)** (run after each task) is the executed gate.
- **TDD RED not observed.** Task 3's RED step (the cap fact failing against the old generic message) could not execute here; both facts were authored before the GREEN change and the code change is exactly the narrowed catch the plan specifies. GREEN must be observed on the owner's Windows machine.
- **Dispatched test.yml leg and push not performed** — this run's git rules forbid touching the remote. Deferred to the owner.
- **Windows human-check (Task 2)** — the bulk progress panel / per-row byte bar / restore-sentence check joins the end-of-phase UAT batch.

### Acceptance-criterion adjustments

- **Frozen-suite diff target measured against `96a06a3`** (01-01's final commit) rather than the raw `phase1-baseline` tag, for the reason recorded in 01-02-SUMMARY.md: the tag predates `MainViewModelGuardPinTests.cs`, so the literal tag diff shows the whole file as an addition. Against `96a06a3` all frozen files (MainViewModelTests.cs, MainViewModelGuardPinTests.cs, RestoreService.cs) are byte-identical.
- **Task 1 precondition**: `refs/tags/phase1-baseline` exists on this machine (checked; no recreation needed).

---

**Total deviations:** 4 documented (all test-host adaptations or measurement-target corrections; no production-code deviations).
**Impact on plan:** All production work executed as written; only test execution and CI dispatch moved to the owner's environment.

## Issues Encountered

- None beyond the adaptations above. A stray brace from the Task 2 patch was caught by the immediately-following build and fixed before any commit.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `BulkBackupCoordinator` is in place with the exact shape Plan 04 (`StatusSweepCoordinator`) and Plan 05 (`RowActionsCoordinator`) will copy; `MainViewModel` is at 1774 lines, still carrying the status sweep and per-row actions those plans extract.
- `BulkRunRules.BackupOutcome`/`RestoreOutcome` are consumed by both new wrappers, as Plan 02 intended.
- The Windows run at phase close must show: full suite green, total = 01-02's expected 649 + this plan's 11 new test cases (9 coordinator + 2 BackupService) = 660.
- The REF-04 boundary fact pair is the verifier's reference for the 101-vs-100 reconciliation.

## Deferred Verification

Every item below must run on the owner's Windows machine at phase close:

| # | Item | Command | Result |
|---|------|---------|--------|
| 1 | Coordinator + frozen MainViewModel suites | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~BulkBackupCoordinatorTests\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~MainViewModelGuardPinTests\|FullyQualifiedName~StartupUnsecuredPromptTests"` | [pending — owner Windows machine] |
| 2 | REF-04a boundary facts + service tests | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~BackupServiceTests\|FullyQualifiedName~ServiceContractTests"` (both new facts green; cap fact must fail against the pre-fix code — if a RED re-check is wanted, `git stash` the BackupService change) | [pending — owner Windows machine] |
| 3 | Full suite | `dotnet test SavegameBunker.sln -c Release -tl:off` (green; total = 649 + 11 = 660) | [pending — owner Windows machine] |
| 4 | CI leg | push branch + `gh workflow run test.yml --ref refactor/v1.1-phase-1`, watch green for HEAD `70e0458` | [pending — owner Windows machine; this run may not touch the remote] |
| 5 | Windows human-check (Task 2 UAT) | run the app against ≥3 games, one with ≥50 MB of saves: Back up all changed, then Restore selected on two games declining both confirmations — window-level progress counts up per game, the large game's own bar moves, "Restoring N of M" sentence, one confirm per game in list order, no crash, no bar left visible, verdicts settle | [pending — owner Windows machine] |

## Self-Check: PASSED

Verification commands actually run on this host:

- `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → **Build succeeded. 0 Warning(s), 0 Error(s)** (after Task 1, Task 2 and Task 3).
- `test -f src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs && grep -c "internal sealed class BulkBackupCoordinator" …` → 1 (EXPECTED 1).
- `grep -v -E "^\s*//" BulkBackupCoordinator.cs | grep -c -E "System\.Windows\|CommunityToolkit\|Dispatcher\|ObservableObject"` → 0 (EXPECTED 0).
- `grep -v -E "^\s*//" BulkBackupCoordinator.cs | grep -c "new Progress<"` → 0 (EXPECTED 0); `grep -c ConfigureAwait` → 0 (EXPECTED 0).
- `grep -v -E "^\s*//" MainViewModel.cs | grep -c -F "_backup.Backup("` → 0 (EXPECTED 0); `grep -c "_bulk = new BulkBackupCoordinator(_backup, _gameActions, OnUi)"` → 1 (EXPECTED ≥ 1).
- `git diff --quiet 96a06a3 -- tests/…/MainViewModelTests.cs tests/…/MainViewModelGuardPinTests.cs` → exit 0; extended in Task 2 to `src/SavegameBunker/Services/RestoreService.cs` → exit 0.
- `grep -v -E "^\s*//" MainViewModel.cs | grep -c TryPlanNewestOrdinaryAsync` → 0 (EXPECTED 0).
- `grep -n "new Progress<" MainViewModel.cs` → exactly 3 lines (CreateByteProgress, CreateStepProgress, RunBulkBackupAsync).
- `grep -n "Task.Run" BulkBackupCoordinator.cs` (code lines) → exactly one, line 100, inside BackupAsync.
- `wc -l < MainViewModel.cs` → 1774 (EXPECTED ≤ 1780).
- `grep -c "private const int MaxRenameRetries" BackupService.cs` → 1; both catch filters use the constant; `grep -c 'attempt < 100'` (code lines) → 0.
- Both new fact names present in BackupServiceTests.cs (1 each); `grep -c "## \[Unreleased\]" CHANGELOG.md` → 1.
- `git diff --numstat 96a06a3 -- tests/SavegameBunker.Tests` → 0 deleted lines (additions only).
- Commits `8ae6c23`, `fb95a15`, `70e0458` are HEAD-ancestors on `refactor/v1.1-phase-1`, each committed with the ArgusLoom identity; `git status` shows no uncommitted tracked changes.
- Tracer gate (Task 1): `⚡ Tracer verified end-to-end — expanding` logged on the passing Release build; the test leg's deferral is recorded above, not counted as a pass.
