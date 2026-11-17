---
phase: 01-bugfixing-refactoring
plan: 05
subsystem: viewmodels
tags: [refactoring, composition-root, row-actions]
requires: [01-03, 01-04]
provides: [RowActionsCoordinator, IRowActionHost, composition-root MainViewModel]
affects: [MainViewModel]
tech-stack:
  added: []
patterns:
  - coordinator-with-host-interface
key-files:
  created:
    - src/SavegameBunker/ViewModels/RowActionsCoordinator.cs
    - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - docs/DECISIONS.md
key-decisions:
  - D-P05-01 row-actions scope (Back up, Restore, Open save folder; catalog gestures stay on MainViewModel)
  - D-P05-02 IRowActionHost as the seam's own contract, implemented explicitly by MainViewModel
requirements-completed: []
duration: ~35 min
completed: 2026-11-17
---

# Phase 01 Plan 05: RowActionsCoordinator + composition root Summary

Moved the per-row Back up / Restore / Open save folder flows into `RowActionsCoordinator`
(through the internal `IRowActionHost` seam, implemented explicitly by MainViewModel), gave
MainViewModel its composition-root form — named collaborators, fixed documented construction
order, `IsAnyRowChecking`'s reset documented where it lives — and added the DECISIONS.md row.
REF-02's ~700-line ceiling is presented to the owner as the plan's blocking checkpoint.

## Accomplishments

- `RowActionsCoordinator` (internal sealed, no WPF type, no observable state) owns the three
  per-row flows moved verbatim, with the old command docs carried onto them; the per-row
  re-entrancy guard (IsBusy before the first await, released in a finally) lives inside it now.
- MainViewModel implements `IRowActionHost` explicitly (nine members, no public surface change),
  forwards the three commands in one line each (still building each row's `Progress<T>` on the
  UI thread), and documents the fixed four-collaborator construction order.
- Composition-root pass: class doc rewritten, `IsAnyRowChecking`'s remarks name
  `StatusSweepCoordinator.Reset` and `BulkBackupCoordinator`'s claim as the reset sites,
  DECISIONS.md "Build and dependencies" row added, unused `using System.IO` re-checked and kept
  (`Path` still in use), dead code scan found nothing else this phase made unused.
- 8 new coordinator facts over a recording fake host, reusing the suite's existing idioms
  (temp-dir fixture, `RecordingDialogService`, synchronous recorders).

## Tasks

| Task | Commit | Files |
|------|--------|-------|
| 1 — RowActionsCoordinator end to end | f19b85f | RowActionsCoordinator.cs, MainViewModel.cs, RowActionsCoordinatorTests.cs |
| 2 — Composition-root pass | f4a8a9f | MainViewModel.cs, docs/DECISIONS.md |
| 3 — REF-02 ceiling decision | ⏸ awaiting owner | (none yet) |

## Measured figures (for Task 3's checkpoint)

| File | wc -l total | code-only (non-blank, non-comment) |
|------|------------|-------------------------------------|
| MainViewModel.cs | 1556 | 761 |
| RowActionsCoordinator.cs | 203 | — |
| StatusSweepCoordinator.cs | 158 | — |
| BulkBackupCoordinator.cs | 272 | — |

Plan-time estimate was ~1,450; measured 1,556. The ~700 ceiling is not reachable under the
phase's own constraints ("no abstraction beyond the named seams", XML docs explaining why).

## Self-Check: PASSED

Commands actually run on this host:
- `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → `0 Warning(s)`,
  `0 Error(s)` (after Task 1, after Task 2, and after the `using System.IO` revert).
- All Task 1/Task 2 acceptance greps pass: `RowActionsCoordinator`/`IRowActionHost` present;
  no `CheckRestorePreconditionsAsync`/`PickRestoreAsync`/`PathResolver.TryResolve`/
  `ComputeSaveDataSize` call left in MainViewModel; zero WPF/CommunityToolkit/Dispatcher/
  `new Progress<`/`ConfigureAwait` references in all three coordinators; `IRowActionHost.`
  explicit implementations ≥1; `CanBackupRow` predicate byte-identical; MainViewModel at 1,556
  ≤ 1,560 ceiling; frozen test suites and `RestoreService`/`IDialogService` byte-identical vs
  `phase1-baseline`; exactly 2 `<PackageReference>`; zero DI-container types in src; no
  pre-existing test line deleted.

Known diff caveat (documented in 01-02-SUMMARY.md as well): `phase1-baseline` points at
9d96b18, before Plan 01, so `MainViewModelGuardPinTests.cs` shows as added vs the tag; its
history on this branch contains only Plan 01's two commits, as required.

## Deviations from Plan

- [Environment] Task 1/2's `dotnet test` legs and Task 2's push + test.yml dispatch were not
  run: this Linux host cannot execute the net10.0-windows/WPF suite, and the owner has not
  approved a push. Recorded under Deferred Verification; the plan's verification-of-record
  (owner's Windows run) is unchanged.
- [Environment] Task 1 commit message scope is `[REF-02, REF-03]` (the plan's own requirement
  list) rather than the untagged `refactor(vm): ...` wording in the plan text, to match the
  phase's commit-convention axis.

## Deferred Verification

All results below are **pending — owner Windows machine** (the xUnit host cannot run here):

| # | Fact | result |
|---|------|--------|
| 1 | RowActionsCoordinatorTests: 8 facts green | [pending — owner Windows machine] |
| 2 | Frozen MainViewModelTests / MainViewModelGuardPinTests / StartupUnsecuredPromptTests unchanged and green | [pending — owner Windows machine] |
| 3 | Full suite ≥ 01-04 total + this plan's tests | [pending — owner Windows machine] |
| 4 | Human check: row Back up → Restore → Open save folder → Refresh → Back up selected (50 MB save folder) | [pending — owner Windows machine] |
| 5 | Push + `gh workflow run test.yml --ref refactor/v1.1-phase-1` green (owner approval outstanding) | [pending — owner decision] |

## Checkpoint: Task 3 — REF-02 ceiling (blocking-human) — ANSWERED

Owner's answer, verbatim: **"1 accept-measured"** (2026-11-17).
Given the measured figures above (1,556 total / 761 code-only). Applied: only the REF-02 line of
`.planning/REQUIREMENTS.md` was edited — it now names the measured figure and the reason. No code
change; no gap plan needed; the phase proceeds to verification.

## Issues Encountered

None beyond the environment deviations above.
