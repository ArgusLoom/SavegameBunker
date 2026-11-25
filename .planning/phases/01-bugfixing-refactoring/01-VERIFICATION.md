---
phase: 01-bugfixing-refactoring
verified: 2026-11-25T00:00:00Z
status: passed
score: 44/44 must-haves verified
covered_files:
  - .planning/phases/01-bugfixing-refactoring/01-01-PLAN.md
  - .planning/phases/01-bugfixing-refactoring/01-01-SUMMARY.md
  - .planning/phases/01-bugfixing-refactoring/01-02-PLAN.md
  - .planning/phases/01-bugfixing-refactoring/01-02-SUMMARY.md
  - .planning/phases/01-bugfixing-refactoring/01-03-PLAN.md
  - .planning/phases/01-bugfixing-refactoring/01-03-SUMMARY.md
  - .planning/phases/01-bugfixing-refactoring/01-04-PLAN.md
  - .planning/phases/01-bugfixing-refactoring/01-04-SUMMARY.md
  - .planning/phases/01-bugfixing-refactoring/01-05-PLAN.md
  - .planning/phases/01-bugfixing-refactoring/01-05-SUMMARY.md
  - CHANGELOG.md
  - docs/DECISIONS.md
  - src/SavegameBunker/Models/SaveStatus.cs
  - src/SavegameBunker/Services/BackupService.cs
  - src/SavegameBunker/Services/DirectoryWalk.cs
  - src/SavegameBunker/Services/RestoreService.cs
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
  - tests/SavegameBunker.Tests/RestoreServiceTests.cs
  - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
  - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
covered_digest: "v4:sha256:49bc2d85533f9802072688e6028a2e3b1423fc59446cd4e59291f26491f2e321"
behavior_unverified: 0
overrides_applied: 2
overrides:
  - must_have: "phase1-baseline tag marks the last commit before any phase-1 code change (tag recreated on the feature branch rather than on main)"
    reason: "Plan-time intent was a main-HEAD tag; execution branched from main at 9d96b181 and tagged that commit on refactor/v1.1-phase-1 instead. `git rev-parse phase1-baseline` = 9d96b1814cc7 (re-confirmed this pass) — the tagged commit is the exact last pre-phase commit, so diff-baseline semantics hold unchanged. Carried forward."
    accepted_by: "owner"
    accepted_at: "2026-11-17"
  - must_have: "MainViewModel.cs reduced to a composition root of at most ~700 lines (REF-02 ceiling)"
    reason: "Measured at phase close (commit f8665df / tag v1.0.1): 1,556 total lines / 761 code-only. Owner decision 'accept-measured' recorded in 01-05-SUMMARY.md and applied to REQUIREMENTS.md's REF-02 line. Carried forward as a point-in-time acceptance; this pass re-confirms the composition-root structure at current HEAD and records that the file has since grown past the accepted figure (see Advisory #1)."
    accepted_by: "owner (accept-measured)"
    accepted_at: "2026-11-17"
re_verification:
  previous_status: passed
  previous_score: 44/44
  gaps_closed: []
  gaps_remaining: []
  regressions: []
advisory:
  - finding: "REF-02 ceiling exceeded at current HEAD. MainViewModel.cs is 1,821 lines total (1,556 at phase close). Counting non-blank, non-comment, non-attribute lines — the method that reproduces the recorded figure (758 vs recorded 761 at f8665df) — it is ~837 now, about 76 over the accepted 761. A plain non-blank/non-comment count gives 879 (799 at close). The '~672 code-only' figure supplied for this run could not be reproduced by any counting method tried (including excluding brace-only lines: 688 now, 631 at close)."
    category: architectural
    reason: "The owner-accepted 761 is a measurement taken at phase close, not a standing cap on later phases. Growth since is 211 insertions / 46 deletions across 13 commits, all attributable to post-phase scope (known-games scan, About dialog, startup prompt, review-pass fixes). No extracted logic has come back: MainViewModel holds no bulk-run loops, sweep engine, or per-row flow bodies (no `foreach (var row in targets)` additions, no new `Progress<T>`), and the four coordinator constructions plus the explicit `IRowActionHost` block are intact. The structure the REF-02 must-have names holds; only the numeric figure is stale. If the owner wants 761 to stay a live ceiling, that is a decision for the owner and a future refactor phase."
    evidence_status: "measured this pass; owner decision on whether to re-accept a new figure requested but not blocking"
  - finding: "01-REVIEW.md WR-03 (previous WR-01): BulkBackupCoordinator.RestoreAsync's `finally` releases IsBusy but does not reset RestoreStep/RestoreStepText, and the rescan runs only after the call returns, so an unexpected throw from GameActions.RestoreAsync leaves rows on Checking until Refresh."
    category: other
    reason: "Re-confirmed at BulkBackupCoordinator.cs lines 259-267. Baseline MainViewModel.RestoreSelectedAsync (phase1-baseline, lines ~1730-1785) has the identical shape: `finally` releases only IsBusy and StartStatusScan(only: targets) runs after the try. So it is a pre-existing latent defect preserved by REF-01's parity contract, not introduced by the split. It requires a service to throw despite the never-throw contract. Outside REF-04's named defect list; does not contradict REF-03 (guard semantics preserved) because the semantics preserved are the baseline's."
    evidence_status: "reproduced against phase1-baseline this pass; disposition open in 01-REVIEW-DISPOSITION.md"
  - finding: "01-REVIEW.md WR-04: bulk backup discards each successful game's result.Message, so the partial-archive (unreadable paths) and restore-cap warnings never reach the user on a bulk run, which then reports 'Backed up all N games'."
    category: other
    reason: "Adjacent to REF-04 but not within it: REF-04 requires Scan status rows to surface skipped paths the way a backup does, which holds (GameRowViewModel.ApplyStatus + SaveStatus.SkippedPaths, verified below). The baseline bulk loop (phase1-baseline MainViewModel lines 1605-1609) also read result.Message only on failure, so this is pre-existing behavior the refactor preserved. The row's verdict after the rescan still reads LastBackupPartial."
    evidence_status: "reproduced against phase1-baseline this pass"
  - finding: "Review WR-01/WR-02/WR-05 and IN-01..IN-07 concern BackupService timestamping, DirectoryWalk MoveNext guarding, message punctuation, doc-comment drift, key placement, necessity and test coverage."
    category: other
    reason: "None names a Phase 1 must-have. WR-01 (archive timestamp) and WR-05 are in code that Phase 1 touched only for the rename-retry message; the stamping and warning-composition code is post-phase work. WR-02 is in DirectoryWalk's walk-level guard, whose documented contract Phase 1 did not change (Phase 1 only wired the existing onSkipped channel into Scan). All 12 findings remain 'open' in the disposition file and are for the owner's triage, not for gap closure here."
    evidence_status: "read against current code; none contradicts a must-have"
  - finding: "tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs — declared frozen by 01-01-PLAN.md — has been edited by later test-infrastructure cleanup (collection expressions, shared helpers)."
    category: other
    reason: "Carried forward from the 2026-11-24 pass: diffed against its creation commit 96a06a3, all 6 facts, assertions and the timeout are behaviorally unchanged. Re-run this pass: the 6 facts pass (6 [Fact] attributes present)."
    evidence_status: "diffed and re-run"
behavior_unverified_items: []
---

# Phase 1: Bugfixing & Refactoring Verification Report

**Phase Goal:** Close the remaining R2 audit defects and split `MainViewModel.cs` (1,885 lines) into sealed, single-responsibility coordinators — behavior-locked at every step, verified by the unchanged 629-test suite.
**Verified:** 2026-11-25T00:00:00Z
**Status:** passed
**Re-verification:** Yes — the prior report (2026-11-24, `passed`, 44/44) went stale because 15 covered files changed after it, through Phase 02 and 16 manual review-pass commits (`575f5ef`..`8a0c1db`) on `dev/v1.1`. This pass re-checked every must-have against the code as it stands now rather than carrying the prior verdict.

## What changed since the last pass, and the verdict on each

- **`RestoreService.cs`** changed after phase close (62 insertions, 5 deletions vs `f8665df`). The prior report's "byte-identical" claim no longer holds, and so T-01-12's baseline-diff evidence is gone. The destructive order was re-read in the code instead: safety backup (line 107, `Backup(..., "-pre-restore", ...)`) → confirm → extract to a temp sibling (line ~140) → `Directory.Move(saveDir, oldAsideDir)` (180) → `Directory.Move(tempDir, saveDir)` (199) with rollback of the aside on failure (208). A failed or incomplete safety backup aborts before anything is written (lines 108-121). `RestoreServiceTests` pins the ordering (`Restore_writes_a_pre_restore_safety_backup_before_swapping_in_the_restored_contents`, `Confirm_is_invoked_once_with_a_safety_backup_already_on_disk_and_save_dir_still_pre_restore`, `Restore_aborts_when_the_safety_backup_could_not_read_every_file`). The order is intact and tested.
- **`MainViewModel.cs`**: 1,556 → 1,821 lines. See Advisory #1; the composition-root structure holds.
- **`BackupService.cs`, `DirectoryWalk.cs`, `SaveStatusScanner.cs`, `SaveStatus.cs`, `GameRowViewModel.cs`, `StatusSweepCoordinator.cs`, `RowActionsCoordinator.cs`, `BulkBackupCoordinator.cs`, `BulkRunRules.cs`**: grew with later-phase and review-pass work. Every Phase 1 deliverable in them is still present and wired (rows 9-34 below). `GameRowViewModel`'s public surface: no `public`/`[ObservableProperty]` line differs from `f8665df`. `IDialogService.cs` differs by 3 added lines (additive members for later features).
- **`CHANGELOG.md`, `docs/DECISIONS.md`, `Strings.en.xaml`**: the REF-04 changelog entries ("rename retry limit reached", "A partly unreadable save folder is now visible"), the "composition root over sealed coordinators" decision row and `str_game_unreadablePaths` are all present.

## Goal Achievement

### Observable Truths

| # | Plan | Truth | Status | Evidence |
|---|------|-------|--------|----------|
| 1 | 01-01 | phase1-baseline tag + recorded baseline figures | ✓ VERIFIED (override) | `git rev-parse phase1-baseline` = `9d96b181…`; accepted deviation re-confirmed |
| 2 | 01-01 | Phase work on feature branch, main never pushed directly | ✓ VERIFIED | Unchanged commit history; current work on `dev/v1.1` |
| 3 | 01-01 | Guard pins characterize unchanged behavior and are frozen for Plans 02-05 | ✓ VERIFIED (see advisory #5) | 6 facts present and passing; mechanical edits only |
| 4-8 | 01-01 | Bulk claim order, mid-run guard, zero-row paths (pinned) | ✓ VERIFIED | `verify.artifacts` 1/1, `verify.key-links` 2/2; `MainViewModelGuardPinTests` passes (part of 46/46 filtered run and 772/772 full) |
| 9 | 01-02 | `BulkRunRules.NeedsBackup` is the single definition of the unsecured-verdict rule; no inline pattern left in MainViewModel | ✓ VERIFIED | `MainViewModel.cs` lines 320, 523, 900 call `BulkRunRules.NeedsBackup`; lines 1529, 1633, 1660, 1682 use the other `BulkRunRules` members. (The rule now also admits `LastBackupPartial` — post-phase scope; review IN-02 notes the doc comments still say "pair".) |
| 10 | 01-02 | Selection/outcome wording in `BulkRunRules` (internal static, no WPF/IO/state) | ✓ VERIFIED | `internal static class BulkRunRules`; 0 WPF/Dispatcher/MessageBox references |
| 11-15 | 01-02 | REF-05 contract test over hostile inputs, reflection-enumerated surface, no state leak | ✓ VERIFIED | `ServiceContractTests` passes (in the 46/46 filtered run) |
| 16-18 | 01-03 | Per-row blocked during bulk run; row order preserved; one guard owner (`IsAnyRowChecking`) | ✓ VERIFIED | `IsAnyRowChecking` is defined once, `MainViewModel.cs:333`; the coordinator only sets row state (`ScanToken++`, `ApplyBackupComparison(Checking)`, `IsBusy`) |
| 19 | 01-03 | (backstop) `Progress<T>` constructed on the UI thread, never in a coordinator | ✓ VERIFIED | `grep "new Progress<"` over the three coordinator files = 0 |
| 20-22 | 01-03 | Claim/release order documented; writes through `onUi`; coordinator `internal sealed`, built in MainViewModel's constructor | ✓ VERIFIED | `internal sealed class BulkBackupCoordinator`; `_bulk = new BulkBackupCoordinator(_backup, _gameActions, OnUi)` at `MainViewModel.cs:136`; claim order commented at `BulkBackupCoordinator.cs:~200-213` |
| 23-24 | 01-03 | Rename-retry cap is a distinguishable failure; sub-cap succeeds | ✓ VERIFIED | `BackupService.cs`: `MaxRenameRetries = 100` (line 37), retry filter at 279, cap branch at 285, message `"…rename retry limit reached — … up to -100 already exist…"` at 293; `BackupServiceTests` pass |
| 25-27 | 01-04 | `StatusSweepCoordinator` owns the sweep engine; token-checked writes through `onUi`; reachability memo only there | ✓ VERIFIED | `_sweep = new StatusSweepCoordinator(_backup, OnUi)` at line 137; 0 `_lastBackupRootReachable` references in MainViewModel |
| 28 | 01-04 | (backstop) Zero-row operation leaves `IsAnyRowChecking` unstuck | ✓ VERIFIED | Coordinator test facts pass in the full run; `RestoreAsync` returns `null` early for zero targets (`BulkBackupCoordinator.cs:196-197`) |
| 29-30 | 01-04 | Zero skipped paths silent; Scan carries `SaveStatus.SkippedPaths`; Scan and Backup agree | ✓ VERIFIED | `SaveStatus.SkippedPaths` (line 48); `SaveStatusScanner.cs:83` passes `onSkipped: skipped.Add`, `:96` sets `SkippedPaths`; `GameRowViewModel.cs:217-221` renders `str_game_unreadablePaths` via `DirectoryWalk.FormatSkippedList` (the same helper `BackupService` uses) |
| 31 | 01-04 | `GameRowViewModel` public surface unchanged; only `ApplyStatus` bodies changed for Phase 1 | ✓ VERIFIED | `git diff f8665df HEAD` shows 0 added/removed `public`/`[ObservableProperty]` lines |
| 32-34 | 01-04 | (backstop) Encoding-independent messages; repeated-scan reporting; Scan/Backup independence | ✓ VERIFIED | Both paths format through the one `FormatSkippedList`; no caching or cross-game lock |
| 35 | 01-05 | Moved methods removed from MainViewModel | ✓ VERIFIED | grep for `CheckRestorePreconditionsAsync\|PickRestoreAsync\|PathResolver.TryResolve\|ComputeSaveDataSize` in MainViewModel = 0 |
| 36 | 01-05 | Line count measured and recorded; ceiling reached or owner decision applied; composition-root structure holds | ✓ VERIFIED (override) | Owner "accept-measured" 1,556/761 at close stands. At HEAD the figure is exceeded (Advisory #1) but the structure holds: four coordinators built in one block (`:135-138`), no loop/engine bodies returned, `IRowActionHost` explicit |
| 37 | 01-05 | `RowActionsCoordinator` owns per-row flows; state only via `IRowActionHost` | ✓ VERIFIED | `MainViewModel : CommunityToolkitObservableObject, IRowActionHost` (`:24`); 9 explicit interface members at `:1786-1794`; `_rowActions = new RowActionsCoordinator(_backup, _gameActions, _dialog, this)` at `:138`; `internal interface IRowActionHost` + `internal sealed class RowActionsCoordinator`, 0 WPF references |
| 38 | 01-05 | Change save folder / Remove game stay on MainViewModel (D-P05-01) | ✓ VERIFIED | Decision row present in `docs/DECISIONS.md:47` |
| 39 | 01-05 | Per-row `IsBusy` raised before first await, released in finally | ✓ VERIFIED | Unchanged shape in `RowActionsCoordinator.cs`; `RowActionsCoordinatorTests` pass |
| 40-41 | 01-05 | Zero/single-game tests and the earlier MainViewModel tests behaviorally unchanged | ✓ VERIFIED | Full suite 772/772 on this host (grown from 629/689 by later phases; the phase's behavior-lock tests all pass) |
| 42-43 | 01-05 | (backstop) Zero-row no-op per coordinator; fixed construction order | ✓ VERIFIED | `_gameActions` → `_bulk` → `_sweep` → `_rowActions` at lines 135-138 |
| 44 | 01-05 | Guard semantics documented where they now live; exactly 2 shipping PackageReferences; no DI type; no WPF type in any coordinator | ✓ VERIFIED | `grep -c "<PackageReference"` csproj = 2; DI-type grep over `src/**/*.cs,*.xaml,*.csproj` = 0 (a plain `src` grep also hits compiled `bin/` DLLs, which are build output, not source); WPF-type grep over the four ViewModels files = 0 |

**Score:** 44/44 truths verified (2 via recorded owner overrides, re-confirmed). 0 present-but-behavior-unverified.

### Prohibitions

| Prohibition | Verification | Evidence |
|---|---|---|
| No new shipping NuGet package | RESOLVED | csproj has exactly 2 `PackageReference` (WPF-UI, CommunityToolkit.Mvvm) |
| No DI container / non-manual construction | RESOLVED | 0 DI type references in source; collaborators `new`'d in MainViewModel's constructor |
| `GameRowViewModel` public surface / `IDialogService` contract not modified by Phase 1 | RESOLVED | No public-surface diff in `GameRowViewModel`; `IDialogService` gained only additive members for later features |

### Deferred Items

None.

### Advisory (New Scope, Unevidenced)

| # | Finding | Category | Why Advisory |
|---|---------|----------|--------------|
| 1 | `MainViewModel.cs` now 1,821 lines / ~837 code-only (my reproducible method) vs the accepted 761 | architectural | Point-in-time owner acceptance; structure intact; growth is later-phase scope. The supplied "~672" is not reproducible. Owner may want to re-accept a figure or schedule a further split. |
| 2 | Review WR-03 — bulk-restore `finally` leaves `Checking`/restore bar on a thrown item | other | Identical in `phase1-baseline`; preserved by REF-01 parity; needs a service to violate its never-throw contract |
| 3 | Review WR-04 — bulk backup drops per-game partial/oversize warnings on success | other | Identical in `phase1-baseline`; REF-04's row-level surfacing is satisfied |
| 4 | Review WR-01, WR-02, WR-05, IN-01..IN-07 | other | In post-phase code or outside any Phase 1 must-have; open in the disposition file |
| 5 | `MainViewModelGuardPinTests.cs` edited after being declared frozen | other | Mechanical syntax/helper changes only; all 6 facts pass |

### Required Artifacts

`verify.artifacts` re-run on all five plans against current HEAD: 1/1, 3/3, 4/4, 5/5, 3/3 = 16/16 passed (`all_passed: true` each; no MISSING, no STUB). Artifacts: `MainViewModelGuardPinTests.cs`, `BulkRunRules.cs`, `BulkRunRulesTests.cs`, `ServiceContractTests.cs`, `BulkBackupCoordinator.cs`, `BulkBackupCoordinatorTests.cs`, `BackupService.cs` (`MaxRenameRetries`), `CHANGELOG.md`, `StatusSweepCoordinator.cs`, `StatusSweepCoordinatorTests.cs`, `SaveStatus.cs` (`SkippedPaths`), `DirectoryWalk.cs` (`FormatSkippedList`), `Strings.en.xaml` (`str_game_unreadablePaths`), `RowActionsCoordinator.cs`, `RowActionsCoordinatorTests.cs`, `DECISIONS.md`.

### Key Link Verification

`verify.key-links` re-run on all five plans: 2/2, 2/2, 3/3, 4/4, 3/3 = 14/14 verified, 0 NOT_WIRED, 0 PARTIAL.

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| `GameRowViewModel.ApplyStatus` | unreadable-paths status text | `SaveStatus.SkippedPaths` ← `SaveStatusScanner` ← `DirectoryWalk.ForEachFile(onSkipped)` | yes | ✓ FLOWING |
| `BulkBackupCoordinator` | per-row byte counters | `BackupService.Backup` byte progress | yes | ✓ FLOWING |
| `MainViewModel.IsAnyRowChecking` | aggregate | live `Games` row `ComparisonKind` | yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Full build + suite on Windows | `dotnet test SavegameBunker.sln -tl:off` | `Failed: 0, Passed: 772, Total: 772` | ✓ PASS |
| REF-05 contract, guard pins, restore order | `dotnet test --no-build --filter "ServiceContractTests\|MainViewModelGuardPinTests\|RestoreServiceTests"` | `Passed: 46, Failed: 0` | ✓ PASS |
| Debt markers (`TBD`/`FIXME`/`XXX`) in all phase-modified source, strings, changelog, decisions | grep | 0 matches; 0 `TODO`/`HACK`/`PLACEHOLDER` in ViewModels and phase services | ✓ PASS |

### Probe Execution

No probes declared by the phase; `scripts/` holds only an unrelated help-pack link checker. Not applicable.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ---------- | ----------- | ------ | -------- |
| REF-01 | 01-01, 01-03, 01-05 | Behavior parity; suite passes unchanged | ✓ SATISFIED | 772/772; guard pins and the pre-existing MainViewModel tests pass; WR-03/WR-04 reproduced as baseline behavior |
| REF-02 | 01-02, 01-03, 01-04, 01-05 | Composition root; three sealed coordinators, no WPF references | ✓ SATISFIED (accepted-measured; figure now exceeded, structure intact) | Truths 20-22, 25-27, 36-37; Advisory #1 |
| REF-03 | 01-01, 01-03, 01-04, 01-05 | Uniform `OnUi`/`IProgress` discipline; guard semantics preserved and documented | ✓ SATISFIED | Truths 16-22, 26, 39, 44 |
| REF-04 | 01-03, 01-04 | Rename-retry message distinguishable; Scan surfaces skipped paths like Backup | ✓ SATISFIED | Truths 23-24, 29-30; CHANGELOG entries present |
| REF-05 | 01-02 | Never-throw service contract, contract test over the Services surface | ✓ SATISFIED | `ServiceContractTests` passes |

All five IDs appear in at least one PLAN's `requirements` frontmatter (01-01: REF-01, REF-03; 01-02: REF-02, REF-05; 01-03: REF-01..04; 01-04: REF-02..04; 01-05: REF-01..03) and in REQUIREMENTS.md's traceability table (all Phase 1, all `[x]`). Orphaned requirements: none. Note: the previous report listed plan 01-02 under REF-02 and REF-05 only, which matches frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| (none) | — | Debt markers in phase-modified files | — | 0 matches |
| `BulkBackupCoordinator.cs` | 259-267 | `finally` does not reset restore-bar fields (review WR-03) | ℹ️ Info (pre-existing) | Advisory #2 |

No Blocker anti-patterns.

### Human Verification Required

None. `01-UAT.md` is `complete` (3 harvested `<human-check>` items resolved). Two things the UAT covered have changed code paths since (bulk and per-row flows have had review-pass edits); the automated coverage on those paths (`BulkBackupCoordinatorTests`, `RowActionsCoordinatorTests`, `StatusSweepCoordinatorTests`, restore tests, 772/772) is the evidence here, and no must-have asserts an unexercised state transition.

### Gaps Summary

No gaps. The goal holds at current HEAD: the defects in REF-04 are fixed and still shipped, the never-throw contract test passes, the bulk-run, status-sweep and per-row flows live in `internal sealed` coordinators with no WPF types, constructed in order in `MainViewModel`'s constructor with no DI container and two shipping packages, and the behavior-lock suite is green (772/772).

One item needs the owner's eye, though it is not a gap: the REF-02 numeric ceiling the owner accepted (761 code-only) is exceeded at HEAD (~837 by the method that reproduces it), because later phases added scope to the file. That is growth, not regression of the split, and I did not treat a post-phase figure as a Phase 1 failure. The 5 warning-level review findings do not contradict a must-have: WR-03 and WR-04 are baseline behavior preserved by the parity contract, and WR-01, WR-02, WR-05 live in post-phase code. They remain `open` in `01-REVIEW-DISPOSITION.md` for triage.

---

_Verified: 2026-11-25T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
