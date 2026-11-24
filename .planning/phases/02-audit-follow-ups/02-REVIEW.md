---
phase: 02-audit-follow-ups
reviewed: 2026-11-24T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - CHANGELOG.md
  - src/SavegameBunker/Services/DialogService.cs
  - src/SavegameBunker/Views/RestorePickerWindow.xaml
  - src/SavegameBunker/Views/RestorePickerWindow.xaml.cs
findings:
  critical: 0
  warning: 1
  info: 0
  total: 1
  necessity: 0
status: issues_found
---

# Phase 2: Code Review Report

**Reviewed:** 2026-11-24
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Reviewed the four files Plan 02-16 changed to close gap G-02-6 (restore picker's delete
confirmation unreachable by a real mouse click while the picker is modal): `DialogService.cs`,
`RestorePickerWindow.xaml(.cs)`, and the `CHANGELOG.md` entry. Cross-checked against
`02-16-PLAN.md`'s `must_haves`/`acceptance_criteria` and `02-16-SUMMARY.md` rather than trusting
either document's own claims:

- `git diff --quiet 880442f -- src/SavegameBunker/App.xaml.cs src/SavegameBunker/Services/IDialogService.cs src/SavegameBunker/ViewModels/ tests/` exits `0` — confirmed `IDialogService`, `App.xaml.cs`, every view model and the test suite are untouched, so the fix really is confined to the one WPF-aware service and the one view, matching CLAUDE.md's UI-boundary rule.
- `SavegameBunker.csproj` still carries exactly two `PackageReference` entries (WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2) — `ContentDialogService`/`ContentDialogHost` are WPF-UI types already referenced, not a new dependency.
- No test constructs `RestorePickerWindow` directly (grepped `tests/`), so dropping its view-model constructor parameter for a parameterless one is safe.
- `DialogService.ShowRestorePickerDialog` builds the picker's own `ContentDialogService`/`DialogService` pair bound to `dialog.RootDialogHost` in the order the host must exist before it (constructor → `SetDialogHost` → `DialogService` → `RestorePickerViewModel` → `DataContext` → `ShowDialog`), mirroring `App.xaml.cs`'s own `MainWindow` wiring. `RestorePickerWindow.xaml.cs`'s now-unused `using SavegameBunker.ViewModels;` was correctly dropped.
- `RestorePickerWindow.xaml`'s new `ui:ContentDialogHost` is the last child of the root `Grid` with `Grid.Row="0" Grid.RowSpan="2"` — same declaration and z-order position as `MainWindow.xaml`'s, with an XML comment explaining why a modal child window needs a host of its own.
- The CHANGELOG `### Fixed` entry is inserted directly after the related "cancelling now updates the game's row" entry, in the file's established voice.

No logic errors or edge-case gaps were found in the code added by this plan itself — it is a
faithful, minimal implementation of the diagnosed fix. One pre-existing robustness gap in
`DialogService.Confirm`, unchanged by this plan but now reachable from a second window, is
reported below.

## Warnings

### WR-01: `Confirm`'s blocked worker thread has no escape if the owning window closes while a dialog is pending

**File:** `src/SavegameBunker/Services/DialogService.cs:118-136` (`Confirm`), `src/SavegameBunker/Services/DialogService.cs:184-202` (`ShowRestorePickerDialog`)
**Issue:** `Confirm` is called from a `Task.Run` background thread and blocks that thread synchronously on `task.GetAwaiter().GetResult()` (line 135) until the dispatcher-marshalled `ShowSimpleDialogAsync` call completes. Nothing in `Confirm`, `ShowQueuedAsync` or `ShowAfterAsync` ties that wait to the owning window's lifetime: if the window hosting the dialog is closed (Alt+F4) before the user answers, there is no code path that cancels or faults the pending `ContentDialogService` task. Before this plan, the picker's `RestorePickerViewModel` shared `MainWindow`'s long-lived `DialogService`/host, so an abandoned confirmation there was a one-off hazard; this plan now routes the same unbounded wait through a picker-scoped `DialogService` (`ShowRestorePickerDialog`, lines 194-201) created and discarded per dialog, so the same gap recurs every time the picker is opened and closed with a confirmation left unanswered. `02-16-PLAN.md`'s own threat register already names this exact scenario as T-02-30 ("Confirm worker when the picker is closed... while its confirmation is open") with disposition "accept" and severity "low," and `02-16-SUMMARY.md`'s Alt+F4 observation shows the *main window* keeps responding — but neither artifact states what happens to the thread-pool thread blocked inside the closed picker's `Confirm` call, and nothing in the delivered code gives that thread a way to return. Repeated Alt+F4-while-confirming interactions would each leak one blocked thread-pool thread for the life of the process.
**Fix:** This is a pre-existing gap in `Confirm`'s design, not something this plan need have fixed under its own narrow scope — but if it is picked up, the straightforward fix is to have the window's `Closed` event cancel or fault the in-flight dialog task (e.g., a `CancellationTokenSource` passed to `ShowSimpleDialogAsync` in place of `CancellationToken.None`, cancelled from `Window.Closed`), so a closed window's blocked `Task.Run` caller is released instead of hanging until process exit. At minimum, record the accepted limitation in a code comment on `Confirm` itself (it currently only explains the deadlock-avoidance reasoning, not this gap) so a future reader does not have to rediscover T-02-30 from a planning artifact that may later be archived.

---

_Reviewed: 2026-11-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
