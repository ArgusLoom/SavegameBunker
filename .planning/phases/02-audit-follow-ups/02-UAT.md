---
status: testing
phase: 02-audit-follow-ups
source: [02-VERIFICATION.md]
started: 2026-11-19T00:00:00Z
updated: 2026-11-19T00:00:00Z
---

## Current Test

number: 2
name: Alt-mnemonic access-key pass on the eight keyed toolbar buttons and the two unkeyed buttons
expected: |
  Each keyed button responds to its Alt+letter; the two unkeyed buttons render as plain text with
  no underlined letter.
awaiting: user response

## Tests

### 1. Open screenshots/before/ and compare against screenshots/after-19-20/ (items 19, 20)
expected: Each image shows only the app window in the state its name describes; toolbar icons, labels, spacing, hover colour and Alt underlines are identical in before vs after-19-20 (01-03); menu glyphs/text/hover/spacing and row-action busy collapse are identical (04-06); 01-09 and 14 are pixel-identical, 10-13 differ only in footer button width (>=110).
result: automated-pass — before/ captured 2026-11-19 from a 57811c1 worktree build (XAML unchanged since that commit); 9/14 shots obtainable (01,02,03,04,06,09,10,12,14 — same set capture.ps1 produces on every run, see item 7). ImageMagick `compare -metric AE` against after-19-20 gives ~1% AE on all 9 pairs, consistent with capture-to-capture background noise, no structural difference. 10-13 not capturable (item 7) so the footer-width clause is unverified.

### 2. Alt-mnemonic access-key pass on the eight keyed toolbar buttons and the two unkeyed buttons
expected: Each keyed button responds to its Alt+letter; the two unkeyed buttons render as plain text with no underlined letter.
result: [pending]

### 3. Run the app against 3+ games including one with a 500MB+ save folder and click Back up all changed
expected: The window-level progress panel counts up game by game with the right name, the large game's own bar moves during its write, and the panel disappears before the outcome message.
result: [pending]

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
result: issue — 9/14 shots captured (01,02,03,04,06,09,10,12,14), each shows only the app window in the expected state. 5 missing, same failure on both the 57811c1 build and current HEAD (so a capture.ps1 element-finder gap, not an app regression): 05-row-actions-idle and 07-statusbar ("op_Subtraction" method-not-found in capture.ps1's own element lookup), 08-restore-steps and 13-restorepicker-footer ("Restore button not found"), 11-changesavefolder-footer ("Change save folder menu item not found"). Separately found and fixed (worktree copy only, not committed): demo-data.ps1 lines 144-147 used backtick line-continuation immediately followed by CRLF, which both Windows PowerShell 5.1 and pwsh 7 fail to parse ("Unerwartetes Token '.Replace'") — pre-existing bug in the committed file, blocks any fresh demo-data.ps1 run until fixed there too.

## Summary

total: 7
passed: 3
issues: 2
pending: 2
skipped: 0
blocked: 0

## Gaps

- Item 6 above is a confirmed real defect (restore-picker delete confirmation hidden behind the
  modal), already recorded in `.planning/STATE.md` Blockers. Architectural fix, not scoped to this
  phase.
- Item 7 above found two capture-tooling defects: `demo-data.ps1` lines 144-147 (backtick
  line-continuation + CRLF parse failure, unfixed in the committed file) and `capture.ps1`'s
  element finders for 5 of 14 shots (05, 07, 08, 11, 13). Neither blocks the app itself — both are
  test-infrastructure gaps, not scoped to this phase's app changes.
- Items 2 and 3 remain pending: both need live interactive UI testing (Alt-mnemonic key presses,
  a real bulk-backup run against a 500MB+ save) that UI-Automation screenshot capture cannot cover.
