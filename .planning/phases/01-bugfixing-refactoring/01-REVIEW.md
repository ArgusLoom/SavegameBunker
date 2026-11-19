---
phase: 01-bugfixing-refactoring
reviewed: 2026-11-19T00:00:00Z
depth: standard
files_reviewed: 21
files_reviewed_list:
  - CHANGELOG.md
  - docs/DECISIONS.md
  - src/SavegameBunker/Models/SaveStatus.cs
  - src/SavegameBunker/Services/BackupService.cs
  - src/SavegameBunker/Services/DirectoryWalk.cs
  - src/SavegameBunker/Services/SaveStatusScanner.cs
  - src/SavegameBunker/Themes/Strings.en.xaml
  - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
  - src/SavegameBunker/ViewModels/BulkRunRules.cs
  - src/SavegameBunker/ViewModels/GameRowViewModel.cs
  - src/SavegameBunker/ViewModels/MainViewModel.cs
  - src/SavegameBunker/ViewModels/RowActionsCoordinator.cs
  - src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs
  - tests/SavegameBunker.Tests/BackupServiceTests.cs
  - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs
  - tests/SavegameBunker.Tests/BulkRunRulesTests.cs
  - tests/SavegameBunker.Tests/GameRowViewModelTests.cs
  - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
  - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
  - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
findings:
  critical: 0
  warning: 3
  info: 0
  total: 3
  necessity: 2
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-11-19T00:00:00Z
**Depth:** standard
**Files Reviewed:** 21
**Status:** issues_found

## Summary

Reviewed the full phase-01 file set: the `MainViewModel` → `BulkBackupCoordinator` /
`StatusSweepCoordinator` / `RowActionsCoordinator` / `BulkRunRules` split, the unreadable-save-path
and rename-retry-limit fixes in `BackupService`/`DirectoryWalk`/`SaveStatusScanner`/`SaveStatus`,
`GameRowViewModel`'s status mapping, and the matching test files (`BulkBackupCoordinatorTests`,
`BulkRunRulesTests`, `RowActionsCoordinatorTests`, `StatusSweepCoordinatorTests`,
`MainViewModelGuardPinTests`, plus the pre-existing service/row test files touched here). Diffed
each source file against its pre-phase baseline (`96a06a3..27e47d1`) to separate what this phase
actually changed from what it only moved, and checked the moved logic against CLAUDE.md's
documented contracts (never-throw service boundary, UI-thread marshalling via `OnUi`, the
restore-ordering invariant, the two-dependency rule).

This necessity review (`$HOME/.claude/gsd-core/references/necessity-review.md`) was loaded
successfully, so the `necessity:` key above is populated.

Behavior preservation across the split holds up well: the claim/release ordering
(reset-to-Checking → raise counters → claim `IsBusy`, reversed in a `finally`) is identical between
the old inline `MainViewModel` code and the new `BulkBackupCoordinator`, and
`MainViewModelGuardPinTests`/`BulkBackupCoordinatorTests`/`StatusSweepCoordinatorTests` pin the
exact sequencing facts that matter (token-based stale-result rejection, the false→true
reachability flip, per-row re-entrancy guards). No Critical-tier defects were found — no security
issue, no data-loss path, and the restore ordering (safety backup → extract to temp sibling → move
old aside → rename new in) is unchanged. The issues below are a real but narrow robustness gap in
the new bulk-restore loop, plus two provably-unreachable defensive branches left over from the
status-mapping code this phase touched.

## Warnings

### WR-01: Bulk restore leaves a row's progress bar stuck if one item throws mid-restore

**File:** `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs:215-258`
**Issue:** `RowActionsCoordinator.RestoreAsync` (the single-row path) resets
`row.RestoreStep`/`row.RestoreStepText` to idle both right after the restore call succeeds/fails
*and* again in its own `finally` (`RowActionsCoordinator.cs:159-160` and `170-171`) — so a row's
determinate restore bar cannot outlive the write even if `GameActions.RestoreAsync` throws
unexpectedly. The new bulk-restore loop in `BulkBackupCoordinator.RestoreAsync` only clears those
two fields once, immediately after a *successful return* from
`_gameActions.RestoreAsync(row, plan!, stepProgress[row], i + 1, targets.Count)` (lines 228-230).
The loop's own `finally` (lines 250-258) releases `IsBusy` for every target but never touches
`RestoreStep`/`RestoreStepText`. If that call throws for one row — rather than returning a failed
`OperationResult` the way the documented never-throw-at-the-service-boundary contract expects —
that row's `IsBusy` goes back to `false` (every button re-enables) while `RestoreStep` stays at
whatever step (1-3) it was on, so `IsRestoring` (`RestoreStep > 0`) stays true and that row's
restore bar renders forever stuck, with no further event to clear it short of the next restore on
that same row.
**Fix:** Clear the progress fields for every target in the loop's own `finally`, mirroring the
single-row path:
```csharp
finally
{
    foreach (var row in targets)
    {
        row.IsBusy = false;
        row.RestoreStep = 0;
        row.RestoreStepText = "";
    }

    onFinished();
}
```

### WR-02: Necessity — impossible-state `default` branch in `ApplyBackupComparison`

**File:** `src/SavegameBunker/ViewModels/GameRowViewModel.cs:269-273`
**Category:** necessity
**Issue:** `ApplyBackupComparison`'s switch already has an explicit `case` for every member of
`BackupComparisonKind` (`UpToDate`, `SavesChangedSinceBackup`, `NeverBackedUp`, `NoSaveData`,
`BackupFolderNotAvailable`, `Checking`), then adds `default:` sharing the `Checking` block
(lines 269-273). `BackupComparisonKind` values only ever originate from `BackupComparison`'s six
static factories (`src/SavegameBunker/Models/BackupComparison.cs:49-64`), each of which sets `Kind`
to one of exactly those six named literals — a grep of the whole repo (`grep -rn
BackupComparisonKind`) shows no cast, no deserialization and no arithmetic construction of this
enum anywhere else. With every producer closed and every one of the six literals already given its
own named `case`, the `default` arm can never run; it defends against a state the call graph proves
does not occur.
**Fix:** Drop the `default:` label (keep the `Checking` case on its own) and let an exhaustive
switch over a closed set of producers stand without a defensive fallback, or leave a one-line
comment recording the invariant instead of a runtime branch.

### WR-03: Necessity — impossible-state `default` branch in `ApplyStatus`

**File:** `src/SavegameBunker/ViewModels/GameRowViewModel.cs:205-212`
**Category:** necessity
**Issue:** Same shape as WR-02, one switch up: `ApplyStatus`'s switch already names every
`SaveStatusKind` member (`PathError`, `NoSaves`, `Found`, `Checking`) as its own `case`, then adds
`default:` sharing the `Checking` block (lines 205-212). `SaveStatusKind` values only ever come from
`SaveStatus`'s four factories (`src/SavegameBunker/Models/SaveStatus.cs:50-60`:
`Checking()`/`Found()`/`NoSaves()`/`PathError()`), each setting `Kind` to one of exactly those four
literals, and `SaveStatusScanner.Scan` (the only producer this phase touched) only ever returns one
of those four. No other code path constructs a `SaveStatus`.
**Fix:** Same as WR-02 — remove the `default:` label; the four named cases already cover every
value this type can hold.

---

_Reviewed: 2026-11-19T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
