---
status: testing
phase: 01-bugfixing-refactoring
source: [01-05-PLAN.md human-check]
started: 2026-11-17T23:30:00Z
updated: 2026-11-17T23:30:00Z
---

## Current Test

number: 1
name: Per-row backup, restore, open-folder, refresh and bulk run in the running app
expected: |
  The suite is fully green at the recorded total (689). In the app: the row's bar moves during
  the large backup and disappears afterwards; restore shows its three steps and the row's
  verdict settles; Explorer opens the save folder; Refresh flips rows to "Checking…" and back;
  the bulk run's progress panel counts up. No crash, no row left busy, no bar left visible.
awaiting: user response

## Tests

### 1. Per-row backup, restore, open-folder, refresh and bulk run in the running app
expected: Owner, on Windows (dotnet test already green at 689): start the app and, on one game
with a save folder of at least 50 MB, use the row's Back up, then its Restore (pick an archive,
confirm), then Open save folder from the row overflow; finally run Refresh and Back up selected
over two ticked games. The row's bar moves during the large backup and disappears afterwards;
restore shows its three steps and the row's verdict settles; Explorer opens the save folder;
Refresh flips rows to "Checking…" and back; the bulk run's progress panel counts up. No crash,
no row left busy, no bar left visible.
result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps
