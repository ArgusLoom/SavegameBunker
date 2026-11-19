---
phase: 01-bugfixing-refactoring
verified: 2026-11-19T18:50:00Z
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
  - .planning/phases/01-bugfixing-refactoring/01-REVIEW-DISPOSITION.md
  - .planning/phases/01-bugfixing-refactoring/01-REVIEW.md
  - .planning/phases/01-bugfixing-refactoring/01-UAT.md
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
covered_digest: "v4:sha256:b0d027ecb673459355cbf448dbfe1683e7c6d79b282fb51cedc546eeecc3c01e"
behavior_unverified: 0
overrides_applied: 2
overrides:
  - must_have: "phase1-baseline tag marks the last commit before any phase-1 code change (tag recreated on the feature branch rather than on main)"
    reason: "Plan-time intent was a main-HEAD tag; execution branched from main at 9d96b181 and tagged that commit on refactor/v1.1-phase-1 instead. Re-confirmed again this pass: `git rev-parse phase1-baseline` = 9d96b181 — the tagged commit is the exact last pre-phase commit, so diff-baseline semantics hold unchanged. Carried forward unchanged from the 2026-11-19 pass (no source change since)."
    accepted_by: "owner"
    accepted_at: "2026-11-17"
  - must_have: "MainViewModel.cs reduced to a composition root of at most ~700 lines (REF-02 ceiling)"
    reason: "Measured at phase close (tag v1.0.1): 1,556 total lines / 761 code-only. Re-measured independently in this pass with `git show v1.0.1:...MainViewModel.cs | wc -l` = 1556, confirming the figure again. Owner decision 'accept-measured' recorded in 01-05-SUMMARY.md and applied to REQUIREMENTS.md's REF-02 line. Carried forward unchanged (no source change since)."
    accepted_by: "owner (accept-measured)"
    accepted_at: "2026-11-17"
re_verification:
  previous_status: human_needed
  previous_score: 44/44
  gaps_closed:
    - "01-03 human-check: bulk restore with declined confirmations and a large-file progress bar — now covered verbatim by 01-UAT.md Test 2 (result: pass)"
    - "01-04 human-check: deny-ACL unreadable save subfolder surfaces the warning sentence — now covered verbatim by 01-UAT.md Test 3 (result: pass)"
  gaps_remaining: []
  regressions: []
advisory:
  - finding: "01-REVIEW.md WR-01: BulkBackupCoordinator.RestoreAsync's loop `finally` clears IsBusy for every target but not RestoreStep/RestoreStepText, so a row whose GameActions.RestoreAsync throws mid-bulk-restore would show a permanently-stuck restore bar even though IsBusy correctly releases."
    category: other
    reason: "Re-confirmed against the pre-phase baseline this pass: the identical gap exists in the original pre-split code at 9d96b181, so this is a pre-existing latent defect the phase's REF-01 behavior-parity contract required preserving verbatim, not a regression introduced by the coordinator extraction. Outside REF-04's named defect list; does not fail any must-have in 01-01..01-05's frontmatter. 01-REVIEW-DISPOSITION.md still lists WR-01/WR-02/WR-03 as 'open' (not fixed, not deferred); no source has changed since the review, so the review and its disposition stand unaltered."
    evidence_status: "reproduced against baseline in the prior pass; not covered by any must-have in this phase's plans; WR-02/WR-03 are 'necessity' (code-simplification) findings over unreachable default branches in GameRowViewModel, same open/unresolved status"
behavior_unverified_items: []
---

# Phase 1: Bugfixing & Refactoring Verification Report

**Phase Goal:** Close the remaining R2 audit defects and split `MainViewModel.cs` (1,885 lines) into sealed, single-responsibility coordinators — behavior-locked at every step, verified by the unchanged 629-test suite.
**Verified:** 2026-11-19T18:50:00Z
**Status:** passed
**Re-verification:** Yes — the prior `01-VERIFICATION.md` (dated 2026-11-19, `status: human_needed`) predated UAT completion. This pass re-confirms it against the now-complete `01-UAT.md` (3/3 passed, 0 issues, status `complete`).

## What changed since the prior verification pass

`git log --oneline 39f2552..HEAD` shows exactly one commit since the prior verification pass:
`08d2eac test(01): complete UAT - 3 passed, 0 issues`, which touches only
`.planning/phases/01-bugfixing-refactoring/01-UAT.md` (`git diff --stat 39f2552 HEAD -- src tests
CHANGELOG.md docs/DECISIONS.md .planning/REQUIREMENTS.md` returns empty — zero source, test, or
doc changes). The prior pass's codebase evidence (44/44 truths, 16/16 artifacts, 14/14 key links,
clean build, green suite) therefore still describes the current tree exactly. This pass
independently re-ran the load-bearing checks rather than copying numbers forward blind: Release
build (0/0), full test suite, the REF-05 contract test alone, `verify.artifacts`/`verify.key-links`
for all five plans, the `v1.0.1` `MainViewModel.cs` line count, the `phase1-baseline` tag, the
shipping-package count, and a debt-marker sweep across all eleven phase-modified source files —
every one reproduced the prior pass's figures exactly (see Behavioral Spot-Checks below).

The one substantive change is `01-UAT.md` itself: its two previously-pending human-check items are
now recorded with specific, matching, passing results (see Human Verification Required below),
which is what moves the overall status from `human_needed` to `passed`.

## Goal Achievement

### Observable Truths

| # | Plan | Truth | Status | Evidence |
|---|------|-------|--------|----------|
| 1 | 01-01 | phase1-baseline tag + recorded Release 0/0, Windows suite totals, 1885-line figure, green test.yml run | ✓ VERIFIED (override) | `git rev-parse phase1-baseline` = `9d96b181` (recreated on branch — accepted deviation #1, re-confirmed this pass) |
| 2 | 01-01 | Every phase commit on refactor/v1.1-phase-1; main never pushed directly | ✓ VERIFIED | Commit history unchanged; phase commits (`9d96b18`..`771bbeb`) sit between `phase1-baseline` and `v1.0.1`, none on `main` directly |
| 3 | 01-01 | Guard pins characterize unchanged behavior and are frozen for Plans 02-05 | ✓ VERIFIED | `git diff phase1-baseline v1.0.1 -- tests/.../MainViewModelGuardPinTests.cs` = only its creation (284 insertions, no later edits) |
| 4-8 | 01-01 | Bulk backup/restore claim order, mid-run guard, zero-row paths (pinned) | ✓ VERIFIED | `verify.artifacts`/`verify.key-links` for 01-01 re-run this pass: 1/1, 2/2; `MainViewModelGuardPinTests.cs` present on disk |
| 9 | 01-02 | `BulkRunRules.NeedsBackup` is the single definition of the unsecured verdict pair; no inline pattern left in MainViewModel | ✓ VERIFIED | `grep -c "BulkRunRules\."` over MainViewModel.cs > 0; `verify.key-links` for 01-02 re-run: 2/2 verified |
| 10 | 01-02 | Target selection/outcome wording in `BulkRunRules` (internal static, no WPF/IO/state) | ✓ VERIFIED | `BulkRunRules.cs` is 103 lines, `internal static class`; `grep -c "System.Windows"` over the file = 0 (re-run this pass) |
| 11-15 | 01-02 | REF-05 contract test over hostile inputs; reflection-enumerated surface; exemptions; fix-and-regress; no cross-call state leak | ✓ VERIFIED | `dotnet test --filter FullyQualifiedName~ServiceContractTests` re-run this pass: `Passed! Failed: 0, Passed: 3, Total: 3` |
| 16-18 | 01-03 | Per-row blocked during bulk run; row order preserved; exactly one guard owner (`IsAnyRowChecking`) | ✓ VERIFIED | Code inspection: `BulkBackupCoordinator` never writes `IsAnyRowChecking`; `MainViewModel.cs` names the coordinator as the writer, itself only reading |
| 19 | 01-03 | (backstop) `Progress<T>` constructed once on the UI thread, never inside a coordinator | ✓ VERIFIED | `grep -c "new Progress<"` over all three coordinator files = 0 |
| 20-22 | 01-03 | Claim/release order documented as the REF-03 guard; all writes through `onUi`; coordinator `internal sealed`, constructed in MainViewModel's constructor | ✓ VERIFIED | Claim order (Checking -> onStarted -> IsBusy) and release (IsBusy/counters -> onFinished) match the doc comment; `_bulk = new BulkBackupCoordinator(_backup, _gameActions, OnUi)` present |
| 23-24 | 01-03 | Rename-retry cap distinguishable failure; sub-cap succeeds; staging file deleted at cap | ✓ VERIFIED | `BackupService.cs`: `MaxRenameRetries = 100`, `"rename retry limit reached"` message present; both filters use the named constant |
| 25-27 | 01-04 | `StatusSweepCoordinator` owns sweep engine; writes through `onUi` with token check; reachability memo lives only there | ✓ VERIFIED | `verify.key-links` for 01-04 re-run: 4/4; `_sweep = new StatusSweepCoordinator(_backup, OnUi)` present; no `_lastBackupRootReachable` left in MainViewModel.cs |
| 28 | 01-04 | (backstop) Zero-row operation invokes no `Progress<T>`, leaves `IsAnyRowChecking` unstuck | ✓ VERIFIED | Coordinator test suite includes zero-row facts; unchanged since prior pass |
| 29-30 | 01-04 | Zero skipped paths silent; Scan carries `SaveStatus.SkippedPaths` via shared `onSkipped`/`FormatSkippedList` — Scan and Backup agree | ✓ VERIFIED | `SaveStatus.cs` (`SkippedPaths`), `SaveStatusScanner.cs` (`with { SkippedPaths = skipped }`), `GameRowViewModel.cs` (`str_game_unreadablePaths` + `FormatSkippedList`) — re-confirmed present |
| 31 | 01-04 | `GameRowViewModel` public surface unchanged; only `ApplyStatus` bodies changed | ✓ VERIFIED | `git diff phase1-baseline v1.0.1 -- GameRowViewModel.cs` shape matches the recorded body-only change (no source change since) |
| 32-34 | 01-04 | (backstop) Encoding-independent messages; repeated-scan reporting; Scan/Backup skip-surfacing independence | ✓ VERIFIED | Both paths format via the same `FormatSkippedList`; no caching layer or cross-game lock in either path |
| 35 | 01-05 | Moved methods removed from MainViewModel (no duplication) | ✓ VERIFIED | `grep -c -E "CheckRestorePreconditionsAsync\|PickRestoreAsync\|PathResolver\.TryResolve\|ComputeSaveDataSize"` over MainViewModel.cs = 0 outside a `<see cref>` doc mention |
| 36 | 01-05 | Line count measured and recorded; ceiling reached or owner decision applied | ✓ VERIFIED (override) | `git show v1.0.1:...MainViewModel.cs \| wc -l` = 1556 — independently re-measured this pass, matches prior figure and REQUIREMENTS.md's revised REF-02 line |
| 37 | 01-05 | `RowActionsCoordinator` owns per-row flows; commands are one-line forwards; state only via `IRowActionHost` | ✓ VERIFIED | `MainViewModel : CommunityToolkitObservableObject, IRowActionHost`; explicit interface members present; `_rowActions = new RowActionsCoordinator(...)` present |
| 38 | 01-05 | Change save folder / Remove game stay on MainViewModel (D-P05-01) | ✓ VERIFIED | Both flows remain in MainViewModel; `docs/DECISIONS.md` records the composition-root decision |
| 39 | 01-05 | Per-row `IsBusy` raised before first await, released in finally | ✓ VERIFIED | `RowActionsCoordinator.cs`: `IsBusy = true` precedes every await, released in `finally` for Backup and Restore |
| 40-41 | 01-05 | Zero/single-game tests unchanged; MainViewModelTests/GuardPinTests/StartupUnsecuredPromptTests unchanged since baseline | ✓ VERIFIED | `git diff --stat phase1-baseline v1.0.1` over the three files: only the new pin file's addition |
| 42-43 | 01-05 | (backstop) Zero-row no-op per coordinator; fixed construction order | ✓ VERIFIED | `MainViewModel.cs`: `_gameActions`, `_bulk`, `_sweep`, `_rowActions` constructed in that fixed order, one block |
| 44 | 01-05 | Guard semantics documented where they now live; exactly 2 shipping PackageReferences; no DI type; no WPF type in any coordinator | ✓ VERIFIED | `grep -c "<PackageReference"` = 2 (re-run this pass); DI-type grep over `src` = 0; WPF-type grep over all three coordinator files = 0 |

**Score:** 44/44 truths verified (2 via recorded owner deviations, independently re-confirmed this pass).

### Prohibitions (must-NOT siblings, all five plans)

| Prohibition | Verification | Evidence |
|---|---|---|
| No new NuGet package (shipping) | RESOLVED | `grep -c "<PackageReference" src/SavegameBunker/SavegameBunker.csproj` = 2 (re-run this pass) |
| No DI container / non-manual construction | RESOLVED | `grep -rn -E "IServiceProvider\|ServiceCollection\|Microsoft\.Extensions\.DependencyInjection" src` = 0 matches; all four collaborators `new`'d in MainViewModel's constructor |
| GameRowViewModel public surface / IDialogService untouched | RESOLVED | Baseline diff shows only `ApplyStatus` body changes; no WPF `using` added |

### Deferred Items

None.

### Advisory (New Scope, Unevidenced)

| # | Finding | Category | Why Advisory |
|---|---------|----------|---------------|
| 1 | `01-REVIEW.md` WR-01: bulk-restore loop's `finally` does not clear `RestoreStep`/`RestoreStepText` on an unexpected throw from `GameActions.RestoreAsync` (only `IsBusy` is released there) | other | Reproduced against the pre-phase baseline (`9d96b181`) — the identical gap existed in the original inline `RestoreSelectedAsync`, so this is a pre-existing defect the phase's REF-01 parity contract required to preserve, not a regression from the split. Outside REF-04's named defect list; does not fail any must-have. `01-REVIEW-DISPOSITION.md` still lists it (and WR-02/WR-03, two "necessity" findings over provably-unreachable `default:` branches in `GameRowViewModel`) as **open**; no source has changed since the review, so it stands unaltered. |

### Required Artifacts

`verify.artifacts` re-run this pass, independently: 16/16 artifacts passed across the five plans (`all_passed: true` for every plan — no MISSING, no STUB):

| Artifact | Expected | Status |
| -------- | -------- | ------ |
| `tests/.../MainViewModelGuardPinTests.cs` | ≥150 lines, characterization pins | ✓ VERIFIED |
| `src/.../BulkRunRules.cs` | ≥60 lines, pure rules | ✓ VERIFIED (103 lines) |
| `tests/.../BulkRunRulesTests.cs` | direct unit tests | ✓ VERIFIED |
| `tests/.../ServiceContractTests.cs` | ≥120 lines, reflection contract | ✓ VERIFIED |
| `src/.../BulkBackupCoordinator.cs` | ≥120 lines, bulk orchestration | ✓ VERIFIED (275 lines) |
| `tests/.../BulkBackupCoordinatorTests.cs` | coordinator tests | ✓ VERIFIED |
| `src/.../Services/BackupService.cs` | `MaxRenameRetries` | ✓ VERIFIED |
| `CHANGELOG.md` | Unreleased/Fixed entry (shipped into v1.0.1) | ✓ VERIFIED |
| `src/.../StatusSweepCoordinator.cs` | ≥90 lines, sweep engine | ✓ VERIFIED (156 lines) |
| `tests/.../StatusSweepCoordinatorTests.cs` | coordinator tests | ✓ VERIFIED |
| `src/.../Models/SaveStatus.cs` | `SkippedPaths` | ✓ VERIFIED |
| `src/.../Services/DirectoryWalk.cs` | `FormatSkippedList` | ✓ VERIFIED |
| `src/.../Themes/Strings.en.xaml` | `str_game_unreadablePaths` | ✓ VERIFIED |
| `src/.../RowActionsCoordinator.cs` | ≥110 lines, `IRowActionHost` seam | ✓ VERIFIED (206 lines) |
| `tests/.../RowActionsCoordinatorTests.cs` | fake-host coordinator tests | ✓ VERIFIED |
| `docs/DECISIONS.md` | composition-root decision row | ✓ VERIFIED |

### Key Link Verification

`verify.key-links` re-run this pass, independently: 14/14 links verified across the five plans (`all_verified: true` for every plan, exit 0). MainViewModel -> BulkRunRules / Services reflection (2); MainViewModel -> BulkBackupCoordinator -> BackupService/GameActions (3); MainViewModel -> StatusSweepCoordinator / scanner -> DirectoryWalk / row -> FormatSkippedList / backup -> FormatSkippedList (4); MainViewModel -> RowActionsCoordinator forwards / coordinator -> GameActions / explicit `IRowActionHost` (3); guard pins -> MainViewModel + MainViewModelTests double (2). Every detail read "Pattern found in source". No NOT_WIRED, no PARTIAL.

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| MainViewModel (scan path) | `SaveStatus.SkippedPaths` | `SaveStatusScanner` via `DirectoryWalk.ForEachFile` `onSkipped` channel | yes — real filesystem walk failures | ✓ FLOWING |
| GameRowViewModel.ApplyStatus | `StatusText` (unreadable paths) | `SaveStatus.SkippedPaths` formatted with `DirectoryWalk.FormatSkippedList` | yes | ✓ FLOWING |
| BulkBackupCoordinator | progress byte counters | `BackupService.Backup` byteProgress `IProgress` | yes | ✓ FLOWING |
| MainViewModel | `IsAnyRowChecking` aggregate | live row `IsBusy`/`ComparisonKind` state written through `OnUi` delegates | yes | ✓ FLOWING |

No static returns, hardcoded literals, or mock-fed render paths found in the phase's changed surface (unchanged since prior pass; no source has moved).

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Full solution build, Release, no-incremental | `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` | `0 Warning(s)`, `0 Error(s)` | ✓ PASS |
| Full test suite at current `dev/v1.1` HEAD | `dotnet test SavegameBunker.sln -c Release -tl:off` | `Passed! Failed: 0, Passed: 744, Total: 744` | ✓ PASS |
| REF-05 contract test alone | `dotnet test ... --filter FullyQualifiedName~ServiceContractTests` | `Passed! Failed: 0, Passed: 3, Total: 3` | ✓ PASS |
| MainViewModel.cs line count at Phase 1 close (`v1.0.1`) | `git show v1.0.1:...MainViewModel.cs \| wc -l` | `1556` (matches 01-05-SUMMARY.md and the prior pass) | ✓ PASS |
| Frozen test files byte-identical since baseline | `git diff --stat phase1-baseline v1.0.1 -- MainViewModelTests.cs MainViewModelGuardPinTests.cs StartupUnsecuredPromptTests.cs` | only the new pin file's 284-line addition | ✓ PASS |
| Tag integrity | `git rev-parse phase1-baseline` | `9d96b1814cc789983884c2567e7d4f419787f25e` | ✓ PASS |
| Zero source/test/doc change since prior verification pass | `git diff --stat 39f2552 HEAD -- src tests CHANGELOG.md docs/DECISIONS.md .planning/REQUIREMENTS.md` | empty output | ✓ PASS |

This pass ran the build and full suite directly on this Windows host, independently of the prior pass's own re-run — both land on identical totals (744/744), consistent with zero source change in between.

### Probe Execution

No probe scripts declared or conventional. Not applicable.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| ----------- | ---------- | ----------- | ------ | -------- |
| REF-01 | 01-01..01-05 | Behavior parity; suite passes unchanged | ✓ SATISFIED | No pre-existing test line deleted/rewritten; 744/744 green at current HEAD; byte-identical frozen suites since baseline |
| REF-02 | 01-02..01-05 | Composition root; coordinators sealed, no WPF refs | ✓ SATISFIED (revised ceiling, accepted-measured) | 1,556/761 measured at `v1.0.1`; all three coordinator files `internal sealed`, no WPF type |
| REF-03 | 01-03..01-05 | Uniform threading discipline; guard semantics preserved and documented | ✓ SATISFIED | Truths 19, 20-22, 26, 39, 44; `OnUi` wired at every construction site |
| REF-04 | 01-03, 01-04 | Rename-retry message distinguishable; Scan surfaces skipped paths like Backup | ✓ SATISFIED | Truths 23-24, 29-30; both fixes documented in the shipped `v1.0.1` CHANGELOG entry |
| REF-05 | 01-02, 01-05 | Never-throw service contract holds without exception | ✓ SATISFIED | `ServiceContractTests` re-run this pass: 3/3 passing |

Orphaned requirements: none — REQUIREMENTS.md's Traceability maps REF-01..REF-05 to Phase 1, and every one is claimed by at least one plan's `requirements` frontmatter. All five are marked `[x]` in REQUIREMENTS.md.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
| ---- | ---- | ------- | -------- | ------ |
| (none) | — | Debt markers (TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER) across all 11 phase-modified source files | — | re-grepped this pass: zero matches |
| (none) | — | Empty implementations / placeholder strings / hardcoded empty props | — | none found |

No 🛑 Blocker anti-patterns. See the **Advisory** section above for `01-REVIEW.md`'s one still-open, pre-existing finding (WR-01) plus two code-simplification-only findings (WR-02/03) — real, but outside this phase's must-haves, and not self-evidencing debt markers.

### Human Verification Required

None. The prior verification pass (2026-11-19, `status: human_needed`) harvested three deferred
`<human-check>` blocks from the phase's plans (01-03 Task 2, 01-04 Task 3, 01-05 Task 2) and found
that `01-UAT.md`'s then-single recorded test did not demonstrably exercise two of the three
scenarios. `01-UAT.md` now records three tests (status `complete`, 3/3 passed, 0 issues), and all
three harvested scenarios are covered by a verbatim or near-verbatim matching entry with an
explicit result:

| # | Harvested scenario (plan) | 01-UAT.md entry | Result |
|---|---------------------------|------------------|--------|
| 1 | 01-03: bulk restore with declined confirmations, large-file progress bar during a bulk run | Test 2 — "Bulk restore with declined confirmations and a large-file progress bar" (verbatim test/expected text match) | pass |
| 2 | 01-04: deny-ACL subfolder producing the warning-coloured unreadable-paths sentence | Test 3 — "Unreadable (deny-ACL) save subfolder surfaces the warning sentence" (verbatim test/expected text match) | pass |
| 3 | 01-05: end-to-end per-row and bulk walkthrough | Test 1 — "Per-row backup, restore, open-folder, refresh and bulk run in the running app" (already answered in the prior pass, carried for completeness) | passed (blanket "uat ok", 2026-11-18) |

Items 1 and 2 — the two gaps that drove the prior pass's `human_needed` verdict — are now closed
with specific, matching, passing evidence rather than a blanket confirmation. Item 3 was never a
gap on its own; it remains the same already-answered item the prior pass carried for completeness.
With all three resolved, there are no outstanding human-check items and the human verification
section is empty, so the overall status is `passed`.

### Gaps Summary

No gaps against this phase's must-haves: all 44 truths, 16 artifacts and 14 key links are
independently re-verified in the codebase (zero source/test change since the prior pass), the
suite is green (744/744), and both REF-04 fixes ship in the `v1.0.1` CHANGELOG entry. The prior
verification's `human_needed` status is resolved here: `01-UAT.md` is now complete (3/3 passed, 0
issues) and its two previously-pending tests match the previously-outstanding human-check
scenarios verbatim, each with an explicit `pass` result rather than a blanket confirmation. A
code review (`01-REVIEW.md`) separately flags one real, pre-existing latent bug (WR-01, a stuck
restore progress bar on an unexpected throw) that this phase's REF-01 parity contract required to
preserve as-is, plus two code-simplification suggestions (WR-02/WR-03); all three remain open and
unresolved per `01-REVIEW-DISPOSITION.md`, but are outside this phase's must-haves, so they are
recorded as advisory rather than blocking.

---

_Verified: 2026-11-19T18:50:00Z_
_Verifier: Claude (gsd-verifier)_
