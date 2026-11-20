---
phase: 02-audit-follow-ups
plan: 16
subsystem: ui
tags: [wpf-ui, content-dialog, modal-window, dialog-service]

requires:
  - phase: 02-audit-follow-ups
    provides: "02-10's delete-action delegation and the confirmed G-02-6 finding (restore picker's delete confirmation unreachable by a real click while the picker is modal)"
provides:
  - "RestorePickerWindow has its own ui:ContentDialogHost (RootDialogHost), the same declaration MainWindow closes with"
  - "DialogService.ShowRestorePickerDialog builds a picker-owned ContentDialogService + DialogService bound to that host and hands it to RestorePickerViewModel instead of `this`"
  - "Real-click evidence script (.temp/02-16-picker-delete-check.ps1, gitignored) proving the confirmation is answerable by a real mouse click while the picker is modal"
affects: [".planning/STATE.md Blocker for Phase 02 Item 11 (clearable once the owner re-runs UAT test 6)"]

actuals:
  tokens: 1210        # chars/4 over the realized diff (git diff 880442f..HEAD, 4841 chars)
  tasks: 2
  commits: 2
  plan_head_before: 880442f2d56724f54197029650658d802e43282e
  plan_head_after: da2a4e11d1333a3c2e508739455fef1e57059a31

tech-stack:
  added: []           # zero packages; shipping csproj unchanged (T-02-SC) — ContentDialogService/ContentDialogHost are WPF-UI types already referenced
  patterns: ["per-window ContentDialogHost + DialogService pair for a modal child window that needs its own answerable dialogs"]

key-files:
  created: []
  modified:
    - src/SavegameBunker/Views/RestorePickerWindow.xaml
    - src/SavegameBunker/Views/RestorePickerWindow.xaml.cs
    - src/SavegameBunker/Services/DialogService.cs
    - CHANGELOG.md

key-decisions:
  - "Took exactly the diagnosed fix from 02-UAT.md's G-02-6 root_cause/missing fields: a second ContentDialogHost + ContentDialogService + DialogService scoped to the picker window, with no change to IDialogService, App.xaml.cs or any view model."
  - "RestorePickerWindow's constructor became parameterless (DataContext assigned by DialogService after construction) because the picker's own RootDialogHost must exist before the picker-owned DialogService can be built — matching the order the plan's must_haves pinned."
  - "The real-click evidence script needed two fixes beyond the plan's pseudocode to actually work on this host: (1) SetThreadDpiAwarenessContext(-4) before any SetCursorPos call — without it, DPI virtualization silently remapped clicks on this scaled display (verified: SetCursorPos(500,500) landed the cursor at (0,559) without it); (2) the restore picker's owned window surfaces as a UI Automation DESCENDANT of MainWindow's own element in this environment, not as a child of the desktop root — Find-PickerWindow searches MainWindow's descendants first, falling back to the root-children search the plan's prose described."

requirements-completed: [ITEM-11]

coverage:
  - id: D1
    description: "RestorePickerWindow gets its own ui:ContentDialogHost; DialogService.ShowRestorePickerDialog builds a picker-owned ContentDialogService + DialogService bound to it and passes that to RestorePickerViewModel instead of `this`"
    requirement: "ITEM-11"
    verification:
      - kind: other
        ref: "dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental (0 Warnings, 0 Errors) + dotnet test SavegameBunker.sln -c Release -tl:off (745/745 passed) + all 11 plan acceptance-criteria greps"
        status: pass
    human_judgment: false
  - id: D2
    description: "A real mouse click (never UI Automation's InvokePattern) reaches the picker's delete confirmation while the picker is modal: the confirmation appears within 3s (check a), is visible fully inside the picker in a screenshot (check b), WindowFromPoint at its Delete button resolves to the picker's own HWND (check c), a real Cancel keeps the archive and leaves the picker open (check d), and a real Delete removes exactly one archive (check e)"
    requirement: "ITEM-11"
    verification:
      - kind: manual_procedural
        ref: ".temp/02-16-picker-delete-check.ps1 (gitignored evidence script) — run twice on this Windows host, both runs printed PASS for a, b, c, d, e and exited 0"
        status: pass
    human_judgment: true
    rationale: "The scripted real-click run is strong evidence, but the plan's own <verify> names UAT test 6 as the owner's check of record and asks for a human run with a real mouse on a catalog with 2+ backups; that run is still pending the owner."

duration: 55min
completed: 2026-11-20
status: complete
---

# Phase 02 Plan 16: Restore picker delete confirmation hosted in its own window Summary

**The restore picker's delete confirmation now renders inside `RestorePickerWindow`'s own `ContentDialogHost` instead of `MainWindow`'s, so a real mouse click — not just UI Automation's `InvokePattern` — can reach it while the picker is modal; proven twice on this host with `SetCursorPos`/`mouse_event` clicks plus `WindowFromPoint`.**

## Performance

- **Duration:** 55 min
- **Started:** 2026-11-19T23:05Z (approx.)
- **Completed:** 2026-11-19T23:59:53Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- `RestorePickerWindow.xaml` gets its own `<ui:ContentDialogHost x:Name="RootDialogHost">` as the last child of its root `Grid` — the same `Grid.Row="0" Grid.RowSpan="2"` declaration `MainWindow.xaml` closes with — with an XML comment explaining why a modal child window needs a dialog host of its own (G-02-6).
- `RestorePickerWindow`'s constructor is now parameterless; `InitializeComponent()` only. `DialogService` builds the picker-owned dialog service first (which needs `RootDialogHost` to exist) and assigns `DataContext` itself afterward.
- `DialogService.ShowRestorePickerDialog` now builds `pickerContentDialogs` (a `ContentDialogService` bound to `dialog.RootDialogHost`) and `pickerDialogs` (a `DialogService` over the picker window, that service, and the existing `_snackbarService`), and passes `pickerDialogs` — not `this` — into `RestorePickerViewModel`. The snackbar stays the main window's; the picker only ever raises `Confirm`/`Error`.
- `IDialogService`, `App.xaml.cs`, every view model and every test are byte-identical to `880442f` (acceptance-criteria `git diff --quiet` check); the shipping csproj still carries exactly two `PackageReference` entries.
- A real-click evidence script (`.temp/02-16-picker-delete-check.ps1`, gitignored, never committed) backs up "Small Save Game" twice via real clicks, opens the restore picker, and runs checks (a)-(e) plus one observation — all with `SetCursorPos` + `mouse_event`, never `InvokePattern` (which gave a false pass in Plan 02-10). Run twice on this host; both runs printed PASS for every check.

## Task Commits

Each task was committed atomically:

1. **Task 1: The picker's delete confirmation end to end** - `1156099` (fix)
2. **Task 2: Real-click proof + CHANGELOG entry** - `da2a4e1` (docs)

**Plan metadata:** commit pending (this SUMMARY; STATE.md/ROADMAP.md are the orchestrator's to update per this dispatch's instructions)

## Files Created/Modified

- `src/SavegameBunker/Views/RestorePickerWindow.xaml` - added the picker's own `RootDialogHost`
- `src/SavegameBunker/Views/RestorePickerWindow.xaml.cs` - parameterless constructor, dropped the now-unused `SavegameBunker.ViewModels` using
- `src/SavegameBunker/Services/DialogService.cs` - `ShowRestorePickerDialog` builds and hands the picker a dialog service bound to its own host
- `CHANGELOG.md` - Fixed entry: "Deleting a backup in the restore list now asks inside the restore list."

## Decisions Made

See `key-decisions` in the frontmatter. In short: implemented exactly the diagnosed fix from `02-UAT.md`'s G-02-6 entry, touching only the three files the plan named; the evidence script needed a DPI-awareness fix and a UI-Automation-tree-location fix beyond its pseudocode to actually exercise real clicks correctly on this host (see Deviations below).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The evidence script's real clicks landed at the wrong screen coordinates on this scaled display**
- **Found during:** Task 2 (writing and first-running `.temp/02-16-picker-delete-check.ps1`)
- **Issue:** `SetCursorPos` coordinates computed from UI Automation's (DPI-aware, physical-pixel) `BoundingRectangle` were silently remapped by Windows' DPI virtualization for the (non-DPI-aware) PowerShell host process on this scaled display. Verified directly: `SetCursorPos(500, 500)` moved the cursor to `(0, 559)`, not `(500, 500)`. Every backup/restore/delete click in Task 2's check script was landing on the wrong control, which is why the first several runs produced 0 archives after two "Back up" clicks.
- **Fix:** Added `SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)` (`-4`) once at script startup, before any `SetCursorPos` call. Verified the fix directly: the same `SetCursorPos(500, 500)` call then landed the cursor at exactly `(500, 500)`.
- **Files modified:** `.temp/02-16-picker-delete-check.ps1` (gitignored scratch tooling, not a deliverable)
- **Verification:** both full runs of the script printed PASS for checks a-e with the exact expected archive-count deltas (2→2 on Cancel, 2→1 on Delete).
- **Committed in:** not committed (gitignored by design per the plan's `<files>`)

**2. [Rule 3 - Blocking] `RestorePickerWindow`'s owned window did not surface where the plan's pseudocode expected to find it**
- **Found during:** Task 2 (locating the picker window by `ProcessId` + `ControlType.Window` + title)
- **Issue:** The plan's action text (and the pattern `capture.ps1` uses elsewhere for top-level dialogs) looks for the picker as a child of the UI Automation desktop root (`AutomationElement.RootElement.FindAll(TreeScope.Children, ...)`). On this host, the owned `RestorePickerWindow` instead surfaces as a **descendant** of `MainWindow`'s own `AutomationElement` — confirmed by dumping top-level windows after a real click on the row's Restore button (only `MainWindow`'s title appeared at the root, while the picker's footer and row buttons were found by a plain descendant search scoped to `MainWindow`).
- **Fix:** `Find-PickerWindow` tries a descendant search scoped to the main app window first (where it was actually found), falling back to the original root-children search for an environment where a different Automation provider places it there instead.
- **Files modified:** `.temp/02-16-picker-delete-check.ps1` (gitignored scratch tooling, not a deliverable)
- **Verification:** same as above — both full runs located the picker on the first attempt and passed all five checks.
- **Committed in:** not committed (gitignored by design per the plan's `<files>`)

---

**Total deviations:** 2 auto-fixed (both Rule 3, both confined to the gitignored evidence script — no production code was affected).
**Impact on plan:** No scope creep. Both fixes were required to get truthful real-click evidence at all on this host; without them the script would have reported false failures (or, with the DPI bug alone, silently clicked the wrong things without the picker ever opening).

## Issues Encountered

None beyond the two deviations above.

## Real-click check evidence (Task 2)

Both full runs of `.temp/02-16-picker-delete-check.ps1` on this host printed:

```
a PASS confirmation "Delete" button appeared in the picker subtree within 3s
b PASS screenshot of the picker rectangle saved (inspect by eye: confirmation fully inside the picker)
c PASS WindowFromPoint at Delete button centre (2414,1146) returned <picker HWND>; picker HWND is <picker HWND>
d PASS zip count 2 -> 2 (expected unchanged), picker still open: True
e PASS zip count 2 -> 1 (expected -1), picker rows 2 -> 1 (expected -1)
Observation (Alt+F4 while confirmation open): MainWindow process present: True; Responding: True; Refresh clicked for real: True; remaining archive count: 1 (expected unchanged at 1)
Done — checks a-e all PASS.
```

- **Screenshot (check b):** `C:\Dev\SavegameBunker\.temp\02-16-confirm-inside-picker.png` (gitignored, not committed). Viewed by eye: the confirmation ("Delete this archive permanently? This deletes the ZIP file on disk." with Delete/Cancel buttons) renders fully inside the picker window, below its header and above its footer Restore/Cancel row — nothing from it falls outside the picker's bounds.
- **Archive counts:** 2 archives after two real-click backups → 2 after a real-click Cancel (check d) → 1 after a real-click Delete (check e), at `%TEMP%\SavegameBunkerDemo\backups\small-save-game`.
- **Alt+F4 observation (not a pass/fail gate, per the plan):** with a third confirmation left open on the remaining row, Alt+F4 (via `keybd_event`) closed the picker without answering it. `MainWindow`'s process remained present and `Responding: True`; its Refresh button was clicked for real and the click succeeded; the remaining archive count stayed at 1 (unchanged — the abandoned confirmation's background `Confirm` call never got an answer, so nothing was deleted, matching the threat register's T-02-30 "accept" disposition).

## Known Stubs

None.

## Threat Flags

None beyond the plan's own threat register (T-02-29, T-02-30, T-02-SC), which this plan satisfied as designed — see coverage D1/D2 and the Alt+F4 observation above for T-02-29/T-02-30; the `<PackageReference` count stayed at 2 for T-02-SC.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- G-02-6's code-level fix is complete and covered by the suite (745/745 passed, Release build 0/0) and by two full real-click evidence runs.
- The plan's `<verify>` names UAT test 6 as the owner's check of record: run the app on a catalog where a game has 2+ backups, open Restore, click a row's delete button, answer Cancel then (on a second attempt) Delete, both with the mouse. Once done, `.planning/STATE.md`'s Blocker for Phase 02 Item 11 can be cleared.
- The out-of-scope finding noted in the plan's `<objective>` stands unchanged: `AboutDialogViewModel` still raises `Error`/`Info` through the `MainWindow`-scoped service while the modal About window is open (fire-and-forget, no blocked caller) — not fixed here, reported only.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-20*

## Self-Check: PASSED

- All 4 modified files (`RestorePickerWindow.xaml`, `RestorePickerWindow.xaml.cs`, `DialogService.cs`, `CHANGELOG.md`) confirmed present on disk with the expected changes.
- Both task commits (`1156099`, `da2a4e1`) confirmed as ancestors of HEAD.
- All 11 of Task 1's acceptance-criteria greps re-verified as passing; Task 2's CHANGELOG grep and the `.temp` untracked-files check re-verified as passing.
- Plan-level `<verification>`: Release build 0 Warnings/0 Errors; full suite 745/745 passed; `IDialogService`/`App.xaml.cs`/view models/tests byte-identical to `880442f` (`git diff --quiet` exit 0).
