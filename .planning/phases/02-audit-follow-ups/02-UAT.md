---
status: complete
phase: 02-audit-follow-ups
source: [02-VERIFICATION.md]
started: 2026-11-19T00:00:00Z
updated: 2026-11-24T00:00:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Open screenshots/before/ and compare against screenshots/after-19-20/ (items 19, 20)
expected: Each image shows only the app window in the state its name describes; toolbar icons, labels, spacing, hover colour and Alt underlines are identical in before vs after-19-20 (01-03); menu glyphs/text/hover/spacing and row-action busy collapse are identical (04-06); 01-09 and 14 are pixel-identical, 10-13 differ only in footer button width (>=110).
result: automated-pass — before/ captured 2026-11-19 from a 57811c1 worktree build (XAML unchanged since that commit); 9/14 shots obtainable (01,02,03,04,06,09,10,12,14 — same set capture.ps1 produces on every run, see item 7). ImageMagick `compare -metric AE` against after-19-20 gives ~1% AE on all 9 pairs, consistent with capture-to-capture background noise, no structural difference. 10-13 not capturable (item 7) so the footer-width clause is unverified.

### 2. Alt-mnemonic access-key pass on the eight keyed toolbar buttons and the two unkeyed buttons
expected: Each keyed button responds to its Alt+letter; the two unkeyed buttons render as plain text with no underlined letter.
result: pass

### 3. Run the app against 3+ games including one with a 500MB+ save folder and click Back up all changed
expected: The window-level progress panel counts up game by game with the right name, the large game's own bar moves during its write, and the panel disappears before the outcome message.
result: pass

### 4. Compare screenshots/after-19-20/01, 10, 14 with screenshots/after-21/01, 10, 14
expected: Error-row text/icon and Add-game validation text keep the same red; only the title-bar close-button hover colour changes.
result: pass — cropped 14-close-hover.png close-button region (120x60 at +1380+0) visually compared: after-19-20 shows a muted rose-red hover, after-21 a brighter saturated red. Matches expectation. 01/10 AE ~1% (background noise only, no structural change).

### 5. Review every pair screenshots/after-19-20/NN vs screenshots/after-21/NN (and the never-captured before/NN vs after-21/NN)
expected: Spacing changes appear only at the status bar, the bulk-progress panel, the restore-step text and the empty-state link; the close-button hover red changes; everything else matches.
result: automated-pass for the 9 shots obtainable in all three sets (before/after-19-20/after-21 — see item 7 for which). `compare -metric AE`: 06-busy-bulk is the outlier at 6.8% (expected — bulk-progress panel region); all other pairs ~1.0-1.3% (background noise). One apparent anomaly investigated: after-21/01-main-default shows a "No saves yet" subtitle under "Large Save Game" absent from before/01 and after-19-20/01 — traced to `GameRowViewModel.ApplyStatus` (GameRowViewModel.cs:189) setting `StatusText` asynchronously after a background scan; a capture-timing race, not a code change (string present since initial commit, outside phase-02 scope). Restore-step text and empty-state link not independently isolated by crop — only the whole-image AE, which stayed in the noise band.

### 6. Run the app on a catalog with 2+ backups, open Restore, press a row's delete button, Cancel, then Delete
expected: The confirmation can be answered both times while the restore list is open; Cancel keeps the archive, Delete removes exactly that one row and its file.
result: already run — FAILED (confirmed real defect: confirmation renders hidden behind the modal RestorePickerWindow, unreachable by a real click; see 02-10-SUMMARY.md and .planning/STATE.md Blockers)

### 7. Open every image in screenshots/before/ and read the SUMMARY's shot table
expected: Each image shows only the app window, in the state its name describes; every shot not on disk has a reason.
result: issue — 9/14 shots captured (01,02,03,04,06,09,10,12,14), each shows only the app window in the expected state. 5 missing, same failure on both the 57811c1 build and current HEAD (so a capture.ps1 element-finder gap, not an app regression): 05-row-actions-idle and 07-statusbar ("op_Subtraction" method-not-found in capture.ps1's own element lookup), 08-restore-steps and 13-restorepicker-footer ("Restore button not found"), 11-changesavefolder-footer ("Change save folder menu item not found"). The demo-data.ps1 backtick/CRLF bug originally named here is NOT reproducible — diagnosis found it already fixed on this branch by commit 0e546aec (same day); only the 3 capture.ps1 element-lookup bugs remain open (see Gaps G-02-7).

### 8. Re-run UAT test 6: on a catalog where a game has two or more backups, open Restore, press a row's delete button, Cancel, then press it again and Delete — both with a real mouse.
expected: The confirmation renders inside the restore list window and is answerable by both clicks while the list is open; Cancel keeps the archive, Delete removes exactly that one row and its file.
result: [pending]

### 9. Re-run UAT test 1 (before/ vs after-19-20/ comparison for items 19, 20) on the now-complete fourteen-shot sets.
expected: Toolbar icons/labels/spacing/hover/Alt-underlines identical 01-05; menu/row-action identical 04-06; 01-09-14 pixel-identical; 10-13 differ only in footer-button width (D-12).
result: [pending]

### 10. Re-run UAT test 5 (after-19-20/ vs after-21/ comparison for item 21) and UAT test 7 (before/ shot-table completeness) on the complete sets.
expected: Spacing changes appear only at the status bar/bulk-progress panel/restore-step text/empty-state link; close-button hover colour changes; every one of the fourteen named shots is present and shows only the app window in its named state.
result: [pending]

### 11. Decide whether to restore the two disclosed-but-now-untracked findings (WR-01 backup-of-own-archive-folder repack; IN-01 orphaned `xs` Styles.xaml token) to WISHLIST.md or a fresh 02-REVIEW-DISPOSITION.md row.
expected: Either both findings get a durable home (WISHLIST.md or a disposition row), or an explicit owner decision that losing them is acceptable.
result: [pending]

## Summary

total: 11
passed: 5
issues: 2
pending: 4
skipped: 0
blocked: 0

## Gaps

- gap_id: G-02-6
  truth: "The confirmation can be answered both times while the restore list is open; Cancel keeps the archive, Delete removes exactly that one row and its file."
  status: failed
  reason: "User reported: confirmation renders hidden behind the modal RestorePickerWindow, unreachable by a real click (confirmed with SetCursorPos/mouse_event plus WindowFromPoint; see 02-10-SUMMARY.md and .planning/STATE.md Blockers)"
  severity: major
  test: 6
  root_cause: "App.xaml.cs:188-189 creates the app's only ContentDialogService bound to MainWindow's RootDialogHost. DialogService.ShowRestorePickerDialog (DialogService.cs:184-191) passes that same app-wide DialogService instance into RestorePickerViewModel, so RestorePickerViewModel.DeleteArchiveAsync's Confirm() call renders its ContentDialog inside MainWindow's RootDialogHost — a lower z-order HWND than RestorePickerWindow (a true modal child window via ShowDialog with Owner=MainWindow). The dialog paints behind the picker; UI Automation's InvokePattern still finds the logical element and gives a false positive."
  artifacts:
    - path: "src/SavegameBunker/App.xaml.cs"
      issue: "Only one ContentDialogService exists, bound to MainWindow's RootDialogHost"
    - path: "src/SavegameBunker/Services/DialogService.cs"
      issue: "ShowRestorePickerDialog (lines 184-191) passes the MainWindow-owned DialogService instance into RestorePickerViewModel instead of a RestorePickerWindow-owned one"
    - path: "src/SavegameBunker/ViewModels/RestorePickerViewModel.cs"
      issue: "DeleteArchiveAsync (lines 107-140) calls Confirm() on the MainWindow-scoped dialog service"
  missing:
    - "RestorePickerWindow needs its own ContentDialogHost in XAML"
    - "DialogService.ShowRestorePickerDialog needs to construct a second ContentDialogService bound to that host plus a second DialogService instance owned by the picker dialog (not _owner/MainWindow), passed into RestorePickerViewModel instead of this"
  debug_session: ""

- gap_id: G-02-7
  truth: "Each shot not on disk has a reason; every screenshot in screenshots/before/ is capturable and shows only the app window in the expected state."
  status: failed
  reason: "User reported: 5/14 shots (05-row-actions-idle, 07-statusbar, 08-restore-steps, 11-changesavefolder-footer, 13-restorepicker-footer) uncapturable due to capture.ps1 element-finder gaps. The demo-data.ps1 backtick/CRLF bug named in the original report is NOT reproducible — git history (commit 0e546aec, same day, already on dev/v1.1) already split the chained .Replace() calls; current working tree matches HEAD with no diff. Only the capture.ps1 gaps remain open."
  severity: minor
  test: 7
  root_cause: "Three distinct capture.ps1 element-lookup bugs: (A) shots 05/07 — capture.ps1:137 and :296 call `New-Object System.Drawing.Bitmap($x.Right - $x.Left, $x.Bottom - $x.Top)` unparenthesized; PowerShell's comma binds tighter than `-` inside the arg list, so it becomes an array literal before the second subtraction runs, throwing op_Subtraction. (B) shots 08/13 — capture.ps1:309-320 and :414-425 search ControlType.Custom descendants whose Name matches the display name, but DataGrid cells expose only a generic 'Element: ...GameRowViewModel, Anzeigeindex der Spalte: N' as Name; the real text lives in a nested ControlType.Text child two levels down, and no row sets AutomationProperties.Name. (C) shot 11 — capture.ps1:373 calls Find-ButtonByName (filters ControlType.Button), but MainWindow.xaml:678-682 defines 'Change save folder' as a ui:MenuItem (ControlType.MenuItem) inside a DropDownButton.Flyout that renders in its own top-level visual-tree root, outside $win's descendant tree entirely (documented in MainWindow.xaml:666-671)."
  artifacts:
    - path: ".planning/phases/02-audit-follow-ups/screenshots/capture.ps1"
      issue: "Line 137 (Save-ElementShot) and line 296 (Shot-07Statusbar): unparenthesized subtraction in Bitmap constructor args throws op_Subtraction"
    - path: ".planning/phases/02-audit-follow-ups/screenshots/capture.ps1"
      issue: "Lines 309-320 and 414-425: row/cell lookup matches ControlType.Custom Name instead of a nested ControlType.Text descendant, so Restore button search never finds its row"
    - path: ".planning/phases/02-audit-follow-ups/screenshots/capture.ps1"
      issue: "Line 373: Find-ButtonByName filters ControlType.Button and searches $win descendants, but 'Change save folder' is a ControlType.MenuItem in a separate top-level popup root"
  missing:
    - "Parenthesize each subtraction individually: New-Object System.Drawing.Bitmap(($x.Right - $x.Left), ($x.Bottom - $x.Top))"
    - "Row/cell lookup: iterate ControlType.DataItem rows, match a nested ControlType.Text descendant to the display name, then find the Restore button within that same DataItem"
    - "Shot 11: search ControlType.MenuItem (not Button) scoped to AutomationElement.RootElement's top-level children, matching the pattern Shot-12ScanFooter/Shot-13RestorePickerFooter already use for other popups"
  debug_session: ""

- Items 2 and 3 passed live: Alt-mnemonic keys respond correctly, and a real bulk-backup run
  against a 500MB+ save showed the progress panel behaving as expected.
