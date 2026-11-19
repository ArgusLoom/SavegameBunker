---
status: testing
phase: 02-audit-follow-ups
source: [02-VERIFICATION.md]
started: 2026-11-19T00:00:00Z
updated: 2026-11-19T00:00:00Z
---

## Current Test

number: 1
name: Open screenshots/before/ and compare against screenshots/after-19-20/ (items 19, 20)
expected: |
  Each image shows only the app window in the state its name describes; toolbar icons, labels,
  spacing, hover colour and Alt underlines are identical in before vs after-19-20 (01-03); menu
  glyphs/text/hover/spacing and row-action busy collapse are identical (04-06); 01-09 and 14 are
  pixel-identical, 10-13 differ only in footer button width (>=110).
awaiting: user response

## Tests

### 1. Open screenshots/before/ and compare against screenshots/after-19-20/ (items 19, 20)
expected: Each image shows only the app window in the state its name describes; toolbar icons, labels, spacing, hover colour and Alt underlines are identical in before vs after-19-20 (01-03); menu glyphs/text/hover/spacing and row-action busy collapse are identical (04-06); 01-09 and 14 are pixel-identical, 10-13 differ only in footer button width (>=110).
result: [pending]

### 2. Alt-mnemonic access-key pass on the eight keyed toolbar buttons and the two unkeyed buttons
expected: Each keyed button responds to its Alt+letter; the two unkeyed buttons render as plain text with no underlined letter.
result: [pending]

### 3. Run the app against 3+ games including one with a 500MB+ save folder and click Back up all changed
expected: The window-level progress panel counts up game by game with the right name, the large game's own bar moves during its write, and the panel disappears before the outcome message.
result: [pending]

### 4. Compare screenshots/after-19-20/01, 10, 14 with screenshots/after-21/01, 10, 14
expected: Error-row text/icon and Add-game validation text keep the same red; only the title-bar close-button hover colour changes.
result: [pending]

### 5. Review every pair screenshots/after-19-20/NN vs screenshots/after-21/NN (and the never-captured before/NN vs after-21/NN)
expected: Spacing changes appear only at the status bar, the bulk-progress panel, the restore-step text and the empty-state link; the close-button hover red changes; everything else matches.
result: [pending]

### 6. Run the app on a catalog with 2+ backups, open Restore, press a row's delete button, Cancel, then Delete
expected: The confirmation can be answered both times while the restore list is open; Cancel keeps the archive, Delete removes exactly that one row and its file.
result: already run — FAILED (confirmed real defect: confirmation renders hidden behind the modal RestorePickerWindow, unreachable by a real click; see 02-10-SUMMARY.md and .planning/STATE.md Blockers)

### 7. Open every image in screenshots/before/ and read the SUMMARY's shot table
expected: Each image shows only the app window, in the state its name describes; every shot not on disk has a reason.
result: [pending]

## Summary

total: 7
passed: 0
issues: 1
pending: 6
skipped: 0
blocked: 0

## Gaps

- Item 6 above is a confirmed real defect (restore-picker delete confirmation hidden behind the
  modal), already recorded in `.planning/STATE.md` Blockers. Architectural fix, not scoped to this
  phase.
