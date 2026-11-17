---
phase: "1"
slug: "bugfixing-refactoring"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| 01-01-xx | 01 | 1 | REF-01 | — | Full suite passes unchanged after the baseline pin | regression | `dotnet test SavegameBunker.sln` | ✅ | ⬜ pending |
| 01-0x-xx | TBD | TBD | REF-02 | — | `MainViewModel.cs` ≤~700 lines; 3 sealed coordinators extracted | structural + unit | `wc -l src/SavegameBunker/ViewModels/MainViewModel.cs` + new coordinator tests | ❌ W0 | ⬜ pending |
| 01-0x-xx | TBD | TBD | REF-03 | — | OnUi/IProgress pattern and `IsAnyRowChecking` guard preserved across the split | unit + integration | `dotnet test --filter "FullyQualifiedName~MainViewModelTests"` + new coordinator-level guard tests | ✅ existing / ❌ W0 new | ⬜ pending |
| 01-0x-xx | TBD | TBD | REF-04 (rename cap) | — | 100-collision message distinguishable from a disk-error message | unit | `dotnet test --filter "FullyQualifiedName~BackupServiceTests"` | ❌ W0 | ⬜ pending |
| 01-0x-xx | TBD | TBD | REF-04 (scan skip) | — | Scan surfaces skipped unreadable paths | unit | `dotnet test --filter "FullyQualifiedName~SaveStatusScannerTests"` | ❌ W0 | ⬜ pending |
| 01-0x-xx | TBD | TBD | REF-05 | — | Every public Services entry point returns failure/warning, never throws | contract | `dotnet test --filter "FullyQualifiedName~ServiceContract"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Exact task IDs are assigned by the planner; this map is seeded from 01-RESEARCH.md's Phase Requirements → Test Map and refined once PLAN.md files exist.

---

## Wave 0 Requirements

- [ ] Extend `BackupServiceTests.cs` — a test proving the ≥100-collision rename-retry path produces a distinguishable message (REF-04a)
- [ ] Extend `SaveStatusScannerTests.cs` (and/or `MainViewModelTests.cs`, depending on which surface the plan picks) — a test proving Scan surfaces a skipped unreadable path somewhere user-visible (REF-04b)
- [ ] New contract-test file enumerating the Services surface via reflection, asserting no public method throws across its boundary on hostile input (REF-05)
- [ ] New unit test file(s) for each extracted coordinator (bulk-backup, status-sweep, row-actions), following the existing `<Subject>Tests.cs` convention

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full regression pass | REF-01..05 | xUnit test host lacks a `SynchronizationContext`; the Owner's Windows run is the verification of record per ROADMAP.md | Run `dotnet test SavegameBunker.sln` on Windows; confirm 629/629 green |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
