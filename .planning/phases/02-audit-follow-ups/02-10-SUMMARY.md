---
phase: 02-audit-follow-ups
plan: 10
subsystem: services
tags: [dialogs, mvvm, threading, debug-assert, ui-automation]

requires:
  - phase: 02-audit-follow-ups
    provides: "02-03's demo-data.ps1 / capture.ps1 UI-Automation approach, reused (not called directly — see Issues Encountered) to drive the manual restore-picker delete-confirmation check"
provides:
  - "IDialogService.ShowRestorePickerDialog(gameDisplayName, backups, deleteArchive) — the delete action is a parameter supplied by the caller, never constructed by DialogService"
  - "GameActions.PickRestoreAsync passes _backup.DeleteArchive, since GameActions already holds the BackupService"
  - "DialogService.Confirm asserts its off-UI-thread contract via Debug.Assert instead of carrying a dead UI-thread branch"
  - "CONFIRMED NEW FINDING: the restore picker's delete confirmation, hosted in MainWindow's RootDialogHost, is rendered entirely behind the modal RestorePickerWindow and is unreachable by a real mouse click or keyboard focus while the picker is open — see Deviations/New Finding below"
affects: ["02-audit-follow-ups end-of-phase verification", ".planning/STATE.md Blockers (owner to add at phase close)"]

actuals:
  tokens: 2500        # chars/4 over the realized diff (6 files, 70 changed lines)
  tasks: 2
  commits: 2           # measured: ae47e5f..HEAD
  plan_head_before: ae47e5f48a6002be9bcbf1c04c600c5c8a79b4c3
  plan_head_after: 987b13140f90275c441fc317d407cadb417724ac

tech-stack:
  added: []            # zero packages; shipping csproj unchanged (T-02-SC)
  patterns: ["delegate-injected service calls instead of dialog-layer construction", "Debug.Assert for a threading contract instead of a dead defensive branch"]

key-files:
  created: []
  modified:
    - src/SavegameBunker/Services/IDialogService.cs
    - src/SavegameBunker/Services/DialogService.cs
    - src/SavegameBunker/ViewModels/GameActions.cs
    - tests/SavegameBunker.Tests/MainViewModelTests.cs
    - tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs
    - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs

key-decisions:
  - "Took the ROADMAP's interface-signature form (not the smaller constructor-injection alternative 02-RESEARCH.md also offered): IDialogService.ShowRestorePickerDialog gained a Func<string, OperationResult> deleteArchive parameter, touching one production caller and three test fakes."
  - "Confirm's unreachable UI-thread branch became a Debug.Assert of the off-thread contract (Claude's discretion per 02-RESEARCH.md), not a silent deletion — the single InvokeAsync(...).Task.Unwrap() path stays, and Confirm still never routes through the Error dialog queue (would deadlock a background caller)."
  - "Bypassed .planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1 for the manual check: it has a pre-existing PowerShell parse error (chained .Replace backtick-continuation, lines 144-147) that this plan does not own fixing — see Issues Encountered. Built a minimal one-game catalog directly in the ad hoc check script instead."
  - "Verified the restore-picker delete-confirmation sub-check with BOTH UI Automation's InvokePattern and a real simulated mouse click (SetCursorPos + mouse_event) plus screenshots, because InvokePattern invokes a control's command in-process and does not prove a real user could reach it. The two methods disagree (see New Finding) — the real-click result is the one that matters for a human user."

requirements-completed: [ITEM-11]

coverage:
  - id: D1
    description: "DialogService no longer constructs a BackupService for the restore picker's delete; the delete action is a parameter GameActions supplies from the BackupService it already holds"
    requirement: "ITEM-11"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#The_restore_picker_is_given_a_delete_action_that_removes_exactly_that_archive"
        status: pass
    human_judgment: false
  - id: D2
    description: "Confirm's unreachable UI-thread branch is replaced by a Debug.Assert of the off-UI-thread contract; the single InvokeAsync(...).Task.Unwrap() path is unchanged and Confirm is not routed through the Error dialog queue"
    requirement: "ITEM-11"
    verification:
      - kind: other
        ref: "dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental (0 Warnings, 0 Errors) + dotnet test SavegameBunker.sln -c Release -tl:off (739/739 passed) + acceptance-criteria greps"
        status: pass
    human_judgment: true
    rationale: "DialogService is WPF-only and outside ServiceContractTests' reflected surface, so Confirm's threading behavior is not unit-testable; the build/suite/grep evidence proves no regression, but the live dialog-answering behavior itself is the human-check task 2's <verify> already calls for."
  - id: D3
    description: "The restore picker's delete-confirmation sub-check (whether it can be answered while the picker is modal) was tried in the running app rather than left unverified"
    verification:
      - kind: manual_procedural
        ref: "Ad hoc PowerShell UI-Automation + real-mouse-click script run against the Release build on this Windows host (see Deviations/New Finding for the two screenshots and the outcome)"
        status: fail
    human_judgment: true
    rationale: "Confirmed a NEW defect, not a pass: the confirmation is invisible and unreachable by a real click while the picker is open. This is reported to the owner per the plan's fallback for a failing sub-check, not fixed in this phase — a human must decide the fix (e.g. a per-picker-window dialog host) and prioritize it."

duration: 55min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 10: Restore picker delete delegation and Confirm's threading assertion Summary

**DialogService no longer builds its own BackupService for the restore picker's delete — GameActions supplies BackupService.DeleteArchive through a new IDialogService parameter — and Confirm's unreachable UI-thread branch is now a Debug.Assert; the long-open "is the picker's delete confirmation answerable while modal" question is answered NO, confirmed with screenshots, and filed as a new finding rather than silently fixed.**

## Performance

- **Duration:** 55 min
- **Started:** 2026-11-18T20:58Z (approx.)
- **Completed:** 2026-11-18T21:53Z (approx.)
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- `IDialogService.ShowRestorePickerDialog` takes a `Func<string, OperationResult> deleteArchive` parameter; `DialogService` passes it straight to `RestorePickerViewModel` instead of constructing `new BackupService()`. `GameActions.PickRestoreAsync` supplies `_backup.DeleteArchive`, since `GameActions` already held the `BackupService`.
- `RecordingDialogService` (MainViewModelTests.cs) records each call's delete action in a new `RestorePickerDeleteActions` list; `ThrowingDialogService` and the nested `ThrowingConfirmDialogService` moved their signatures with the interface change.
- New fact `The_restore_picker_is_given_a_delete_action_that_removes_exactly_that_archive`: seeds a real archive, triggers a restore (picker cancelled), takes the one recorded delete action, invokes it directly with the archive's path, and asserts it deletes exactly that file.
- `DialogService.Confirm`'s dead UI-thread branch (`_contentDialogService.ShowSimpleDialogAsync(...).GetAwaiter().GetResult()`, unreachable because all four callers already run under `Task.Run`) is replaced by `Debug.Assert(!_owner.Dispatcher.CheckAccess(), ...)` followed by the single `InvokeAsync(() => ShowQueuedAsync(options)).Task.Unwrap()` path. The existing comment about why blocking the UI thread would deadlock stays; the new comment names the four off-thread callers' shape.
- The previously-unverified sub-check — whether the restore picker's per-row delete confirmation can be answered while the picker window is modal — was run against the Release build on this Windows host and answered definitively: **it cannot.** See New Finding below.

## Task Commits

Each task was committed atomically:

1. **Task 1: The picker's delete action end to end** - `14707d5` (refactor)
2. **Task 2: Confirm asserts its off-UI-thread contract; manual sub-check run** - `987b131` (refactor)

**Plan metadata:** commit pending (this SUMMARY + REQUIREMENTS.md; STATE.md/ROADMAP.md are the orchestrator's to update per this dispatch's instructions)

## Files Created/Modified

- `src/SavegameBunker/Services/IDialogService.cs` - `ShowRestorePickerDialog` gained the `deleteArchive` parameter and doc
- `src/SavegameBunker/Services/DialogService.cs` - restore picker no longer builds `new BackupService()`; `Confirm` asserts instead of branching
- `src/SavegameBunker/ViewModels/GameActions.cs` - `PickRestoreAsync` passes `_backup.DeleteArchive`
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` - `RecordingDialogService.RestorePickerDeleteActions` + the new wiring fact
- `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs` - `ThrowingDialogService`'s signature moved with the interface
- `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` - `ThrowingConfirmDialogService`'s signature moved with the interface

## Decisions Made

- Took the ROADMAP's larger form (interface signature change touching four implementers) over 02-RESEARCH.md's smaller constructor-injection alternative, matching the plan's `must_haves`.
- `Confirm`'s dead branch became an assertion, not a silent deletion, per 02-RESEARCH.md's explicit note that this was left to Claude's discretion.
- Did not touch `.planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1` to fix its pre-existing parse bug (out of this plan's scope per the deviation rules' scope boundary — it's a Plan 02-03 artifact, not one this plan's tasks modify). Built a throwaway, minimal catalog directly in the ad hoc verification script instead. Flagged in Issues Encountered for the owner/02-03 plan line.

## Deviations from Plan

### Auto-fixed Issues

None — no Rule 1-3 auto-fixes were needed; the two tasks' code changes were implemented exactly per the plan's `<action>` steps with no blocking issues.

### New Finding (per Task 2's documented fallback for a failing sub-check — not fixed in this phase)

**Title: The restore picker's per-row delete confirmation is invisible and unreachable by a real mouse click while the picker window is modal.**

**Steps to reproduce** (verified on this host, Windows 11, Release build):
1. Back up a game at least twice so its restore picker has 2+ rows.
2. Open the restore picker (row Restore button).
3. Click a row's "Delete archive" button.
4. Observe: the main window (`MainWindow`) becomes `IsWindowEnabled() == False` at the Win32 level (standard WPF owned/modal-window behavior), and the delete confirmation (`DialogService.Confirm`, drawn through `IContentDialogService` into `MainWindow.RootDialogHost`) is now showing somewhere in `MainWindow`'s visual tree — but `MainWindow` is fully covered on screen by the still-active `RestorePickerWindow` sitting on top of it (`RestorePickerWindow` is centered over its owner and the confirmation also centers over the same window, so the two overlap exactly).
5. A screenshot taken at this moment shows only the picker window and its own two rows — **no confirmation dialog is visible anywhere on screen.**
6. `WindowFromPoint` at the confirmation `Delete` button's own `BoundingRectangle` center resolves to a *different* HWND of the same process (the picker window covering that screen location), not `MainWindow`'s HWND.
7. A simulated **real** mouse click (`SetCursorPos` + `mouse_event`, not UI Automation's `InvokePattern`) at that exact screen coordinate does **not** delete the archive — the archive count is unchanged before and after. The same real click sequence against the picker's own "Delete archive" button (which belongs to the active, enabled picker window) works correctly earlier in the same run, confirming the click mechanism itself is sound.
8. By contrast, UI Automation's `InvokePattern.Invoke()` called directly on the confirmation's `Cancel`/`Delete` buttons *does* answer them and produces the correct outcome (archive kept on Cancel, archive count drops by exactly one on Delete) — because `InvokePattern` raises the control's command in-process and does not go through the OS input/focus path a real user's mouse or keyboard would use. This is why the two verification methods disagree, and why the real-click result is the one that matters here.

**Observed behavior:** The confirmation is drawn, functions correctly when triggered programmatically, and is logically answerable — but a real human cannot see it or click it while the restore picker is open, because the picker window covers the only place it renders.

**Evidence:** two screenshots (`before-real-click.png` / `after-real-click.png`, desktop-wide, not committed — ad hoc diagnostic artifacts outside the repo) show the picker's own footer unchanged and no confirmation visible in either frame; archive counts before/after the real click are identical (2 -> 2), while the second UI-Automation-driven delete attempt in the same session (same button, `InvokePattern`) did drop the count (2 -> 1).

**Disposition:** Per Task 2's action step 3, this is filed as a new finding for the owner, not fixed in this phase. Not auto-fixed under the deviation rules either, because the only real fix (e.g., giving `RestorePickerWindow` its own dialog host, or making `Confirm` route through a host visible above the current modal window) is an architectural change to the dialog-hosting design (Rule 4 territory), not a bug-level correction inside the current `Confirm`/`ShowRestorePickerDialog` contract this plan's tasks touch.

**Follow-up for the owner / phase close:** Add to `.planning/STATE.md` Blockers (orchestrator-owned write, not made here per this dispatch's instructions): *"Restore picker delete confirmation is unreachable by a real user while the picker is open (hidden behind the modal picker window) — confirmed 2026-11-18, see 02-10-SUMMARY.md."*

---

**Total deviations:** 0 auto-fixed. 1 new finding reported (not auto-fixed; architectural, Rule 4 territory).
**Impact on plan:** No scope creep — the finding is reported per the plan's own fallback instructions, and no code was changed to address it in this phase.

## Issues Encountered

- `.planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1` (a Plan 02-03 artifact) has a pre-existing PowerShell parse error: the chained `.Replace(...)` calls at lines 144-147 use a backtick line-continuation pattern that Windows PowerShell 5.1 fails to parse (`Unexpected token ".Replace"`). This was never caught before because 02-03-SUMMARY.md recorded that its authoring host was Linux, with no real PowerShell interpreter available, so only a structural brace/paren balance check was done there — not an actual parse. This plan's manual check needed a working demo catalog, so it built a minimal one-game catalog directly in its own ad hoc script instead of fixing `demo-data.ps1` (out of this plan's `files_modified` scope). **Flagging for the owner / whoever next touches the `screenshots/` scripts (Plan 02-04 or 02-11, which also depend on `demo-data.ps1`): the before/after screenshot capture flow for D-07 is currently broken on a real Windows PowerShell host and needs the chained-`.Replace` call reworked (e.g., one call per line without the backtick continuation, or `-replace` operators instead).**
- The manual sub-check initially appeared to pass under UI-Automation-only verification (`InvokePattern.Invoke()` on the Cancel/Delete buttons). A real-mouse-click + screenshot follow-up was run specifically because `InvokePattern` bypasses the OS input path and does not prove reachability for an actual human — this caught the real defect described above that the programmatic check alone would have missed.

## Known Stubs

None — both tasks are complete implementations; nothing deferred.

## Threat Flags

None beyond the plan's own threat register (T-02-17, T-02-18), which this plan satisfied as designed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ITEM-11's code-level scope (delete-action delegation, `Confirm`'s assertion) is complete and covered by the suite (739/739 passed, Release build 0/0).
- The restore-picker delete-confirmation visibility defect is a confirmed new finding, not a pass — it needs an owner decision (architectural: how the dialog host should work when a modal child window is open) before any fix, and should be added to `.planning/STATE.md` Blockers at phase close.
- `.planning/phases/02-audit-follow-ups/screenshots/demo-data.ps1`'s parse bug blocks Plans 02-04/02-11's screenshot capture flow on a real Windows host and should be fixed before those plans' capture runs are attempted there.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*

## Self-Check: PASSED

- All 6 modified files + this SUMMARY.md confirmed present on disk.
- Both task commits (`14707d5`, `987b131`) confirmed as ancestors of HEAD.
- All task-level `<acceptance_criteria>` re-verified (see Accomplishments / commit history); plan-level `<verification>` re-run: Release build 0/0, full suite 739/739 passed.
