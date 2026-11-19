---
status: complete
phase: 01-bugfixing-refactoring
source: [01-05-PLAN.md human-check, 01-VERIFICATION.md]
started: 2026-11-17T23:30:00Z
updated: 2026-11-19T17:43:55.292Z
---

## Current Test

[testing complete]

## Tests

### 1. Per-row backup, restore, open-folder, refresh and bulk run in the running app
expected: Owner, on Windows (dotnet test already green at 689): start the app and, on one game
with a save folder of at least 50 MB, use the row's Back up, then its Restore (pick an archive,
confirm), then Open save folder from the row overflow; finally run Refresh and Back up selected
over two ticked games. The row's bar moves during the large backup and disappears afterwards;
restore shows its three steps and the row's verdict settles; Explorer opens the save folder;
Refresh flips rows to "Checking…" and back; the bulk run's progress panel counts up. No crash,
no row left busy, no bar left visible.
result: [passed] — owner reply "uat ok" (blanket confirmation, 2026-11-18); no per-step
narration was given, so no per-step observation is recorded beyond this pass.

### 2. Bulk restore with declined confirmations and a large-file progress bar
expected: On Windows, run the app against a catalog of three or more games, one with a save
folder of at least 50 MB. Click Back up all changed; then tick two games and click Restore
selected, declining both confirmations. The window-level progress panel counts up game by game
and the large game's own bar moves during its write; the restore run shows the "Restoring N of
M" sentence and asks once per game in list order; no crash, no bar left visible, every row's
verdict settles afterwards.
result: pass

### 3. Unreadable (deny-ACL) save subfolder surfaces the warning sentence
expected: On Windows, give one catalog game a save folder containing a readable file and a
subfolder whose read permission is denied for your user (folder Properties -> Security -> Deny
"List folder / read data"). Start the app, then click Refresh twice; then back up that game. The
game's name cell shows "1 path(s) could not be read and are not counted here or backed up: ..."
in the warning colour after startup and after each Refresh; the backup succeeds and its message
carries the matching "could not be read and are NOT in this backup" warning; other rows are
unchanged.
result: pass

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
