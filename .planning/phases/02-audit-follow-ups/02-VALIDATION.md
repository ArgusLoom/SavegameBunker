---
phase: "02"
slug: "audit-follow-ups"
status: validated
nyquist_compliant: false
wave_0_complete: true
created: "2026-11-18"
validated: "2026-11-19"
---

# Phase 02 — Validation Report

> Retroactive Nyquist audit. `phase_req_ids`: none registered — coverage traced against each
> plan's `requirements:` frontmatter tags (ITEM-N, D-NN) and each task's `<verify>` /
> `coverage[].verification` blocks already recorded in the 15 SUMMARY.md files.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (runner 4.0.1, Test SDK 18.12.0), `net10.0-windows` |
| **Quick run command** | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~<Class>"` |
| **Full suite command** | `dotnet test SavegameBunker.sln -c Release -tl:off` |
| **Independently re-run this session** | `dotnet test SavegameBunker.sln -c Release -tl:off` → **739/739 passed, 0 failed, 0 skipped** (matches every SUMMARY's claimed count; re-verified fresh, not taken on trust) |
| **Build gate** | `dotnet build SavegameBunker.sln -c Release` — 0 warnings / 0 errors (per 02-10/02-11/02-15 SUMMARYs) |
| **Baseline (D-08)** | 695 tests at `57811c1`; grew to 739 across the phase |

---

## Per-Task Map

Every item below already carried an automated `<verify>` filter that was run during execution
(per-plan SUMMARY `coverage[]` blocks); this audit re-confirms the suite is still green as a
whole rather than re-deriving each fact. Items with a genuine residual gap are marked accordingly.

| Plan | Item(s) | Requirement tag | Automated command | Status |
|------|---------|------------------|--------------------|--------|
| 02-01 | 0, D-01, D-02, D-03, D-10, D-11 | (none registered) | `BackupServiceTests\|RestoreServiceTests\|SaveStatusScannerTests` | COVERED |
| 02-02 | 16 | ITEM-16 | `ChangeSaveFolderDialogViewModelTests\|AddGameDialogViewModelTests` | COVERED |
| 02-03 | D-07 baseline tooling | D-07 | build gate + script structural check | COVERED (tooling only; see 02-04/02-11 below for the actual capture) |
| 02-04 | 19, 20, D-07, D-12 | ITEM-19, ITEM-20, D-07, D-12 | build gate + `ResourceKeyTests` + full suite | COVERED for code; screenshots deferred to 02-11 (see Manual-Only) |
| 02-05 | 3, D-04, D-05, D-13 | ITEM-3, D-04, D-05, D-13 | `DriveHintsTests\|MainViewModelTests\|MainViewModelGuardPinTests\|StartupUnsecuredPromptTests` + full suite ×5 | COVERED |
| 02-06 | 1, 2 | ITEM-1, ITEM-2, D-13 | `StartupUnsecuredPromptTests\|MainViewModelTests\|MainViewModelGuardPinTests\|BulkBackupCoordinatorTests` + full suite | COVERED |
| 02-07 | 4, 5 | (none registered) | `PathCompareTests\|PathResolverTests\|BackupServiceTests\|RestoreServiceTests\|ServiceContractTests` | COVERED |
| 02-08 | 14, 15 | ITEM-14, ITEM-15 | `MainViewModelTests\|MainViewModelGuardPinTests\|BulkBackupCoordinatorTests` + full suite | COVERED |
| 02-09 | 6, 7, 9 | (none registered) | `BackupServiceTests\|RestoreServiceTests\|ServiceContractTests` + full suite | COVERED |
| 02-10 | 11 | ITEM-11 | `MainViewModelTests\|RestorePickerViewModelTests\|RowActionsCoordinatorTests\|AddGameDialogViewModelTests\|StartupUnsecuredPromptTests` | COVERED for the delegate wiring and `Confirm` assertion. **MISSING/ESCALATED for the manual delete-confirmation sub-check — see Blocker below.** |
| 02-11 | 21, D-06 | (none registered) | `ResourceKeyTests` + full suite + `after-21` screenshots | COVERED (9/14 shots captured and pixel-verified; 5 shots not captured this session — see Manual-Only) |
| 02-12, 02-13 | 22 | (none registered) | full suite, Total/test-name list unchanged | COVERED |
| 02-14 | 24 | ITEM-24 | full suite ×5 | COVERED |
| 02-15 | 23 | ITEM-23, D-08 | full suite ×5, Total/test-name list unchanged | COVERED |

**Independent verification performed in this audit:** re-ran `dotnet test SavegameBunker.sln -c Release -tl:off` fresh — 739/739 passed. No stale "it passed when we wrote it" claims were taken at face value for the suite-wide count.

---

## Gap Analysis Outcome

No automated-test gaps required filling. Every ROADMAP item 0–24 carries at least one automated
`<verify>` filter that was executed during the plan that implemented it, the full suite was
re-run green independently in this audit, and no plan's `coverage[]` block shows an untested
behavioral claim for a testable (non-UI, non-visual) surface. This phase's own self-checks
(plan-level `<acceptance_criteria>` greps, five consecutive full-suite runs for the flake-prone
items 3/22-24) are more thorough than a typical retroactive audit would independently reconstruct,
so no new test files were created.

One item resolves to **ESCALATED**, not COVERED or SKIP — see below.

---

## ESCALATED — Blocker not yet surfaced to STATE.md

**Item:** 11 (`DialogService` / restore picker delete confirmation)
**Finding (confirmed by 02-10-SUMMARY.md, Task 2, with screenshots and a real simulated mouse
click — not a UI-Automation-only false positive):** the restore picker's per-row delete
confirmation dialog renders into `MainWindow.RootDialogHost`, but `RestorePickerWindow` is a
modal child window centered over `MainWindow` and fully covers the confirmation on screen. A real
user cannot see or click the confirmation while the picker is open — `WindowFromPoint` at the
confirmation's own button coordinates resolves to the picker's HWND, not `MainWindow`'s, and a
real `SetCursorPos`/`mouse_event` click there does not delete the archive (count unchanged, 2→2),
while the same click against the picker's own buttons works. Only `InvokePattern.Invoke()` (which
bypasses the OS input path) can answer it — confirming the defect is real, not a measurement
artifact.

**Why this is an implementation bug, not a test gap:** the behavior is confirmed and reproducible;
no test can "pass" this without the application being fixed (e.g., giving `RestorePickerWindow`
its own dialog host). Per this audit's instructions, implementation bugs are escalated, not fixed
or test-papered-over.

**Gap in the phase's own process, caught by this audit:** 02-10-SUMMARY.md's own "Next Phase
Readiness" section explicitly asks the owner to add this to `.planning/STATE.md` Blockers at
phase close ("Restore picker delete confirmation is unreachable by a real user while the picker is
open... confirmed 2026-11-18, see 02-10-SUMMARY.md"). `.planning/STATE.md` Blockers currently reads
**"None open"** — the follow-up was never carried over. This audit surfaces it again here so it is
not lost a second time.

**Recommendation:** add the Blocker line to `.planning/STATE.md` before closing this phase. The fix
itself (a per-picker-window dialog host, or routing `Confirm` through a host visible above the
current modal window) is architectural and out of this audit's and this phase's scope.

---

## Manual-Only Verifications (disclosed caveats, not silent gaps)

| Behavior | Item | Status | Note |
|----------|------|--------|------|
| Before/after screenshots, 9 of 14 shots (01,02,03,04,06,09,10,12,14) | 19, 20, 21 | WARNING — captured and pixel/visual-verified (`after-19-20` vs `after-21`, both present on disk, confirmed by this audit via `ls`) | — |
| Screenshots 05 (row-actions idle/busy), 07 (status bar), 08 (restore steps), 11 (ChangeSaveFolder footer), 13 (RestorePicker footer) | 19, 20 | WARNING — not captured this session (02-11-SUMMARY.md discloses this; one bad capture, 10-addgame-footer, was discarded/retaken and the operator stopped further ad hoc desktop automation to avoid exposing other windows) | Visual-only claim; XAML changes for these items are still covered by the build gate and `ResourceKeyTests`. Recommend capturing before declaring D-07 fully closed. |
| Access keys (Alt-mnemonics) on the eight keyed toolbar buttons | 19 | WARNING — asserted achievable via WPF-UI's `RecognizesAccessKey` template behavior (cited library source) but not re-confirmed interactively in this audit | Low risk; template structure preserved per 02-04-SUMMARY. |

---

## Validation Sign-Off

- [x] All ROADMAP items 0–24 have at least one automated `<verify>` command that ran and passed during execution, independently re-confirmed green in this audit (739/739).
- [x] No gap required writing a new test file — existing per-plan verification was adversarially reviewed, not rubber-stamped.
- [x] One ESCALATED finding: restore-picker delete-confirmation modal-occlusion defect (ITEM-11), confirmed real, not fixed (out of scope — implementation files are read-only for this audit), and its STATE.md Blocker entry (owed since 02-10) is still missing.
- [x] Manual-only screenshot gaps (5 of 14 shots) disclosed as WARNING, not hidden.
- [ ] `nyquist_compliant: true` — **not set**, because of the open ESCALATED blocker above (`nyquist_compliant: false` until the STATE.md Blocker is recorded and the owner decides a fix/defer).

**Approval:** PARTIAL — compliant on automated test coverage; one implementation defect and its missing Blocker entry remain open for the owner.
