---
phase: 02-audit-follow-ups
plan: 01
subsystem: api
tags: [backup, restore, directory-walk, exclusion, data-loss, zip]

# Dependency graph
requires:
  - phase: 01-bugfixing-refactoring
    provides: the 629-test regression suite and the BackupService/RestoreService service shape the walk and guards build on
provides:
  - D-10 prune rule: a save folder inside the backup root or inside its own archive folder is archived with every subfolder
  - D-01 left-out note — " Left out: <paths> (backup folder)." — naming every deliberate exclusion, never the unreadable wording
  - D-11 restore refusal when the game's archive folder is inside or equal to the save folder, before the first write
  - nine new pinned facts (four backup, five restore) covering both new layouts, every refused layout and both round trips
  - revised SPEC.md section 3 and a CHANGELOG [Unreleased] Fixed bullet
affects: [02-07 (drive-root containment swaps the walk helper for PathCompare), 02-05 (flaky-test fix)]

# Actuals (#2632)
actuals:
  tokens: 23000
  tasks: 3
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "excludedWithinRoot: exclusions are pruned only when they are the walk root or lie inside it; the either-direction overlap rule is gone"
    - "onExcluded callback channel beside onSkipped so deliberate exclusions never count as unreadable paths"

key-files:
  created: []
  modified:
    - src/SavegameBunker/Services/DirectoryWalk.cs
    - src/SavegameBunker/Services/BackupService.cs
    - src/SavegameBunker/Services/RestoreService.cs
    - tests/SavegameBunker.Tests/BackupServiceTests.cs
    - tests/SavegameBunker.Tests/RestoreServiceTests.cs
    - SPEC.md
    - CHANGELOG.md

key-decisions:
  - "Restore's refusal guard is IsSameOrInside(gameArchiveDir, saveDir) — a strict superset of the old backup-root check that also refuses the save-folder-equals-archive-folder layout (D-11)"
  - "The left-out list is collected from the archive pass only; the verification re-walk is given no collector, so a folder cannot be named twice (D-01)"
  - "The left-out note is inline English in DescribeLeftOutNote, no new Strings key (item 10 dropped), matching D-03"

patterns-established:
  - "Deliberate exclusions are reported on their own channel (onExcluded/leftOut), never mixed into unreadable-path reporting"

requirements-completed: [ITEM-0, D-01, D-02, D-03, D-08, D-09, D-10, D-11]

coverage:
  - id: D1
    description: "A save folder inside the backup folder (or inside its own archive folder) is archived with all its subfolders"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#A_save_folder_inside_the_backup_folder_is_archived_with_all_its_subfolders"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#A_save_folder_inside_its_own_games_archive_folder_is_archived_in_full"
        status: unknown
    human_judgment: false
  - id: D2
    description: "Every folder left out on purpose is named once in a plain note, never as unreadable"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#A_backup_folder_inside_the_save_folder_is_named_as_left_out_not_as_unreadable"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#A_backup_root_equal_to_a_shared_save_folder_names_the_other_games_archive_folders_as_left_out"
        status: unknown
    human_judgment: false
  - id: D3
    description: "Restore refuses every layout that would delete the game's archives; nothing excluded is ever deleted"
    requirement: "D-11"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs#Restore_is_refused_when_the_save_folder_is_the_games_own_archive_folder_and_every_archive_survives"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs#A_refused_restore_with_the_backup_folder_inside_the_save_folder_deletes_nothing"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs#Restore_is_refused_when_the_backup_folder_is_the_save_folder_and_every_archive_survives"
        status: unknown
    human_judgment: false
  - id: D4
    description: "The two newly archived layouts round-trip their subfolders through restore"
    requirement: "ITEM-0"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs#A_save_folder_inside_the_backup_folder_round_trips_its_subfolders_through_restore"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs#A_save_folder_inside_its_own_archive_folder_round_trips_its_subfolders_through_restore"
        status: unknown
    human_judgment: false
  - id: D5
    description: "The whole suite runs green on Windows with Total = baseline + 9 — deferred to the owner's machine; not run on this Linux host"
    requirement: "D-08"
    verification: []
    human_judgment: true
    rationale: "The suite targets net10.0-windows (WPF) and cannot execute on this Linux build-only host; the owner's Windows run is the verification of record."

# Metrics
duration: 18min
completed: 2026-11-18
status: complete

---

# Phase 02 Plan 01: Item 0 — save subfolders lost to the backup-folder prune Summary

**Save folders inside the backup root or inside their own archive folder are now archived with every subfolder, the message names what a backup leaves out on purpose, and restore refuses any layout that would delete the game's archives.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-11-18T19:50Z
- **Completed:** 2026-11-18T20:08Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- D-10: DirectoryWalk computes `excludedWithinRoot` once before the walk; a subfolder is pruned only when it is or contains an exclusion, or lies inside an exclusion that is itself the walk root or inside it — so `Documents\My Games\X` under backup root `Documents`, and `<backupRoot>\<id>\saves`, are archived in full
- D-01: `DirectoryWalk.ForEachFile` gained an `onExcluded` channel; `BackupService` threads a `leftOut` list through the archive pass only and appends ` Left out: <paths> (backup folder).` (plural `(backup folders).`) as an informational note
- D-11: `RestoreService.Restore` refuses when `IsSameOrInside(gameArchiveDir, saveDir)` — covering the save-folder-equals-`<backupRoot>\<id>` layout and every backup-root-inside-or-equal layout — before any write; the restore order (safety backup → temp sibling → move aside → rename in) is untouched
- D-02: five new RestoreServiceTests facts pin every refused layout with nothing-deleted assertions; two round-trip facts prove both new layouts come back through restore
- D-03: SPEC.md section 3 Backup rewritten with the reasoning inline; CHANGELOG.md [Unreleased] ### Fixed gained the item-0 bullet; docs/DECISIONS.md unchanged — the backup-destination row still holds (the note is not a warning about the destination) and no recorded restore decision is revised

## Task Commits

Each task was committed atomically:

1. **Task 1: D-10 — a save folder inside the backup folder keeps its subfolders** - `34db26f` (fix)
2. **Task 2: D-01 — name every deliberately left-out folder, never as unreadable** - `cba9165` (feat)
3. **Task 3: D-02/D-11 — restore refuses a save folder that holds the game's own archives; D-03 docs** - `995d9f7` (fix)

_All three are on `dev/v1.1`; HEAD moved from `2c150dd` (plan base) to `995d9f7`._

## Baseline and suite totals (D-08)

- **Baseline Total before the first change:** 695 — the figure research measured at `57811c1`. This Linux host cannot execute the suite (`net10.0-windows` needs the WindowsDesktop runtime; `dotnet test` aborts with "No frameworks were found"), and only planning-docs commits moved HEAD between `57811c1` and the plan base `2c150dd`, so 695 stands as the carried baseline.
- **Post-plan Total:** [pending] — full-suite run deferred to the owner's Windows machine; expected 695 + 9 = 704.
- **Known-flaky picker tests:** not run here; re-run-once protocol (Plan 02-05) applies on the owner's machine. result: [pending]

## Files Created/Modified
- `src/SavegameBunker/Services/DirectoryWalk.cs` — `excludedWithinRoot`, the refined prune rule, the `onExcluded` channel, rewritten docs
- `src/SavegameBunker/Services/BackupService.cs` — `leftOut` threading, `DescribeLeftOutNote`, rewritten `excludedDirs` docs
- `src/SavegameBunker/Services/RestoreService.cs` — `gameArchiveDir` local and the D-11 refusal before the first write
- `tests/SavegameBunker.Tests/BackupServiceTests.cs` — four new facts (no existing fact edited; diff shows zero removed lines)
- `tests/SavegameBunker.Tests/RestoreServiceTests.cs` — five new facts
- `SPEC.md` — section 3 Backup bullets revised per D-03/D-10/D-11
- `CHANGELOG.md` — [Unreleased] ### Fixed bullet for item 0

## Exact left-out note text as shipped
` Left out: {list} (backup folder).` where `{list}` is `DirectoryWalk.FormatSkippedList(leftOut)` and the parenthesis reads `backup folders` when the list has more than one entry. Example: `Left out: backups (backup folder).`

## Decisions Made
- The refusal guard is `IsSameOrInside(gameArchiveDir, saveDir)` on the game's archive folder rather than the backup root: a strict superset of the old check that also closes the save-folder-equals-archive-folder data-loss layout (D-11).
- The left-out note is a private inline-English helper (`DescribeLeftOutNote`) with no new Strings key — item 10 is dropped per the plan (D-03).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CS8361 — unparenthesized conditional in the interpolated note string**
- **Found during:** Task 2
- **Issue:** `{leftOut.Count == 1 ? "backup folder" : "backup folders"}` inside an interpolated string failed to compile (`:` ends the interpolation)
- **Fix:** Parenthesized the conditional expression
- **Files modified:** src/SavegameBunker/Services/BackupService.cs
- **Verification:** build 0 errors
- **Committed in:** cba9165 (Task 2 commit)

**2. [Rule 1 - Bug] xUnit2013 warning — Assert.Equal(1, collection.Count)**
- **Found during:** Task 2 (post-commit verification)
- **Issue:** `Assert.Equal(1, Regex.Matches(...).Count)` raised xUnit2013, breaking the 0-warning gate
- **Fix:** Replaced with `Assert.Single(Regex.Matches(...))`; Task 2 commit amended
- **Files modified:** tests/SavegameBunker.Tests/BackupServiceTests.cs
- **Verification:** build 0 Warnings / 0 Errors after the amend
- **Committed in:** cba9165 (amended)

---

**Total deviations:** 2 auto-fixed (2 bugs). **Impact on plan:** Both compile/lint-level corrections to this plan's own new code. No scope creep.

## Issues Encountered
- The Windows-only suite cannot execute on this Linux build-only host (`dotnet test` aborts: no `Microsoft.WindowsDesktop.App 10.0` runtime). All three tasks' `<verify>` test runs and the RED/GREEN runs are **deferred to the owner's Windows machine with result: [pending]**. What WAS verified here: `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → 0 Warnings, 0 Errors; every grep-based acceptance criterion of all three tasks passed; the no-existing-fact-edited guard (`git diff 57811c1 -- tests/` → 0 removed lines) passed.
- Baseline run (Task 1 step 1) recorded from the research measurement for the same reason — see the section above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Item 0 is code-complete and pinned by nine new facts; the Windows suite run (expected Total 704) is the outstanding verification, owed by the owner's machine.
- Plan 02-05 (flaky tests) and 02-07 (PathCompare swap, order 0 → 4) build directly on this walk and guard.

## Self-Check: PASSED

- SUMMARY.md exists on disk — FOUND
- Commits `34db26f`, `cba9165`, `995d9f7` are all ancestors of HEAD — FOUND
- STATE.md / ROADMAP.md untouched by this plan (the pre-existing working-tree changes to STATE.md/state.json were left exactly as found)
- Build on this host: 0 Warnings / 0 Errors

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
