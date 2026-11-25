---
phase: 01-bugfixing-refactoring
verified: 2026-11-25T10:30:00Z
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
  - SPEC.md
  - docs/DECISIONS.md
  - src/SavegameBunker/Models/OperationResult.cs
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
  - tests/SavegameBunker.Tests/DirectoryWalkTests.cs
  - tests/SavegameBunker.Tests/GameRowViewModelTests.cs
  - tests/SavegameBunker.Tests/MainViewModelBulkTests.cs
  - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
  - tests/SavegameBunker.Tests/RestoreServiceTests.cs
  - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
  - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
covered_digest: "v4:sha256:5ddac1d83fcca48641024772edd1d231642b7603c8d7f80299fd655894b459a4"
behavior_unverified: 0
overrides_applied: 2
overrides:
  - must_have: "phase1-baseline tag marks the last commit before any phase-1 code change (tag recreated on the feature branch rather than on main)"
    reason: "Plan-time intent was a main-HEAD tag; execution branched from main at 9d96b181 and tagged that commit on refactor/v1.1-phase-1 instead. `git rev-parse phase1-baseline` = 9d96b1814cc7 (re-confirmed this pass) — the tagged commit is the exact last pre-phase commit, so diff-baseline semantics hold unchanged. Carried forward across two prior re-verifications."
    accepted_by: "owner"
    accepted_at: "2026-11-17"
  - must_have: "MainViewModel.cs reduced to a composition root of at most ~700 lines (REF-02 ceiling)"
    reason: "Measured at phase close (commit f8665df / tag v1.0.1): 1,556 total lines / 761 code-only. Owner decision 'accept-measured' recorded in 01-05-SUMMARY.md and applied to REQUIREMENTS.md's REF-02 line. Carried forward as a point-in-time acceptance; this pass re-confirms the composition-root structure at current HEAD (1,830 lines) and records that the file has grown further past the accepted figure for reasons unrelated to the split (see Advisory #1)."
    accepted_by: "owner (accept-measured)"
    accepted_at: "2026-11-17"
re_verification:
  previous_status: passed
  previous_score: 44/44
  gaps_closed: []
  gaps_remaining: []
  regressions: []
advisory:
  - finding: "REF-02 ceiling exceeded at current HEAD. MainViewModel.cs is 1,830 lines total (1,556 at phase close, 1,821 at the last verification pass before the review-fix commits landed). A non-blank/non-comment count gives 886 lines now."
    category: architectural
    reason: "The owner-accepted 761 is a measurement taken at phase close, not a standing cap on later phases. The 9-line growth since the prior verification pass is entirely the WR-03/WR-04 review-fix wiring: `RunBulkBackupAsync` now concatenates `run.Warnings` into the Report() call, `RestoreSelectedAsync` moved its rescan into the `onFinished` callback, and `Report`'s parameter was renamed/widened from `failureDetails` to `details` with one added branch for a successful-but-warned result (diff verified: `git diff 8a0c1db HEAD -- src/SavegameBunker/ViewModels/MainViewModel.cs`, 24 lines changed, all call-site plumbing, no loop/engine body returned). No extracted logic has come back: MainViewModel holds no bulk-run loops, sweep engine, or per-row flow bodies. The four coordinator constructions plus the explicit `IRowActionHost` block are intact at lines 135-138 and 1786-1794 respectively. The structure the REF-02 must-have names holds; only the numeric figure is stale. Re-accepting a new figure (or scheduling a further split) is an owner decision, not a Phase 1 gap."
    evidence_status: "measured this pass; owner decision on whether to re-accept a new figure requested but not blocking"
  - finding: "01-REVIEW.md WR-01: a backup's timestamp is taken after verification, so a save rewritten during the backup window can read \"Up to date\" despite not being in the archive."
    category: other
    reason: "Explicitly skipped by 01-REVIEW-FIX.md as a design decision, not an oversight: SPEC.md section 3 and a code comment at BackupService.cs:112-115 both document the late stamp as deliberate (it trades a rare silent-miss window for never dating an archive earlier than the saves it captured). The reviewer's own fix proposal changes SPEC behavior and archive-naming semantics, so it was correctly left for the owner rather than applied unilaterally. Outside every Phase 1 must-have (REF-04's named defects are the rename-retry cap and Scan/Backup skip-path parity, neither of which this is)."
    evidence_status: "read against current code; skip is documented with reasoning in 01-REVIEW-FIX.md; disposition remains 'skipped' in 01-REVIEW-DISPOSITION.md"
  - finding: "01-REVIEW.md IN-01..IN-07 (necessity/doc-comment/key-placement/test-coverage nits) remain open in 01-REVIEW-DISPOSITION.md."
    category: other
    reason: "None names a Phase 1 must-have; all are Info-severity. IN-02 (doc comments say 'pair' after a third verdict joined the unsecured rule) and IN-07 (no coordinator-level test for the backup-root-aware sweep) are the most substantive, but neither contradicts a must-have truth: `BulkRunRules.NeedsBackup`'s behavior (truth 9) and `StatusSweepCoordinator`'s token-checked writes (truth 25-27) are both independently verified against code and passing tests, the doc-comment drift is cosmetic, and the sweep behavior is covered by `BackupServiceTests` directly even if not through the coordinator. For the owner's triage."
    evidence_status: "read against current code; none contradicts a must-have"
  - finding: "tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs — declared frozen by 01-01-PLAN.md — continues to show only mechanical edits since its creation commit."
    category: other
    reason: "Carried forward from the two prior passes: diffed against its creation commit 96a06a3, all 6 facts, assertions and the timeout are behaviorally unchanged (collection-expression style, shared TempDir/WaitForAsync/RecordingDialogService helpers). Re-confirmed this pass: 6 [Fact] attributes present and passing in the 781/781 full run."
    evidence_status: "diffed and re-run"
behavior_unverified_items: []
---

# Phase 1: Bugfixing & Refactoring Verification Report

**Phase Goal:** Close the remaining R2 audit defects and split `MainViewModel.cs` (1,885 lines) into sealed, single-responsibility coordinators — behavior-locked at every step, verified by the unchanged 629-test suite.
**Verified:** 2026-11-25T10:30:00Z
**Status:** passed
**Re-verification:** Yes — the prior report (2026-11-25T00:00:00Z, `passed`, 44/44) went stale because the code-review fix pass (`01-REVIEW-FIX.md`, commits `e9e7c78`..`214f6ba`) landed four fixes after it ran, plus a docs commit (`56f8e78`) recording the fix pass and disposition. This pass re-checked every must-have, re-ran the full suite, and read the four fixed files directly rather than carrying the prior verdict forward.

## What changed since the last pass, and the verdict on each

- **`src/SavegameBunker/Services/DirectoryWalk.cs`** (commit `e9e7c78`, WR-02): added `GuardListing`, a hand-driven enumerator wrapper that turns a mid-listing `MoveNext` fault into "this listing ends here, report the folder as skipped once" instead of unwinding the whole walk. `Walk` now reads both the file and subfolder listings through it. Confirmed in code: `ForEachFile`'s `foreach (var f in GuardListing(files, ListingFaulted))` and the subdirs equivalent; `GuardListing` itself catches `FileSystemFault.Is(ex)` around `MoveNext`, not around the consumer's own exceptions. New `DirectoryWalkTests.cs` pins the helper directly.
- **`src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs`** (commit `be8169b`, WR-03): `RestoreAsync`'s `finally` now also zeroes `RestoreStep`/`RestoreStepText` per target, and the rescan moved from `MainViewModel.RestoreSelectedAsync` (after the awaited call) into the `onFinished` callback invoked from the `finally` — mirroring the backup path exactly. Confirmed at lines 266-279: `finally { foreach (var row in targets) { row.IsBusy = false; row.RestoreStep = 0; row.RestoreStepText = ""; } onFinished(); }`.
- **`src/SavegameBunker/Models/OperationResult.cs`, `BackupService.cs`, `BulkBackupCoordinator.cs`, `MainViewModel.cs`** (commit `30d8634`, WR-04): `OperationResult` gained `HasWarning`/`OkWithWarning`; `BackupService.Backup` sets it for skipped paths, pruned subfolders and an oversize archive, never for the left-out note. `BulkBackupRun` gained `Warnings`; the bulk loop collects each warned success's message. `MainViewModel.Report` now appends its `details` list on a success dialog too, and the bulk-backup call site passes `[.. run.FailureDetails, .. run.Warnings]`. `SPEC.md`'s partial-archive bullet and `CHANGELOG.md` both document the change.
- **`src/SavegameBunker/Services/BackupService.cs`** (commit `214f6ba`, WR-05): `DescribeSkippedPathsWarning` and `DescribeNotBackedUpWarning` now end with a full stop, matching `DescribeLeftOutNote`.
- **Everything else** (`BulkRunRules.cs`, `GameRowViewModel.cs`, `StatusSweepCoordinator.cs`, `RowActionsCoordinator.cs`, `SaveStatus.cs`, `SaveStatusScanner.cs`, `Strings.en.xaml`, `RestoreService.cs`, `docs/DECISIONS.md`) is byte-for-byte unchanged since the prior verification pass (`git diff 8a0c1db HEAD` touches none of them except `MainViewModel.cs`'s 24-line call-site plumbing, already covered above).

## Goal Achievement

### Observable Truths

| # | Plan | Truth | Status | Evidence |
|---|------|-------|--------|----------|
| 1 | 01-01 | phase1-baseline tag + recorded baseline figures | ✓ VERIFIED (override) | `git rev-parse phase1-baseline` = `9d96b181…`; accepted deviation re-confirmed |
| 2 | 01-01 | Phase work on feature branch, main never pushed directly | ✓ VERIFIED | Work continues on `dev/v1.1`, 21 commits ahead of `origin/dev/v1.1`, unpushed |
| 3 | 01-01 | Guard pins characterize unchanged behavior and are frozen for Plans 02-05 | ✓ VERIFIED (see advisory #4) | 6 `[Fact]` attributes present and passing; diff against creation commit shows mechanical edits only |
| 4-8 | 01-01 | Bulk claim order, mid-run guard, zero-row paths (pinned) | ✓ VERIFIED | `verify.artifacts` 1/1, `verify.key-links` 2/2; `MainViewModelGuardPinTests` passes (781/781 full run) |
| 9 | 01-02 | `BulkRunRules.NeedsBackup` is the single definition of the unsecured-verdict rule; no inline pattern left in MainViewModel | ✓ VERIFIED | `MainViewModel.cs` calls `BulkRunRules.NeedsBackup`/other members at multiple call sites; `internal static class BulkRunRules` holds the only definition |
| 10 | 01-02 | Selection/outcome wording in `BulkRunRules` (internal static, no WPF/IO/state) | ✓ VERIFIED | `internal static class BulkRunRules`; 0 WPF/Dispatcher/MessageBox references |
| 11-15 | 01-02 | REF-05 contract test over hostile inputs, reflection-enumerated surface, no state leak | ✓ VERIFIED | `ServiceContractTests` passes in the 781/781 full run |
| 16-18 | 01-03 | Per-row blocked during bulk run; row order preserved; one guard owner (`IsAnyRowChecking`) | ✓ VERIFIED | `IsAnyRowChecking` defined once in `MainViewModel.cs`; coordinators only set row state |
| 19 | 01-03 | (backstop) `Progress<T>` constructed on the UI thread, never in a coordinator | ✓ VERIFIED | `grep "new Progress<"` over all three coordinator files = 0 |
| 20-22 | 01-03 | Claim/release order documented; writes through `onUi`; coordinator `internal sealed`, built in MainViewModel's constructor | ✓ VERIFIED | `internal sealed class BulkBackupCoordinator`; `_bulk = new BulkBackupCoordinator(_backup, _gameActions, OnUi)` at `MainViewModel.cs:136`; claim/release order documented in the class remarks and re-confirmed in the WR-03 fix |
| 23-24 | 01-03 | Rename-retry cap is a distinguishable failure; sub-cap succeeds | ✓ VERIFIED | `BackupService.cs`: `MaxRenameRetries = 100`; cap branch returns `"…rename retry limit reached — … up to -100 already exist…"`; `BackupServiceTests` pass |
| 25-27 | 01-04 | `StatusSweepCoordinator` owns the sweep engine; token-checked writes through `onUi`; reachability memo only there | ✓ VERIFIED | `_sweep = new StatusSweepCoordinator(_backup, OnUi)` at `MainViewModel.cs:137`; 0 `_lastBackupRootReachable` references in MainViewModel |
| 28 | 01-04 | (backstop) Zero-row operation leaves `IsAnyRowChecking` unstuck | ✓ VERIFIED | `RestoreAsync` and `BackupAsync` both return `null` early for zero targets before touching anything |
| 29-30 | 01-04 | Zero skipped paths silent; Scan carries `SaveStatus.SkippedPaths`; Scan and Backup agree | ✓ VERIFIED | `SaveStatus.SkippedPaths`; `SaveStatusScanner.cs:96` sets it via `DirectoryWalk.ForEachFile(onSkipped: skipped.Add)`; `GameRowViewModel.cs:217-221` renders `str_game_unreadablePaths` via the same `DirectoryWalk.FormatSkippedList` `BackupService` uses |
| 31 | 01-04 | `GameRowViewModel` public surface unchanged; only `ApplyStatus` bodies changed for Phase 1 | ✓ VERIFIED | `git diff phase1-baseline HEAD -- GameRowViewModel.cs` shows 0 added/removed `public`/`[ObservableProperty]` lines |
| 32-34 | 01-04 | (backstop) Encoding-independent messages; repeated-scan reporting; Scan/Backup independence | ✓ VERIFIED | Both paths format through the one `FormatSkippedList`; no caching or cross-game lock |
| 35 | 01-05 | Moved methods removed from MainViewModel | ✓ VERIFIED | grep for `CheckRestorePreconditionsAsync\|PickRestoreAsync\|PathResolver.TryResolve\|ComputeSaveDataSize` in MainViewModel.cs = 0 |
| 36 | 01-05 | Line count measured and recorded; ceiling reached or owner decision applied; composition-root structure holds | ✓ VERIFIED (override) | Owner "accept-measured" 1,556/761 at close stands. At current HEAD the figure is exceeded further (1,830 lines / 886 code-only; Advisory #1) but the structure holds: four coordinators built in one block (`:135-138`), no loop/engine bodies returned, `IRowActionHost` explicit |
| 37 | 01-05 | `RowActionsCoordinator` owns per-row flows; state only via `IRowActionHost` | ✓ VERIFIED | `MainViewModel : CommunityToolkitObservableObject, IRowActionHost`; explicit interface members present; `_rowActions = new RowActionsCoordinator(_backup, _gameActions, _dialog, this)` at `:138`; `internal interface IRowActionHost` + `internal sealed class RowActionsCoordinator`, 0 WPF references |
| 38 | 01-05 | Change save folder / Remove game stay on MainViewModel (D-P05-01) | ✓ VERIFIED | Decision row present in `docs/DECISIONS.md` |
| 39 | 01-05 | Per-row `IsBusy` raised before first await, released in finally | ✓ VERIFIED | Unchanged shape in `RowActionsCoordinator.cs`; `RowActionsCoordinatorTests` pass |
| 40-41 | 01-05 | Zero/single-game tests and the earlier MainViewModel tests behaviorally unchanged | ✓ VERIFIED | Full suite 781/781 on this host (grown from the 629 baseline via later-phase and review-fix tests; every Phase 1 behavior-lock test passes) |
| 42-43 | 01-05 | (backstop) Zero-row no-op per coordinator; fixed construction order | ✓ VERIFIED | `_gameActions` → `_bulk` → `_sweep` → `_rowActions` constructed in that order, `MainViewModel.cs:135-138` |
| 44 | 01-05 | Guard semantics documented where they now live; exactly 2 shipping PackageReferences; no DI type; no WPF type in any coordinator | ✓ VERIFIED | `grep -c "<PackageReference"` csproj = 2; DI-type grep over source-only `src/**/*.cs,*.xaml,*.csproj` (excluding `bin/`/`obj/`) = 0; WPF-type grep over the four ViewModels coordinator files = 0 |

**Score:** 44/44 truths verified (2 via recorded owner overrides, re-confirmed). 0 present-but-behavior-unverified.

### Prohibitions

Each phase PLAN's `must_haves.prohibitions` block is authored "descriptor-less (flagged-unverified)" — no `verification: test|judgment` tier was set at plan time. All three distinct statements repeat across the five plans and are resolved here by direct, deterministic code inspection (not estimation):

| Prohibition | Verification | Evidence |
|---|---|---|
| No new shipping NuGet package | RESOLVED | `SavegameBunker.csproj` has exactly 2 `PackageReference` elements (WPF-UI, CommunityToolkit.Mvvm) |
| No DI container / non-manual construction | RESOLVED | 0 `Microsoft.Extensions.DependencyInjection`/`IServiceCollection`/`IServiceProvider` references in `src/**/*.cs,*.xaml,*.csproj` (excluding build output); all four coordinators `new`'d directly in `MainViewModel`'s constructor |
| `GameRowViewModel` public surface / `IDialogService` contract not modified by Phase 1 | RESOLVED | `git diff phase1-baseline HEAD -- GameRowViewModel.cs` shows 0 added/removed `public`/`[ObservableProperty]` lines; `IDialogService` gained only additive members for later, out-of-phase features |

### Deferred Items

None.

### Advisory (New Scope, Unevidenced)

| # | Finding | Category | Why Advisory |
|---|---------|----------|--------------|
| 1 | `MainViewModel.cs` now 1,830 lines / 886 non-blank-non-comment lines vs the accepted 761 | architectural | Point-in-time owner acceptance; structure intact; the growth since the last pass (9 lines) is entirely the WR-03/WR-04 fix's call-site plumbing, verified by diff. Owner may want to re-accept a figure or schedule a further split. |
| 2 | Review WR-01 — backup timestamp taken after verification, can read "Up to date" on a save rewritten mid-backup | other | Explicit, documented, deliberate design decision (SPEC.md + code comment); reviewer's own fix would change SPEC behavior; correctly left open for the owner, not a Phase 1 must-have |
| 3 | Review IN-01..IN-07 | other | Info-severity, in post-phase or cosmetic territory; open in the disposition file for triage |
| 4 | `MainViewModelGuardPinTests.cs` edited after being declared frozen | other | Mechanical syntax/helper changes only across three verification passes; all 6 facts pass each time |

### Required Artifacts

`verify.artifacts` run on all five plans against current HEAD: 1/1, 3/3, 4/4, 5/5, 3/3 = 16/16 passed (`all_passed: true` each; no MISSING, no STUB).

### Key Link Verification

`verify.key-links` run on all five plans: 2/2, 2/2, 3/3, 4/4, 3/3 = 14/14 verified, 0 NOT_WIRED, 0 PARTIAL.

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| `GameRowViewModel.ApplyStatus` | unreadable-paths status text | `SaveStatus.SkippedPaths` ← `SaveStatusScanner` ← `DirectoryWalk.ForEachFile(onSkipped)` | yes | ✓ FLOWING |
| `BulkBackupCoordinator.BackupAsync` | per-game warning messages | `OperationResult.HasWarning`/`Message` ← `BackupService.Backup` | yes | ✓ FLOWING |
| `MainViewModel.Report` (bulk backup) | dialog detail list | `run.FailureDetails` + `run.Warnings` from `BulkBackupCoordinator` | yes | ✓ FLOWING |
| `MainViewModel.IsAnyRowChecking` | aggregate | live `Games` row `ComparisonKind` | yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Full build + suite on Windows | `dotnet test SavegameBunker.sln -tl:off` | `Failed: 0, Passed: 781, Skipped: 0, Total: 781` | ✓ PASS |
| Debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) in all phase-modified source | grep over all 11 covered `src/` files | 0 matches | ✓ PASS |
| WR-02 fix present | `grep GuardListing DirectoryWalk.cs` | hand-driven enumerator wrapper found, wired into both file and subfolder listings | ✓ PASS |
| WR-03 fix present | read `BulkBackupCoordinator.cs:266-279` | `finally` zeroes `RestoreStep`/`RestoreStepText`; rescan moved to `onFinished` | ✓ PASS |
| WR-04 fix present | read `OperationResult.cs`, `BackupService.cs`, `MainViewModel.cs:1635-1637` | `HasWarning`/`OkWithWarning` added; bulk backup now reports `[.. FailureDetails, .. Warnings]` | ✓ PASS |
| WR-05 fix present | read `BackupService.cs:340-341,360-361` | both warning builders now end with a full stop | ✓ PASS |

### Probe Execution

No probes declared by the phase; `scripts/` holds only an unrelated help-pack link checker. Not applicable.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ---------- | ----------- | ------ | -------- |
| REF-01 | 01-01, 01-03, 01-05 | Behavior parity; suite passes unchanged | ✓ SATISFIED | 781/781; guard pins and the earlier MainViewModel tests pass; the WR-fix commits are documented, owner-visible bug fixes to Phase-1-authored code, not undocumented behavior drift |
| REF-02 | 01-02, 01-03, 01-04, 01-05 | Composition root; three sealed coordinators, no WPF references | ✓ SATISFIED (accepted-measured; figure exceeded further, structure intact) | Truths 20-22, 25-27, 36-37; Advisory #1 |
| REF-03 | 01-01, 01-03, 01-04, 01-05 | Uniform `OnUi`/`IProgress` discipline; guard semantics preserved and documented | ✓ SATISFIED | Truths 16-22, 26, 39, 44 |
| REF-04 | 01-03, 01-04 | Rename-retry message distinguishable; Scan surfaces skipped paths like Backup | ✓ SATISFIED | Truths 23-24, 29-30; CHANGELOG entries present |
| REF-05 | 01-02 | Never-throw service contract, contract test over the Services surface | ✓ SATISFIED | `ServiceContractTests` passes; `BackupService`'s new `HasWarning`/`OkWithWarning` members are additive return-value fields, not a new throw path |

All five IDs appear in at least one PLAN's `requirements` frontmatter (01-01: REF-01, REF-03; 01-02: REF-02, REF-05; 01-03: REF-01..04; 01-04: REF-02..04; 01-05: REF-01..03) and in REQUIREMENTS.md's traceability table (all Phase 1, all `[x]`). Orphaned requirements: none.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| (none) | — | Debt markers in phase-modified files | — | 0 matches |

No Blocker anti-patterns. The previous pass's ℹ️ Info finding about `BulkBackupCoordinator.cs`'s `finally` not resetting the restore bar (WR-03) is resolved by commit `be8169b` and no longer applies.

### Human Verification Required

None. `01-UAT.md` is `complete` (3/3 passed, 0 issues). The code paths the review-fix pass touched (bulk restore's `finally`, bulk backup's warning plumbing, DirectoryWalk's mid-listing guard, warning punctuation) are each covered by a new or updated automated test (`BulkBackupCoordinatorTests`, `BackupServiceTests`, `DirectoryWalkTests`, `MainViewModelBulkTests`), confirmed red-before-fix per `01-REVIEW-FIX.md`, and green in the current 781/781 run. No must-have asserts an unexercised state transition.

### Gaps Summary

No gaps. The goal holds at current HEAD: the REF-04 defects are fixed and still shipped, the never-throw contract test passes, the bulk-run, status-sweep and per-row flows live in `internal sealed` coordinators with no WPF types, constructed in order in `MainViewModel`'s constructor with no DI container and two shipping packages, and the behavior-lock suite is green (781/781) — up from the prior pass's 772 by the nine review-fix tests (`DirectoryWalkTests` new; one new fact each in `BulkBackupCoordinatorTests`, `BackupServiceTests`, `MainViewModelBulkTests`).

Two items remain for the owner's eye, neither a gap:
1. The REF-02 numeric ceiling (761 code-only, accepted at phase close) is exceeded further at current HEAD (886 by the same counting method), entirely from the WR-03/WR-04 fix's call-site plumbing in `MainViewModel.Report`. This is growth from closing review findings in Phase-1-authored code, not regression of the split.
2. WR-01 (backup timestamp) was deliberately skipped as a design decision requiring the owner's call — correctly recorded as `skipped` with reasoning in `01-REVIEW-FIX.md`, not silently dropped.

---

_Verified: 2026-11-25T10:30:00Z_
_Verifier: Claude (gsd-verifier)_
