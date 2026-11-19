---
phase: 02-audit-follow-ups
plan: 07
subsystem: api
tags: [paths, containment, drive-roots, backup, empty-archive]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: "Plan 02-01's D-10 prune rule and onExcluded channel in DirectoryWalk/BackupService, which this plan's PathCompare swap builds on (order 0 -> 4 -> 5)"
provides:
  - "internal PathCompare.Normalize/IsSameOrInside — the one containment check for DirectoryWalk, RestoreService and PathResolver.ToTemplate, fixing the drive-root bug where a normalized root (E:\\) got a second trailing separator appended and so never contained anything"
  - "PathResolver.ToTemplate keeps a drive root a root (E:\\ -> E:/) instead of trimming it to E: (the current directory on that drive)"
  - "BackupService.Backup fails when the archive pass collected zero entries and skipped nothing (ITEM-5), instead of reporting a zero-entry archive as a successful backup"
affects: [02-08, 02-09]

# Actuals (#2632)
actuals:
  tokens: 4745
  tasks: 3
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "One internal PathCompare class is the sole containment/normalization check across Services (DirectoryWalk, RestoreService, PathResolver, BackupService); no caller re-implements path comparison"

key-files:
  created:
    - src/SavegameBunker/Services/PathCompare.cs
    - tests/SavegameBunker.Tests/PathCompareTests.cs
  modified:
    - src/SavegameBunker/Services/DirectoryWalk.cs
    - src/SavegameBunker/Services/RestoreService.cs
    - src/SavegameBunker/Services/BackupService.cs
    - src/SavegameBunker/Services/PathResolver.cs
    - tests/SavegameBunker.Tests/PathResolverTests.cs
    - tests/SavegameBunker.Tests/BackupServiceTests.cs
    - SPEC.md
    - CHANGELOG.md

key-decisions:
  - "PathCompare.Normalize keeps a drive/UNC root's trailing separator intact (so IsSameOrInside never appends a second one) and trims every other path's trailing separator, unifying both spellings of a UNC share root (\\\\server\\share and \\\\server\\share\\) to the same normalized value"
  - "PathResolver.ToTemplate was rewired onto PathCompare.Normalize/IsSameOrInside rather than keeping its own TrimEnd pair, so the plan's own prose verification grep (IsSameOrInside confined to PathCompare/DirectoryWalk/RestoreService) undercounts PathResolver.cs as a caller — Task 2's action and acceptance criteria require that call site explicitly"
  - "ITEM-5's empty-archive failure is gated on entryHashes.Count == 0 && skippedPaths.Count == 0, so a locked-only save folder (zero entries, one skipped path) stays the warning-carrying success BackupServiceTests already pins"

requirements-completed: [ITEM-4, ITEM-5]

coverage:
  - id: D1
    description: "One internal PathCompare comparator replaces three diverging copies; drive roots, UNC shares, prefix lookalikes, trailing separators and case all compare correctly"
    requirement: "ITEM-4"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/PathCompareTests.cs#IsSameOrInside_holds_for_a_folder_and_everything_below_it"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/PathCompareTests.cs#IsSameOrInside_rejects_siblings_parents_and_prefix_lookalikes"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/PathCompareTests.cs#Normalize_keeps_a_drive_root_and_trims_any_other_trailing_separator"
        status: pass
    human_judgment: false
  - id: D2
    description: "DirectoryWalk and RestoreService's containment checks go through PathCompare exclusively; DirectoryWalk no longer depends on BackupService for this"
    requirement: "ITEM-4"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs (full class, 104-test filtered run)"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/RestoreServiceTests.cs (full class, 104-test filtered run)"
        status: pass
    human_judgment: false
  - id: D3
    description: "PathResolver.ToTemplate keeps a drive root a root (E:\\ -> E:/) and round-trips through TryResolve; every pre-existing ToTemplate fact stays green"
    requirement: "ITEM-4"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/PathResolverTests.cs#ToTemplate_keeps_a_drive_root_a_root"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/PathResolverTests.cs (full class, 73-test filtered run incl. AddGameDialogViewModelTests/ChangeSaveFolderDialogViewModelTests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "A save folder whose only content is the backup folder fails the backup and writes no archive; a locked-only save folder and a truly empty save folder behave exactly as before"
    requirement: "ITEM-5"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#A_save_folder_holding_only_the_backup_folder_fails_and_writes_no_archive"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BackupServiceTests.cs#Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path"
        status: pass
    human_judgment: false
  - id: D5
    description: "Full suite (736 tests) runs green on Windows after all three tasks"
    requirement: null
    verification:
      - kind: unit
        ref: "dotnet test SavegameBunker.sln -c Release -tl:off"
        status: pass
    human_judgment: false

# Metrics
duration: 42min
completed: 2026-11-18
status: complete

---

# Phase 02 Plan 07: ITEM-4/ITEM-5 — one path comparator and no more empty-archive backups Summary

**A single internal `PathCompare` comparator (fixing the drive-root containment bug) now backs `DirectoryWalk`, `RestoreService` and `PathResolver.ToTemplate`, and `BackupService.Backup` refuses to write an archive that collected nothing.**

## Performance

- **Duration:** 42 min
- **Started:** 2026-11-18T20:10Z (approx.)
- **Completed:** 2026-11-18T20:52Z (approx.)
- **Tasks:** 3
- **Files modified:** 10 (2 created, 8 modified)

## Accomplishments
- ITEM-4: `PathCompare.Normalize`/`IsSameOrInside` is now the sole containment check in `DirectoryWalk` (both exclusion sites), `RestoreService` (the `gameArchiveDir` guard and the archive-containment check) and `PathResolver.ToTemplate`; `BackupService.NormalizeDir`/`IsSameOrInside` are deleted, with `excludedDirs` built from `PathCompare.Normalize` instead
- ITEM-4: `PathResolver.ToTemplate` keeps a drive root a root (`E:\` → `E:/`, round-tripping back to `E:\` through `TryResolve`) instead of trimming it to `E:` (the current directory on that drive) — every pre-existing `ToTemplate` fact stays green
- ITEM-5: `BackupService.Backup` fails with "holds nothing but the backup folder, so there is nothing to back up" when the archive pass collected zero entries and skipped nothing — gated so a locked-only save folder (zero entries, one skipped path) is still the warning-carrying success it was; `"Save folder is empty: ..."` and `InspectSaveDir` are untouched
- `PathCompareTests.cs` pins drive roots, UNC shares, prefix lookalikes (`E:\x` vs `E:\xy`), trailing separators and case, all as pure string comparisons
- `SPEC.md` §3 Backup and `CHANGELOG.md [Unreleased]` record both the drive-root fix and the empty-archive refusal

## Task Commits

Each task was committed atomically:

1. **Task 1: PathCompare end to end — one comparator, wired into the walk and the restore guard** - `f6d7613` (refactor)
2. **Task 2: PathResolver.ToTemplate keeps a drive root a root** - `8b7bc70` (fix)
3. **Task 3: ITEM-5 — a save folder holding only the backup folder fails instead of producing an empty archive** - `a3e858c` (fix)

_All three are on `dev/v1.1`; HEAD moved from `83f711a` (plan base) to `a3e858c`._

## Files Created/Modified
- `src/SavegameBunker/Services/PathCompare.cs` — new internal `Normalize`/`IsSameOrInside`, the one containment check
- `tests/SavegameBunker.Tests/PathCompareTests.cs` — new, pure-string theories and facts for the behavior matrix
- `src/SavegameBunker/Services/DirectoryWalk.cs` — both exclusion call sites switched to `PathCompare.IsSameOrInside`
- `src/SavegameBunker/Services/RestoreService.cs` — the `gameArchiveDir` guard and archive-containment check switched to `PathCompare.IsSameOrInside`
- `src/SavegameBunker/Services/BackupService.cs` — `NormalizeDir`/`IsSameOrInside` deleted; `excludedDirs` built with `PathCompare.Normalize`; ITEM-5 zero-entry failure gate added before `DescribeVerificationFailure`
- `src/SavegameBunker/Services/PathResolver.cs` — `ToTemplate` rewritten onto `PathCompare.Normalize`/`IsSameOrInside`, with an explicit remainder-start index instead of a blind `+ 1`
- `tests/SavegameBunker.Tests/PathResolverTests.cs` — `ToTemplate_keeps_a_drive_root_a_root`
- `tests/SavegameBunker.Tests/BackupServiceTests.cs` — `A_save_folder_holding_only_the_backup_folder_fails_and_writes_no_archive`
- `SPEC.md` — §3 Backup, first bullet, gains the "only content is the backup folder" sentence
- `CHANGELOG.md` — `[Unreleased] ### Fixed` gains the drive-root bullet and the empty-archive bullet

## Suite totals

- **Baseline carried from Plan 02-01:** 695 (pre-phase), with Plan 02-01 expecting 704 and subsequent plans (02-02..02-06) adding further facts.
- **Before this plan's first task (HEAD `83f711a`):** suite not re-measured here; this plan starts from whatever 02-06 left.
- **After all three tasks (HEAD `a3e858c`):** **736 passed, 0 failed, 0 skipped** — `dotnet test SavegameBunker.sln -c Release -tl:off`.
- **Release build:** 0 Warnings, 0 Errors throughout (`dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental`, re-run after each task).

## Decisions Made
- `PathCompare.Normalize` treats a path as a kept root only when its full form already ends in a separator AND its length equals `Path.GetPathRoot`'s length — this correctly unifies `\\server\share` and `\\server\share\` to the same normalized UNC root (verified with a scratch probe of `Path.GetFullPath`/`Path.GetPathRoot` on this machine) without a special UNC branch.
- `PathResolver.ToTemplate`'s remainder-start index is computed explicitly (`best.Value.Length` when the matched root already ends in a separator, else `+ 1`) rather than reusing the old `+ 1` literal, so a drive-root match never off-by-ones the remainder.
- ITEM-5's failure message names the save directory and reuses the existing `TryDelete(tempPath)` cleanup discipline every other `Backup` failure path already follows.

## Deviations from Plan

None — plan executed exactly as written. One documentation-only observation, not a deviation requiring any code change:

**Plan-level `<verification>` grep text is narrower than Task 2's own requirement.** The plan's verification section states `grep -rn "IsSameOrInside" src/SavegameBunker --include=*.cs` should list only `PathCompare.cs`, `DirectoryWalk.cs` and `RestoreService.cs`. Task 2's own action and acceptance criteria (`grep -c "PathCompare\." src/SavegameBunker/Services/PathResolver.cs` must print at least 3) require `PathResolver.ToTemplate` to call `PathCompare.IsSameOrInside` as well, so `PathResolver.cs` necessarily appears in that grep too — confirmed: `src/SavegameBunker/Services/PathResolver.cs:157: .Where(c => PathCompare.IsSameOrInside(target, c.Value))`. This is required by the plan's own Task 2 instructions, not a code defect; the prose list in `<verification>` is simply stale relative to Task 2. No fix needed — Task 2's acceptance criteria are the authoritative, machine-checked contract and both passed.

---

**Total deviations:** 0 auto-fixed. **Impact on plan:** None — the one noted inconsistency is in the plan's own prose verification text, not in the shipped code, and does not affect correctness or test coverage.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ITEM-4 and ITEM-5 are closed: one comparator handles drive roots everywhere, and `ToTemplate` is drive-root-safe; no empty archive can ever be reported as a backup.
- Full suite green at 736 tests; Release build 0/0.
- Plan 02-08 is next (ViewModels: `RequeryOperationGates()` / bulk-progress callback), unaffected by this plan's Services-only changes.

## Self-Check: PASSED

- `src/SavegameBunker/Services/PathCompare.cs` exists on disk — FOUND
- `tests/SavegameBunker.Tests/PathCompareTests.cs` exists on disk — FOUND
- Commits `f6d7613`, `8b7bc70`, `a3e858c` are all ancestors of HEAD — FOUND
- `commits: 3` matches `git rev-list --count 83f711a70613409f1de7e5b62695284dea9a0677..HEAD` — FOUND (3)
- Full suite: 736 passed, 0 failed, 0 skipped
- Release build: 0 Warnings, 0 Errors

plan_head_before: 83f711a70613409f1de7e5b62695284dea9a0677
plan_head_after: a3e858c9f1f83bdb7dee9f5d5711c23935539cef

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
