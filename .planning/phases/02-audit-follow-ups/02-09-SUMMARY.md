---
phase: 02-audit-follow-ups
plan: 09
subsystem: services
tags: [backup, restore, verification, exceptions, refactor]

requires:
  - phase: 02-audit-follow-ups
    provides: "the walk pruning fix (02-01/02-05) and the empty-archive fix (02-07) this extraction rebases on top of"
provides:
  - "Two-mode DescribeVerificationFailure (recorded hashes for backup, disk-hash for restore) instead of three"
  - "MoveStagedArchiveIntoPlace extracted from Backup, isolating the rename/suffix ladder"
  - "FileSystemFault.Is(Exception) — one predicate replacing eleven per-site exception lists"
affects: [backup-service, restore-service, directory-walk, drive-hints, reparse-file]

actuals:
  tokens: 5901
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "One predicate for a repeated catch-filter union, named for what it detects (FileSystemFault.Is), not for the mechanism"
    - "Extracted private-static method returning a nullable failure plus an out parameter, mirroring the existing OperationResult? convention"

key-files:
  created:
    - src/SavegameBunker/Services/FileSystemFault.cs
  modified:
    - src/SavegameBunker/Services/BackupService.cs
    - src/SavegameBunker/Services/RestoreService.cs
    - src/SavegameBunker/Services/DirectoryWalk.cs
    - src/SavegameBunker/Services/DriveHints.cs
    - src/SavegameBunker/Services/ReparseFile.cs
    - tests/SavegameBunker.Tests/BackupServiceTests.cs

key-decisions:
  - "DescribeVerificationFailure's content loop now always runs (ITEM-7); expectedHashes null vs non-null is the only remaining switch, replacing the deleted verifyContent flag"
  - "MoveStagedArchiveIntoPlace keeps the two catch levels (inner collision/limit, outer generic) distinct per ROADMAP, rather than folding them into one"
  - "FileSystemFault.Is is IOException or UnauthorizedAccessException only — DirectoryNotFoundException/FileNotFoundException/PathTooLongException are already covered since all three derive from IOException"

requirements-completed: [ITEM-6, ITEM-7, ITEM-9]

coverage:
  - id: D1
    description: "DescribeVerificationFailure has exactly two modes (recorded-hash backup mode, disk-hash restore mode); the four BackupServiceTests facts assert unchanged messages on production modes"
    requirement: "ITEM-7"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#DescribeVerificationFailure_returns_null_when_the_archive_matches_its_source"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#DescribeVerificationFailure_names_the_entry_count_difference_after_a_file_is_added"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#DescribeVerificationFailure_names_the_byte_counts_after_a_source_file_grows"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#DescribeVerificationFailure_reports_an_unreadable_archive_without_throwing"
        status: pass
      - kind: integration
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs (full file, restore round-trip and corruption facts)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Backup's rename/suffix ladder extracted into MoveStagedArchiveIntoPlace; the rename-cap and same-second collision facts stay green unchanged"
    requirement: "ITEM-6"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs (rename-cap and collision facts, full filtered run)"
        status: pass
    human_judgment: false
  - id: D3
    description: "One FileSystemFault.Is(Exception) predicate replaces the eleven per-site exception lists in BackupService, DirectoryWalk, DriveHints and ReparseFile; DeleteArchive, the rename-collision filters and the two stale-temp cleanups are untouched"
    requirement: "ITEM-9"
    verification:
      - kind: unit
        ref: "dotnet test SavegameBunker.sln -c Release (full suite, 738 passed)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-18
status: complete
---

# Phase 2 Plan 9: Services clean-up — verification modes, rename ladder, fault predicate Summary

**Collapsed DescribeVerificationFailure's two-flag/three-mode shape into one optional parameter, extracted Backup's rename/suffix ladder into MoveStagedArchiveIntoPlace, and replaced eleven per-site filesystem exception lists with one FileSystemFault.Is predicate — all three behavior-preserving.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-11-18T20:50:00Z
- **Completed:** 2026-11-18T21:15:00Z
- **Tasks:** 3
- **Files modified:** 7 (1 created, 6 modified)

## Accomplishments

- **ITEM-7:** `DescribeVerificationFailure` no longer takes a `verifyContent` bool. The content-hash loop runs unconditionally; a non-null `expectedHashes` (backup) compares against recorded hashes, a null one (restore) hashes the on-disk file under `sourceDir` instead. `RestoreService` calls the two-argument form with an updated comment. The byte-count-growth test now passes `expectedHashes` computed with `SHA256.HashData`/`Convert.ToHexString` so it still exercises the byte-count message instead of tripping the content check first; the other three facts keep their `(archive, saveDir)` call under the new disk-hash mode with unchanged assertions.
- **ITEM-6:** `Backup`'s rename/suffix ladder (candidate naming, `File.Move`, the collision filter, the limit catch with its `TryDelete` and message) moved into `private static OperationResult? MoveStagedArchiveIntoPlace(string tempPath, string targetDir, string timestamp, string suffix, string gameDisplayName, out string finalPath)`. `Backup` now wraps a single call in its outer generic catch; the two catch levels (inner collision/limit inside the extracted method, outer generic in `Backup`) stay distinct. `MaxRenameRetries` semantics and the exact limit message are byte-identical.
- **ITEM-9:** New `internal static class FileSystemFault` with `internal static bool Is(Exception ex) => ex is IOException or UnauthorizedAccessException;` replaces the eleven per-site exception-type lists across `BackupService` (`ListBackupsCore`'s projection, `InspectSaveDir`'s four catches folded to one, `ArchiveSaveDir`'s `File.OpenRead` catch), `DirectoryWalk` (all four filters in `Walk`), `DriveHints` (the two catches in the drive loop folded to one `continue`), and `ReparseFile` (the three groups in `HasAnyFile`). `DeleteArchive`'s two message-specific catches, the rename-collision filters, and the two stale-temp cleanups (bare `catch` already) are untouched.

## Task Commits

Each task was committed atomically:

1. **Task 1: ITEM-7 end to end — two verification modes, restore and the four facts on production modes** - `933a485` (refactor)
2. **Task 2: ITEM-6 — extract only the rename/suffix ladder from Backup** - `a3017c9` (refactor)
3. **Task 3: ITEM-9 — one FileSystemFault predicate at every per-site exception list** - `ae47e5f` (refactor)

**Plan metadata:** not yet committed — orchestrator owns STATE.md/ROADMAP.md writes after this plan completes.

## Files Created/Modified

- `src/SavegameBunker/Services/FileSystemFault.cs` - new internal predicate, `FileSystemFault.Is(Exception)`
- `src/SavegameBunker/Services/BackupService.cs` - two-mode `DescribeVerificationFailure`, extracted `MoveStagedArchiveIntoPlace`, predicate use at three sites
- `src/SavegameBunker/Services/RestoreService.cs` - calls the no-hashes verification mode with an updated comment
- `src/SavegameBunker/Services/DirectoryWalk.cs` - predicate use at all four catch filters in `Walk`
- `src/SavegameBunker/Services/DriveHints.cs` - predicate use folding two catches into one
- `src/SavegameBunker/Services/ReparseFile.cs` - predicate use at all three groups in `HasAnyFile`
- `tests/SavegameBunker.Tests/BackupServiceTests.cs` - byte-count-growth fact now passes `expectedHashes`; added `System.Security.Cryptography`/`System.Text` usings

## Backup method line counts (ITEM-6, measured with the plan's `awk` span)

| Measurement | Lines |
|---|---|
| Before extraction (Task 2 start) | 161 |
| After extraction (Task 2 end) | 130 |

(The plan's own worked estimate assumed a 141-line starting point at a different HEAD; the live method on `dev/v1.1` at the start of this plan was 161 lines after plans 02-01/02-05/02-07/02-08 had already landed. The after-value, 130, is lower than the before-value, satisfying the acceptance criterion; the exact starting figure was not a gate.)

## Suite totals

- Task 1 filtered run (`BackupServiceTests|RestoreServiceTests|ServiceContractTests`): 104 passed, 0 failed.
- Task 2 filtered run (`BackupServiceTests|RestoreServiceTests`): 101 passed, 0 failed.
- Task 3 full-suite run (`dotnet test SavegameBunker.sln -c Release`): **738 passed, 0 failed, 0 skipped.**
- Release build after every task: 0 Warning(s), 0 Error(s).

## Decisions Made

- Disambiguated the `<see cref="File.Move"/>` XML-doc reference on the new `MoveStagedArchiveIntoPlace` to `File.Move(string, string)` — the bare reference produced a CS0419 ambiguous-overload warning against `File.Move(string, string, bool)`, which would have failed the plan's "0 Warning(s)" verification gate. This is new code the plan introduced (not a pre-existing warning), so it is in scope as a direct fix rather than a deferred item.
- Kept `FileSystemFault` in its own file (`Services/FileSystemFault.cs`) rather than folding it into `PathCompare.cs` — the plan's artifact table names it as its own file with its own `contains` check, and it has no relationship to path comparison.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Disambiguated an XML-doc `cref` ambiguous-overload warning on the new extracted method**
- **Found during:** Task 2 (ITEM-6 — extract only the rename/suffix ladder from Backup)
- **Issue:** The new `MoveStagedArchiveIntoPlace`'s XML doc referenced `<see cref="File.Move"/>` with no parameter list; `File.Move` has two overloads (`(string, string)` and `(string, string, bool)`), so the compiler raised `warning CS0419: Ambiguous reference in cref attribute`. The plan's own verification requires the Release build summary to read "0 Warning(s)".
- **Fix:** Changed the cref to the fully-qualified `<see cref="File.Move(string, string)"/>`, matching the overload the ladder actually calls.
- **Files modified:** src/SavegameBunker/Services/BackupService.cs
- **Verification:** `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` reports 0 Warning(s), 0 Error(s) after the fix.
- **Committed in:** a3017c9 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug).
**Impact on plan:** The fix was required to meet the plan's own 0-warning verification gate on code this plan introduced. No scope creep — no other file or warning was touched.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ITEM-6, ITEM-7 and ITEM-9 are closed with no behaviour change: the full 738-test suite is green, and the rename-cap, same-second-collision, and verification facts all assert their original messages unchanged.
- No blockers. Plans 02-10 through 02-15 remain in the phase backlog (`IDialogService`/`App.xaml` palette rename, test-helper consolidation, and the `MainViewModelTests` split).
- STATE.md and ROADMAP.md are intentionally left untouched by this executor — the orchestrator updates them after this plan completes, per the dispatch instruction.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*

## Self-Check: PASSED

- `src/SavegameBunker/Services/FileSystemFault.cs` — FOUND
- Commit `933a485` (refactor(verify): two verification modes instead of three) — FOUND on HEAD
- Commit `a3017c9` (refactor(backup): extract the rename ladder from Backup) — FOUND on HEAD
- Commit `ae47e5f` (refactor(services): one file-system fault predicate) — FOUND on HEAD
- All task-level acceptance criteria re-verified with the plan's own grep/awk commands and passed.
- Full suite (`dotnet test SavegameBunker.sln -c Release -tl:off`): 738 passed, 0 failed, 0 skipped.
- Release build: 0 Warning(s), 0 Error(s).
