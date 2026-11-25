---
phase: 01-bugfixing-refactoring
fixed_at: 2026-11-25T00:00:00Z
review_path: C:/Dev/SavegameBunker/.planning/phases/01-bugfixing-refactoring/01-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 4
skipped: 1
status: partial
---

# Phase 01: Code Review Fix Report

**Fixed at:** 2026-11-25T00:00:00Z
**Source review:** C:/Dev/SavegameBunker/.planning/phases/01-bugfixing-refactoring/01-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (WR-01..WR-05; Info findings excluded by instruction)
- Fixed: 4
- Skipped: 1

**Verification:** every fix ran the full suite (`dotnet test SavegameBunker.sln`) in the main
checkout before its commit. `workflow.use_worktrees` is `false` in `.planning/config.json`, so no
worktree was created and the numbers are reproducible from the current tree. Baseline was 772
green; the last commit leaves 781 green (9 new tests, 0 failures). Each new test was seen red
before its fix, except where the test needed the new member to compile (WR-02, the `HasWarning`
tests of WR-04).

## Fixed Issues

### WR-02: `DirectoryWalk` does not guard `MoveNext`, so one mid-listing IO error aborts the whole walk

**Files modified:** `src/SavegameBunker/Services/DirectoryWalk.cs`, `tests/SavegameBunker.Tests/DirectoryWalkTests.cs` (new), `CHANGELOG.md`
**Commit:** e9e7c78
**Applied fix:** Added `DirectoryWalk.GuardListing`, an iterator that drives the enumerator by hand
and turns a filesystem fault thrown from `GetEnumerator`/`MoveNext` into "this listing ends here"
plus one callback. `Walk` reads both the file listing and the subfolder listing through it. A
per-folder flag makes the folder be reported as skipped once even if both listings fault. Non-fs
exceptions still propagate, and consumer exceptions are not caught (the `yield return` sits
outside the guarded try). Class remarks updated. `ReparseFile.HasAnyFile` was left alone on
purpose: its documented contract is that a throw for the folder it was called on propagates to
the caller (which already catches it), and nested folders are already wrapped by the caller's
catch, so it does not have the abort-the-walk behaviour. Tests pin the helper (mid-listing fault
yields what it had and reports once; healthy listing; non-fs fault propagates); a deterministic
mid-`MoveNext` IO error cannot be produced on a real temp tree, so the walk itself is covered
through the helper.
**Status:** fixed

### WR-03: Bulk restore leaves rows stuck on "Checking" and a restore bar stuck on if an item throws (previous WR-01, still open)

**Files modified:** `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs`, `src/SavegameBunker/ViewModels/MainViewModel.cs`, `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs`, `CHANGELOG.md`
**Commit:** be8169b
**Applied fix:** The `finally` in `RestoreAsync` now also zeroes `RestoreStep` and
`RestoreStepText` per target (after `IsBusy = false`, before `onFinished`), and the
`StartStatusScan(only: targets)` moved from after the awaited call into the `onFinished`
callback, mirroring the backup path. Restore order is untouched. New coordinator test makes the
loop throw mid-run and asserts `onFinished` runs once, rows are released and the step display is
cleared (red before the fix).
**Status:** fixed

### WR-04: Bulk backup reports "all N backed up" and drops each game's incompleteness and restore-cap warnings

**Files modified:** `src/SavegameBunker/Models/OperationResult.cs`, `src/SavegameBunker/Services/BackupService.cs`, `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs`, `src/SavegameBunker/ViewModels/MainViewModel.cs`, `tests/SavegameBunker.Tests/BackupServiceTests.cs`, `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs`, `tests/SavegameBunker.Tests/MainViewModelBulkTests.cs`, `SPEC.md`, `CHANGELOG.md`
**Commit:** 30d8634
**Applied fix:** Deviated from the review's suggested internal-overload call, because that route
cannot see the pruned-folder or oversize warnings and string-matching the message would have
flagged the informational "Left out" note, which CLAUDE.md says must never read as a warning.
Instead `OperationResult` gained an optional `HasWarning` flag (`OkWithWarning`), which
`BackupService.Backup` sets for skipped paths, not-backed-up subfolders and an over-cap archive,
and never for the left-out note. `BulkBackupRun` gained `Warnings`; the bulk loop collects the
message of each warned success. `MainViewModel.Report` now appends its detail list to a success
dialog as well as a failure one, and the bulk backup passes failures first, then warnings. The
status line stays the one-line summary (same split as failures). SPEC.md's partial-archive
bullet notes that a bulk backup keeps those warnings. Tests: flag set/not set for locked file,
clean backup, left-out-only and not-backed-up layouts; coordinator collects the message; the view
model's success dialog carries it (red before the `Report` change).
**Status:** fixed: requires human verification (the closing-dialog wording and the `HasWarning`
shape are small design choices the reviewer left open; the review offered two presentations and
this takes the "surface their messages the way `failureDetails` does" one)

### WR-05: Success-message warnings run into each other with no separating punctuation

**Files modified:** `src/SavegameBunker/Services/BackupService.cs`, `tests/SavegameBunker.Tests/BackupServiceTests.cs`, `CHANGELOG.md`
**Commit:** 214f6ba
**Applied fix:** `DescribeSkippedPathsWarning` and `DescribeNotBackedUpWarning` now end with a
full stop. New test asserts the exact combined message for a skipped-path plus left-out backup
(red before the fix). The oversize warning follows `createdMessage`, which now always ends in a
period, so it needed no change.
**Status:** fixed

## Skipped Issues

### WR-01: A backup's timestamp is taken after verification, so a save rewritten during the backup reads "Up to date"

**File:** `src/SavegameBunker/Services/BackupService.cs:198-200`
**Reason:** skipped: needs a design decision, not applied. The late stamp is a documented, deliberate
rule, not an oversight: SPEC.md section 3 (Backup) says "The timestamp is taken after the archive is
written and verified, not before, so a long backup never dates itself earlier than the save files
it just captured", and the code comment at lines 112-115 says the same. The review's
start-of-backup stamp trades the silent false "Up to date" for a false "changed since backup" on any
save touched during a long archive pass, and changes SPEC behaviour, the archive naming
semantics and the ordering of near-simultaneous backups. The finding itself says to pin the case
and document a rejection if the late stamp is intended. Left for the owner to decide; if the
start-time stamp is chosen, the change is a one-variable move in `BackupService.Backup` plus a SPEC
rewrite of those two sentences and `docs/DECISIONS.md`.
**Original issue:** A save rewritten in place during the archive pass has an mtime earlier than the
late `TakenAt` but content not in the archive, so the row reads "Up to date" for a backup that
lacks the newest save.

---

_Fixed: 2026-11-25T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
