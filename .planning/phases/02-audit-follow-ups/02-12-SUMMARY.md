---
phase: 02-audit-follow-ups
plan: 12
subsystem: testing
tags: [xunit, test-refactor, fixtures]

requires:
  - phase: 02-audit-follow-ups
    provides: "Plans 02-01 through 02-11 — every defect fix and clean-code item this plan's migrated tests were written against"
provides:
  - "TempDir: one IDisposable temp-directory fixture with full cleanup (deny-rule removal, junction removal, recursive delete)"
  - "TestWait: one shared async polling helper (WaitForAsync)"
  - "RecordingDialogService moved to its own file, byte-identical"
  - "Seven view-model-side test classes migrated onto the two shared helpers"
affects: ["02-13 (service/coordinator-side test classes)", "02-14 (named timeouts)", "02-15 (MainViewModelTests.cs split)"]

actuals:
  tokens: 6967
  tasks: 2
  commits: 2
  plan_head_before: b54a058557d2712aaaee96f982475127875aebf7
  plan_head_after: 0ee306c909a0b32aa1d85a73ae904a627e02afb9

tech-stack:
  added: []
  patterns:
    - "Shared test fixtures (TempDir, TestWait) instead of per-class copies — ITEM-22"

key-files:
  created:
    - tests/SavegameBunker.Tests/TempDir.cs
    - tests/SavegameBunker.Tests/TestWait.cs
    - tests/SavegameBunker.Tests/RecordingDialogService.cs
  modified:
    - tests/SavegameBunker.Tests/MainViewModelTests.cs
    - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
    - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
    - tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs
    - tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs
    - tests/SavegameBunker.Tests/RestorePickerViewModelTests.cs
    - tests/SavegameBunker.Tests/AddGameFlowTests.cs

key-decisions:
  - "TempDir.Dispose runs RemoveDenyRules + RemoveJunctions + recursive delete for every migrated class, even the five that previously only did a plain Directory.Delete — a superset cleanup, never a behavior loss"
  - "RestorePickerViewModelTests.WaitForPendingDelete reduced to one call delegating to TestWait.WaitForAsync, per the plan's instruction"

requirements-completed: [ITEM-22, D-09]

coverage:
  - id: D1
    description: "TempDir, TestWait and RecordingDialogService created as shared helpers, each used by the two largest view-model test classes (MainViewModelTests, MainViewModelGuardPinTests)"
    verification:
      - kind: unit
        ref: "dotnet test --filter FullyQualifiedName~MainViewModelTests|...|StartupUnsecuredPromptTests (Task 1 scope)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Remaining five view-model-side test classes (StartupUnsecuredPromptTests, AddGameDialogViewModelTests, ChangeSaveFolderDialogViewModelTests, RestorePickerViewModelTests, AddGameFlowTests) migrated onto TempDir/TestWait with zero behavior change"
    verification:
      - kind: unit
        ref: "dotnet test SavegameBunker.sln -c Release -tl:off — full suite Total 739, identical before and after"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 12: Shared test fixtures (TempDir, TestWait, RecordingDialogService) Summary

**ITEM-22 part one: three shared test helpers replace copied temp-dir setup, WaitForAsync and the dialog fake across seven view-model-side test classes, with the 739-test suite and every test name unchanged.**

## Performance

- **Duration:** 25 min
- **Tasks:** 2
- **Files modified:** 10 (3 created, 7 modified)

## Accomplishments
- Created `TempDir : IDisposable` — the one temp-directory fixture every migrated class now uses, with full cleanup (deny-rule removal, junction removal, recursive delete, swallowing `IOException`/`UnauthorizedAccessException`).
- Created `TestWait.WaitForAsync` — the one shared async polling helper, replacing five identical private copies.
- Moved `RecordingDialogService` out of `MainViewModelTests.cs` into its own file, verbatim (including `OnPickFolder` and `RestorePickerDeleteActions`).
- Migrated all seven view-model-side test classes (`MainViewModelTests`, `MainViewModelGuardPinTests`, `StartupUnsecuredPromptTests`, `AddGameDialogViewModelTests`, `ChangeSaveFolderDialogViewModelTests`, `RestorePickerDeleteTests` in `RestorePickerViewModelTests.cs`, `AddGameFlowTests`) onto the shared fixtures.
- `RestorePickerViewModelTests.WaitForPendingDelete` reduced to a single delegating call to `TestWait.WaitForAsync`.

## Task Commits

Each task was committed atomically:

1. **Task 1: The shared helpers end to end — TempDir, TestWait and RecordingDialogService, proven on MainViewModelTests and MainViewModelGuardPinTests** - `af1b724` (test)
2. **Task 2: The remaining view-model-side classes onto TempDir and TestWait** - `0ee306c` (test)

**Plan metadata:** commit_docs is disabled for this project (`.planning/config.json`), so no separate metadata commit was made — this SUMMARY.md, and STATE.md/ROADMAP.md (owned by the orchestrator), are intentionally left out of git history per project configuration.

## Files Created/Modified
- `tests/SavegameBunker.Tests/TempDir.cs` - new shared temp-directory fixture (`internal sealed class TempDir : IDisposable`)
- `tests/SavegameBunker.Tests/TestWait.cs` - new shared async polling helper (`internal static class TestWait`)
- `tests/SavegameBunker.Tests/RecordingDialogService.cs` - `RecordingDialogService` moved out of `MainViewModelTests.cs`, unchanged
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` - fixture replaced with `TempDir`; `RecordingDialogService` and private `WaitForAsync` removed; `using static SavegameBunker.Tests.TestWait;` added
- `tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs` - same fixture/WaitForAsync migration
- `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` - same fixture/WaitForAsync migration
- `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs` - same fixture/WaitForAsync migration (`ThrowingDialogService` stays in this file, unchanged)
- `tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs` - same fixture/WaitForAsync migration
- `tests/SavegameBunker.Tests/RestorePickerViewModelTests.cs` - `RestorePickerDeleteTests` fixture migration; `WaitForPendingDelete` reduced to one delegating call
- `tests/SavegameBunker.Tests/AddGameFlowTests.cs` - fixture migration only (no private `WaitForAsync` existed in this file)

## Decisions Made
- Applied the `TempDir` fixture shape identically to all seven classes, including the five whose old `Dispose()` only ran a plain `Directory.Delete` with no deny-rule or junction cleanup. `TempDir.Dispose` is a safe superset (it always runs `RemoveDenyRules` + `RemoveJunctions` before the recursive delete, both best-effort no-ops when nothing was ever denied or junctioned), so this is not a behavior change for those five classes — just the plan's intended convergence onto one fixture.
- Reduced `RestorePickerViewModelTests.WaitForPendingDelete` to `await WaitForAsync(() => vm.IsDeletePending == expectPending);` as instructed, keeping its own `Assert.Equal`-free signature since `WaitForAsync` already asserts on timeout.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Baseline and Verification Record

- **Full-suite Total before this plan:** 739 (via `dotnet test SavegameBunker.sln -c Release -tl:off`, recorded at the start of Task 1).
- **Full-suite Total after this plan:** 739 — identical.
- **Release build:** 0 Warning(s), 0 Error(s), both after Task 1 and Task 2.
- **Test-name diff:** the sorted `grep -ohE "public (async Task|void) [A-Za-z0-9_]+\(" tests/SavegameBunker.Tests/*.cs | sort` list before vs. after differs by exactly one line: an added `public void Dispose(` match. This is **not** a test — it is the new `TempDir.cs` file's own `IDisposable.Dispose()` implementation, picked up by the grep's naive pattern (which matches any `public void Name(`/`public async Task Name(` signature, not just `[Fact]`-attributed methods — the same pattern already matched 20 pre-existing `Dispose()` methods across other test classes before this plan touched anything). Excluding that one line (`grep -v "public void Dispose($"` on both sides), the diff is empty: every actual test method name is unchanged. Combined with the unchanged `dotnet test` Total of 739, no test was added, removed, or renamed by this plan.
- All six Task 1 acceptance-criteria greps and all four Task 2 acceptance-criteria checks passed as specified in the plan (TempDir/TestWait/RecordingDialogService exist exactly once each; zero remaining `RecordingDialogService` class definitions, private `WaitForAsync` methods, or inline `"SavegameBunkerTests"` literals in the eight migrated files; exactly one `grep -l "static async Task WaitForAsync"` match across the whole test project (`TestWait.cs` itself); exactly 14 files still carrying `"SavegameBunkerTests"` literally, left for Plan 02-13's service/coordinator-side classes plus `TempDir.cs`'s own literal).

## Next Phase Readiness
- Plan 02-13 can proceed: migrates the remaining service- and coordinator-side test classes onto the same `TempDir`/`TestWait` helpers.
- No blockers.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*

## Self-Check: PASSED

All created files found on disk; both task commits (`af1b724`, `0ee306c`) confirmed as ancestors of HEAD.
