---
phase: "1"
slug: "bugfixing-refactoring"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-17"
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none — no `xunit.runner.json` committed |
| **Quick run command** | `dotnet test tests/SavegameBunker.Tests --filter "FullyQualifiedName~<Subject>Tests"` |
| **Full suite command** | `dotnet test SavegameBunker.sln` |
| **Estimated runtime** | ~1–2 minutes (full suite) |

---

## Sampling Rate

- **After every task commit:** Run the filtered single-subject command for whatever file(s) that commit touched
- **After every plan wave:** Run `dotnet test SavegameBunker.sln` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green on the Owner's Windows run (the stated verification of record — a Linux `dotnet build` only covers compile, not test execution)
- **Max feedback latency:** ~2 minutes

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-01-T1/T2 | 01 | 1 | REF-01 | — | Full suite passes unchanged after the baseline pin | regression | `dotnet test SavegameBunker.sln` | ✅ | ✅ green (689/689, Windows + CI 44198312249; re-run 2026-11-25: 772/772) |
| 01-02-T1 | 02 | 2 | REF-02 | — | `BulkRunRules` is the single unsecured-verdict definition; 3 sealed coordinators extracted | structural + unit | `wc -l src/SavegameBunker/ViewModels/MainViewModel.cs` (1,556/761 code-only, owner-accepted ceiling) + `dotnet test --filter "FullyQualifiedName~BulkRunRulesTests"` | ✅ | ✅ green |
| 01-02-T2 | 02 | 2 | REF-05 | — | Every public Services entry point returns failure/warning, never throws | contract | `dotnet test --filter "FullyQualifiedName~ServiceContract"` | ✅ | ✅ green (first real run found the DialogService exemption drift — fixed in 27e47d1) |
| 01-03-T1/T2 | 03 | 3 | REF-03 | — | Claim/release order and `IsAnyRowChecking` guard preserved across the bulk split | unit | `dotnet test --filter "FullyQualifiedName~BulkBackupCoordinatorTests\|FullyQualifiedName~MainViewModelGuardPinTests"` | ✅ | ✅ green (9 coordinator facts + pins) |
| 01-03-T3 | 03 | 3 | REF-04 (rename cap) | — | 100-collision message distinguishable from a disk-error message | unit | `dotnet test --filter "FullyQualifiedName~BackupServiceTests"` | ✅ | ✅ green (2 boundary facts) |
| 01-04-T2/T3 | 04 | 4 | REF-04 (scan skip) | — | Scan surfaces skipped unreadable paths on the row | unit | `dotnet test --filter "FullyQualifiedName~SaveStatusScannerTests"` | ✅ | ✅ green (incl. `A_fully_readable_directory_reports_no_skipped_paths`) |
| 01-04-T1 | 04 | 4 | REF-03 | — | Sweep engine owns the per-row token bump and reset; MainViewModel keeps only the announce | unit | `dotnet test --filter "FullyQualifiedName~StatusSweepCoordinatorTests"` | ✅ | ✅ green |
| 01-05-T1 | 05 | 5 | REF-02, REF-03 | T-01-11..13 | Per-row flows through `IRowActionHost`; IsBusy guard inside the coordinator | unit | `dotnet test --filter "FullyQualifiedName~RowActionsCoordinatorTests"` | ✅ | ✅ green (8 facts, incl. claim-order and cancel-release) |
| 01-05-T2 | 05 | 5 | REF-02 | — | Composition root: fixed construction order, 2 PackageReferences, no DI | structural | `grep -c "<PackageReference" src/SavegameBunker/SavegameBunker.csproj` = 2; `grep -rn -E "IServiceProvider\|ServiceCollection" src --include=*.cs \| wc -l` = 0 | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Exact task IDs are assigned by the planner; this map is seeded from 01-RESEARCH.md's Phase Requirements → Test Map and refined once PLAN.md files exist.

---

## Wave 0 Requirements

- [x] Extend `BackupServiceTests.cs` — a test proving the ≥100-collision rename-retry path produces a distinguishable message (REF-04a) — done in Plan 01-03 Task 3 (2 boundary facts)
- [x] Extend `SaveStatusScannerTests.cs` (and/or `MainViewModelTests.cs`, depending on which surface the plan picks) — a test proving Scan surfaces a skipped unreadable path somewhere user-visible (REF-04b) — done in Plan 01-04 (row-level warning via `str_game_unreadablePaths`)
- [x] New contract-test file enumerating the Services surface via reflection, asserting no public method throws across its boundary on hostile input (REF-05) — `ServiceContractTests.cs` (Plan 01-02; DialogService exemption applied after the first real run)
- [x] New unit test file(s) for each extracted coordinator (bulk-backup, status-sweep, row-actions), following the existing `<Subject>Tests.cs` convention — `BulkBackupCoordinatorTests.cs` (9), `StatusSweepCoordinatorTests.cs`, `RowActionsCoordinatorTests.cs` (8)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full regression pass | REF-01..05 | xUnit test host lacks a `SynchronizationContext`; the Owner's Windows run is the verification of record per ROADMAP.md | Run `dotnet test SavegameBunker.sln` on Windows; confirmed 689/689 green (2026-11-18, CI 44198312249 success) |
| Running-app walkthrough | REF-01..03 | Dispatcher/thread-model defects only surface in the running app | App check passed by owner ("uat ok", 2026-11-18; recorded in 01-UAT.md) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** granted 2026-11-18 — validate-phase audit; all 9 map rows green, 0 gaps.

## Validation Audit 2026-11-17

| Metric | Count |
|---|---|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
