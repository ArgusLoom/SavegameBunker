---
phase: 02-audit-follow-ups
plan: 13
subsystem: testing
tags: [xunit, test-refactor, fixtures]

requires:
  - phase: 02-audit-follow-ups
    provides: "Plan 02-12 — TempDir, TestWait and RecordingDialogService, and the fixture shape to apply"
provides:
  - "TestGames: two shared game-with-saves builders (WithSaveFile, WithNamedSaves) replacing five private copies"
  - "The remaining thirteen service/coordinator/contract/catalog test classes migrated onto TempDir"
  - "Exactly one file in the test project (TempDir.cs) still spells the literal temp-directory path"
affects: ["02-14 (named timeouts)", "02-15 (MainViewModelTests.cs split)"]

actuals:
  tokens: 13293
  tasks: 3
  commits: 3
  plan_head_before: 0ee306c909a0b32aa1d85a73ae904a627e02afb9
  plan_head_after: 4d241c35bf96c0e0cd06efd51b0f3adca51a4177

tech-stack:
  added: []
  patterns:
    - "Shared test fixtures (TempDir) and shared game-with-saves builders (TestGames) instead of per-class copies — ITEM-22 complete"

key-files:
  created:
    - tests/SavegameBunker.Tests/TestGames.cs
  modified:
    - tests/SavegameBunker.Tests/BackupServiceTests.cs
    - tests/SavegameBunker.Tests/RestoreServiceTests.cs
    - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
    - tests/SavegameBunker.Tests/KnownGamesScannerTests.cs
    - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs
    - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
    - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
    - tests/SavegameBunker.Tests/ServiceContractTests.cs
    - tests/SavegameBunker.Tests/ShellOpenTests.cs
    - tests/SavegameBunker.Tests/GameCatalogTests.cs
    - tests/SavegameBunker.Tests/CatalogSessionTests.cs
    - tests/SavegameBunker.Tests/DropFolderTests.cs
    - tests/SavegameBunker.Tests/HelpPathTests.cs

key-decisions:
  - "Two builder names (WithSaveFile, WithNamedSaves) instead of overloads, because both GameWithSaves shapes take three string parameters — an overload would be ambiguous at every call site"
  - "NonAsciiGameWithSaves (BackupServiceTests) and GameWithEmptySaveDir (BackupServiceTests/RestoreServiceTests) left untouched — different signatures/bodies than the GameWithSaves copies this plan targets, out of scope"
  - "PathResolverTests left alone per the plan's explicit instruction — its GetTempPath() call is an input value under test, not a fixture"

requirements-completed: [ITEM-22]

coverage:
  - id: D1
    description: "TestGames created with WithSaveFile and WithNamedSaves, each replacing its matching GameWithSaves copies in the service and coordinator test classes"
    verification:
      - kind: unit
        ref: "dotnet test --filter FullyQualifiedName~BackupServiceTests|...|ShellOpenTests (Tasks 1-2 scope)"
        status: pass
    human_judgment: false
  - id: D2
    description: "All thirteen remaining service/coordinator/contract/catalog test classes migrated onto TempDir; full suite Total and every test name unchanged before and after"
    verification:
      - kind: unit
        ref: "dotnet test SavegameBunker.sln -c Release -tl:off — full suite Total 739, identical before and after"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 13: Shared TestGames builders and TempDir migration for the service/coordinator tests Summary

**ITEM-22 part two: TestGames.WithSaveFile and TestGames.WithNamedSaves replace five copies of GameWithSaves across the service and coordinator test classes, and all thirteen remaining test classes in the project now use TempDir — leaving exactly one file (TempDir.cs) with the literal temp-directory path, with the 739-test suite and every test name unchanged.**

## Performance

- **Duration:** 35 min
- **Tasks:** 3
- **Files modified:** 14 (1 created, 13 modified)

## Accomplishments
- Created `TestGames.WithSaveFile(dir, fileName, content)` and `TestGames.WithNamedSaves(dir, id, displayName)` — the two shared game-with-saves builders, each with the exact body of the copies it replaces.
- Migrated `BackupServiceTests` and `RestoreServiceTests` onto `TempDir` and `TestGames.WithSaveFile`, deleting both private `GameWithSaves` definitions.
- Migrated `SaveStatusScannerTests` and `KnownGamesScannerTests` onto `TempDir` (no `GameWithSaves` in either).
- Migrated `BulkBackupCoordinatorTests`, `RowActionsCoordinatorTests`, `StatusSweepCoordinatorTests` onto `TempDir` and `TestGames.WithNamedSaves`, deleting all three private `GameWithSaves` definitions.
- Migrated `ServiceContractTests` and `ShellOpenTests` onto `TempDir`.
- Migrated `GameCatalogTests`, `CatalogSessionTests`, `DropFolderTests`, `HelpPathTests` onto `TempDir`.
- `PathResolverTests` deliberately left untouched — its `Path.GetTempPath()` call is an input value the resolver is tested against, not a fixture.

## Task Commits

Each task was committed atomically:

1. **Task 1: TestGames end to end — the save-file builder, proven on the backup and restore service tests** - `cb56d64` (test)
2. **Task 2: Coordinator tests onto TestGames.WithNamedSaves; contract and shell tests onto TempDir** - `5ff5361` (test)
3. **Task 3: The last four classes onto TempDir; the whole suite proves nothing was lost** - `4d241c3` (test)

**Plan metadata:** commit_docs is disabled for this project (`.planning/config.json`), so no separate metadata commit was made — this SUMMARY.md, and STATE.md/ROADMAP.md (owned by the orchestrator), are intentionally left out of git history per project configuration.

_Note: this is a pure test refactor — no file under `src/` changed in this plan._

## Files Created/Modified
- `tests/SavegameBunker.Tests/TestGames.cs` - new shared test-game builders (`internal static class TestGames`)
- `tests/SavegameBunker.Tests/BackupServiceTests.cs` - `TempDir` fixture; private `GameWithSaves` deleted; calls rewritten to `TestGames.WithSaveFile(_dir, ...)`
- `tests/SavegameBunker.Tests/RestoreServiceTests.cs` - same migration as BackupServiceTests
- `tests/SavegameBunker.Tests/SaveStatusScannerTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/KnownGamesScannerTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs` - `TempDir` fixture; private `GameWithSaves` deleted; calls rewritten to `TestGames.WithNamedSaves(_dir, ...)`
- `tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs` - same migration as BulkBackupCoordinatorTests
- `tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs` - same migration as BulkBackupCoordinatorTests
- `tests/SavegameBunker.Tests/ServiceContractTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/ShellOpenTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/GameCatalogTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/CatalogSessionTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/DropFolderTests.cs` - `TempDir` fixture only
- `tests/SavegameBunker.Tests/HelpPathTests.cs` - `TempDir` fixture only

## Decisions Made
- Used two distinct builder names rather than C# overloads for `WithSaveFile`/`WithNamedSaves`, since both original `GameWithSaves` shapes take three `string` parameters — an overload resolution would be ambiguous (and misleading) at call sites that pass positional arguments.
- Left `NonAsciiGameWithSaves` (BackupServiceTests) and `GameWithEmptySaveDir` (BackupServiceTests, RestoreServiceTests) untouched — neither matches the `GameWithSaves` signature/body this plan migrates, and the plan's `<files>`/`<action>` for Task 1 scopes only the `GameWithSaves` copies.
- Left `PathResolverTests` untouched per the plan's explicit instruction (Task 3, action step 1).

## Deviations from Plan

None - plan executed exactly as written. One acceptance-criteria grep in Task 1 produces a different count than the plan's literal `EXPECT==0` for a benign reason, noted below under "Acceptance Criteria Notes" rather than as a deviation (no code was changed to chase it).

## Issues Encountered

None.

## Acceptance Criteria Notes

All automated `<verify>` commands and acceptance-criteria greps from the plan were run and recorded below. One grep in Task 1 reports a non-zero count for a reason unrelated to this plan's work:

- `cat BackupServiceTests.cs RestoreServiceTests.cs | grep -c "GameWithSaves("` → **2**, not the plan's expected 0. The two matches are `NonAsciiGameWithSaves(string displayName)` (BackupServiceTests.cs, definition + one call site) — the pattern `GameWithSaves(` is a substring of `NonAsciiGameWithSaves(`, so the plain grep catches it even though `NonAsciiGameWithSaves` is a different helper with a different signature that this plan was never asked to touch (see Decisions Made). No private `GameWithSaves` definition and no bare `GameWithSaves(...)` call remain in either file — confirmed separately by `grep -l "private Game GameWithSaves"` in Task 2's acceptance check returning 0 across the whole project.
- All other acceptance criteria in all three tasks passed exactly as specified:
  - `grep -c "internal static class TestGames" TestGames.cs` → 1
  - `grep -c "TestGames.WithSaveFile(_dir" (Backup+Restore)` → 46 (≥45)
  - `"SavegameBunkerTests"` literal count across the four Task 1 files → 0
  - `grep -l "private Game GameWithSaves" *.cs | wc -l` → 0
  - `grep -c "TestGames.WithNamedSaves(_dir" (three coordinator files)` → 24 (≥20)
  - `"SavegameBunkerTests"` literal count across the five Task 2 files → 0
  - `grep -l "\"SavegameBunkerTests\"" *.cs | wc -l` → 1 (`TempDir.cs` only)

## Baseline and Verification Record

- **Full-suite Total before this plan:** 739 (via `dotnet test tests/SavegameBunker.Tests -c Release -tl:off`, recorded before any edit).
- **Full-suite Total after this plan:** 739 (via `dotnet test SavegameBunker.sln -c Release -tl:off`) — identical.
- **Release build:** 0 Warning(s), 0 Error(s), confirmed after each of the three tasks.
- **Test-name diff:** sorted `grep -ohE "public (async Task|void) [A-Za-z0-9_]+\(" tests/SavegameBunker.Tests/*.cs | sort` captured before Task 1 (678 lines) and after Task 3 (678 lines) — `diff` between the two files is empty. No test was added, removed, or renamed by this plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ITEM-22 is now complete: one `TempDir`, one `TestWait` (Plan 02-12), two `TestGames` builders (this plan), `RecordingDialogService` in its own file (Plan 02-12).
- Plan 02-14 (named timeouts) and 02-15 (MainViewModelTests.cs split) can proceed; neither depends on further changes from this plan.
- No blockers.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*

## Self-Check: PASSED

All created/modified files found on disk; all three task commits (`cb56d64`, `5ff5361`, `4d241c3`) confirmed as ancestors of HEAD.
