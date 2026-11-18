---
phase: "02"
slug: "audit-follow-ups"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-18"
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `02-RESEARCH.md` § Validation Architecture; decisions D-01..D-13 in `02-CONTEXT.md`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (runner 4.0.1, Test SDK 18.12.0), `net10.0-windows` |
| **Config file** | none (no `xunit.runner.json`; one collection per test class) |
| **Quick run command** | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~<Class>"` |
| **Full suite command** | `dotnet test SavegameBunker.sln -c Release -tl:off` |
| **Build gate** | `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` (must stay 0 warnings / 0 errors) |
| **Baseline (D-08)** | 695 tests at `57811c1` |
| **Estimated runtime** | ~120 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** filtered command for the touched class(es) plus the build gate
- **After every plan wave:** full suite command; record the count (rises only by new facts)
- **Flake check (items 3, 22–24):** full suite five times in a row, all green
- **Before `/gsd-verify-work`:** full suite green; CI green on the PR to `main`
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

Filled by the planner per task; item-level map:

| Item | Behavior | Test Type | Automated Command (filter) | File Exists | Status |
|------|----------|-----------|----------------------------|-------------|--------|
| 0 / D-01 / D-10 | Save subfolders archived when save folder is inside backup root or inside `<B>\<id>`; exclusions reported as info line, not skipped paths | unit | `FullyQualifiedName~BackupServiceTests` | extend | ⬜ pending |
| D-02 / D-11 | Restore never deletes excluded content or archives; refuses when `<B>\<id>` is inside or equal to save folder | unit | `FullyQualifiedName~RestoreServiceTests` | extend | ⬜ pending |
| 1 | Arming after first full sweep settled evaluates immediately; once per session; zero-row sweep settles | unit | `FullyQualifiedName~StartupUnsecuredPromptTests` | extend | ⬜ pending |
| 2 | Row actions, Refresh, ChangeBackupRoot, close blocked while bulk picker open; released after cancel | unit | `FullyQualifiedName~MainViewModelTests` | extend (`OnPickFolder` hook) | ⬜ pending |
| 3 / D-13 | Drive-independent picker tests; reachable `BackupSelectedCommand` bulk-cancel fact | unit | `FullyQualifiedName~DriveHintsTests\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~StartupUnsecuredPromptTests` | extend | ⬜ pending |
| 4 | `PathCompare` at drive roots and prefix lookalikes; `ToTemplate("E:\\")` | unit | `FullyQualifiedName~PathCompareTests\|FullyQualifiedName~PathResolverTests` | ❌ W0 new file | ⬜ pending |
| 5 | Only-excluded-content save folder fails; locked-only still succeeds | unit | `FullyQualifiedName~BackupServiceTests` | extend | ⬜ pending |
| 6, 9 | Behavior-preserving | existing suite | full suite | existing | ⬜ pending |
| 7 | Verification modes | unit | `FullyQualifiedName~BackupServiceTests` | migrate | ⬜ pending |
| 11 | Delegate from view model; Confirm branch removed/asserted | build + manual | build gate | n/a | ⬜ pending |
| 14 | Command requery on `IsBusy` / op flags | unit | `FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~MainViewModelGuardPinTests` | extend | ⬜ pending |
| 15 | Coordinator progress via `Action<int,string>` in order | unit | `FullyQualifiedName~BulkBackupCoordinatorTests\|FullyQualifiedName~MainViewModelGuardPinTests` | migrate | ⬜ pending |
| 16 | Shared save-folder `Describe` | unit | `FullyQualifiedName~AddGameDialogViewModelTests\|FullyQualifiedName~ChangeSaveFolderDialogViewModelTests` | extend | ⬜ pending |
| 19, 20, 21 | Visual identity except D-12 footer width and item-21 close-hover | manual screenshots + unit | `FullyQualifiedName~ResourceKeyTests` + build gate | update `ResourceKeyTests.cs:75` | ⬜ pending |
| 22, 23, 24 | Pure test refactor; count unchanged | existing suite | full suite ×5 | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Plan / task map (planner, 2026-11-18)

| Plan · Task | Item / decision | Automated verify (filter or command) | Manual |
|-------------|-----------------|--------------------------------------|--------|
| 02-01 · 1 | 0, D-10, D-08 baseline | `BackupServiceTests\|RestoreServiceTests\|SaveStatusScannerTests` | — |
| 02-01 · 2 | D-01 | `BackupServiceTests\|RestoreServiceTests\|SaveStatusScannerTests\|ServiceContractTests` | — |
| 02-01 · 3 | D-02, D-11, D-03 | `RestoreServiceTests\|BackupServiceTests\|ServiceContractTests` + full suite | — |
| 02-02 · 1–2 | 16 | `ChangeSaveFolderDialogViewModelTests\|AddGameDialogViewModelTests` + full suite | — |
| 02-03 · 1–2 | D-07 baseline | build gate + demo-data.ps1 run and root-guard refusal + unchanged-XAML guard | before screenshots, shot table |
| 02-04 · 1–3 | 19, 20, D-07, D-12 | build gate + `ResourceKeyTests` + full suite | after-19-20 screenshots vs before, access-key table |
| 02-05 · 1 | 3, D-05 | `DriveHintsTests\|MainViewModelTests\|MainViewModelGuardPinTests\|StartupUnsecuredPromptTests` | — |
| 02-05 · 2 | D-04, D-13 | full suite ×5 | — |
| 02-06 · 1 | 1 | `StartupUnsecuredPromptTests\|MainViewModelGuardPinTests\|MainViewModelTests` | — |
| 02-06 · 2 | 2 (`OnPickFolder` hook) | `MainViewModelTests\|MainViewModelGuardPinTests\|BulkBackupCoordinatorTests\|StartupUnsecuredPromptTests` + full suite | — |
| 02-07 · 1 | 4 (`PathCompareTests.cs` new) | `PathCompareTests\|BackupServiceTests\|RestoreServiceTests\|SaveStatusScannerTests\|ServiceContractTests` | — |
| 02-07 · 2 | 4 (ToTemplate) | `PathResolverTests\|AddGameDialogViewModelTests\|ChangeSaveFolderDialogViewModelTests` | — |
| 02-07 · 3 | 5 | `BackupServiceTests\|RestoreServiceTests\|ServiceContractTests` + full suite | — |
| 02-08 · 1 | 14 | `MainViewModelTests\|MainViewModelGuardPinTests\|StartupUnsecuredPromptTests` | — |
| 02-08 · 2 | 15 | `BulkBackupCoordinatorTests\|MainViewModelTests\|MainViewModelGuardPinTests` + full suite | bulk progress in the running app |
| 02-09 · 1–3 | 7, 6, 9 | `BackupServiceTests\|RestoreServiceTests\|ServiceContractTests` + full suite | — |
| 02-10 · 1 | 11 | `MainViewModelTests\|RestorePickerViewModelTests\|RowActionsCoordinatorTests\|AddGameDialogViewModelTests\|StartupUnsecuredPromptTests` | — |
| 02-10 · 2 | 11 (Confirm) | build gate + full suite | picker delete confirmation (or UNVERIFIED with reason) |
| 02-11 · 1–2 | 21, D-06, item-18 comment | `ResourceKeyTests` + full suite | after-21 screenshots |
| 02-12 · 1–2 | 22 (part 1) | full suite, Total and test names unchanged | — |
| 02-13 · 1–3 | 22 (part 2) | full suite, Total and test names unchanged | — |
| 02-14 · 1–2 | 24 | full suite ×5 | — |
| 02-15 · 1–3 | 23 | full suite ×5, Total and test names unchanged | — |

All filters run as `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~A|FullyQualifiedName~B"`.

---

## Wave 0 Requirements

- [ ] `tests/SavegameBunker.Tests/PathCompareTests.cs` — item 4
- [ ] `RecordingDialogService.OnPickFolder` hook in `MainViewModelTests.cs` — item 2
- [ ] Failing-first facts for items 0, 5, D-02 and D-11 cases

*Framework install: none; xUnit infrastructure exists.*

---

## Manual-Only Verifications

| Behavior | Item | Why Manual | Test Instructions |
|----------|------|------------|-------------------|
| Before/after screenshot pairs of all five windows, light and dark | 19, 20, 21 (D-07, D-12) | Rendered WPF output | Capture each window before and after; only the D-12 footer width and the title-bar close-button hover may differ |
| Access keys on the eight keyed toolbar buttons | 19 | Keyboard UI | Press Alt, check each underlined key works; ThemeToggle and BackupAllChanged show plain text |
| Restore picker delete confirmation answerable while picker is modal | 11 | Dialog host interaction | Open restore picker, delete an archive, confirm the dialog can be answered |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
