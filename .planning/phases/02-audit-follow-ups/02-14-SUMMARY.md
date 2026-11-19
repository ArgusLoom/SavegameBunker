---
phase: 02-audit-follow-ups
plan: 14
subsystem: testing
tags: [xunit, test-timeouts, flake-check]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: "TestWait.WaitForAsync shared helper (Plans 02-12/02-13)"
provides:
  - "Three named wait-budget constants in TestWait (DefaultTimeoutMs, RealIoTimeoutMs, NegativeProofDelayMs)"
  - "Every literal timeout/delay call site in the view-model test files renamed to the matching constant"
  - "The one unasserted sweep poll in MainViewModelTests now fails loudly on timeout instead of continuing silently"
affects: [testing]

# Actuals (#2632)
actuals:
  tokens: 3856
  tasks: 2
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns: ["named timeout constants instead of magic numbers in async test waits"]

key-files:
  created: []
  modified:
    - tests/SavegameBunker.Tests/TestWait.cs
    - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
    - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
    - tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs
    - tests/SavegameBunker.Tests/MainViewModelTests.cs

key-decisions:
  - "The 50ms negative-proof wait in StartupUnsecuredPromptTests grows to NegativeProofDelayMs (300ms) per plan instruction — a longer negative proof is only stricter, never a behavior change"
  - "The synchronous sweep-poll deadline moved from a bare AddSeconds(3) to AddMilliseconds(DefaultTimeoutMs), keeping the same 3000ms budget but naming it; a new Assert.DoesNotContain immediately after the loop turns a silent timeout into a failing assertion"

patterns-established:
  - "Pattern: async/sync test waits use named TestWait constants (DefaultTimeoutMs, RealIoTimeoutMs, NegativeProofDelayMs) rather than bare literals; the only literal left in the suite lives in TestWait.cs itself"

requirements-completed: [ITEM-24]

coverage:
  - id: D1
    description: "TestWait carries three named constants (DefaultTimeoutMs=3000, RealIoTimeoutMs=15000, NegativeProofDelayMs=300); WaitForAsync's default uses DefaultTimeoutMs"
    requirement: "ITEM-24"
    verification:
      - kind: other
        ref: "grep -c -E \"const int (DefaultTimeoutMs|RealIoTimeoutMs|NegativeProofDelayMs)\" tests/SavegameBunker.Tests/TestWait.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every timeoutMs: 15000 call site (19 total: 3 in MainViewModelGuardPinTests, 16 in MainViewModelTests) renamed to RealIoTimeoutMs; every Task.Delay(300)/Task.Delay(50) negative proof renamed to NegativeProofDelayMs"
    requirement: "ITEM-24"
    verification:
      - kind: other
        ref: "cat tests/SavegameBunker.Tests/*.cs | grep -c 'timeoutMs: 15000' -> 0"
        status: pass
      - kind: other
        ref: "grep -l -E '\\b(15000|3000)\\b' tests/SavegameBunker.Tests/*.cs -> only TestWait.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "The synchronous sweep poll in MainViewModelTests now asserts no row is still Checking after the loop, so a timed-out poll fails instead of silently continuing"
    requirement: "ITEM-24"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#A_row_hidden_by_the_active_filter_is_still_counted_by_the_existing_safety_gates"
        status: pass
    human_judgment: false
  - id: D4
    description: "Kept fixed waits (Thread.Sleep(1100) x2 in RestoreServiceTests, 20ms NTFS-timestamp waits in CatalogSessionTests/GameCatalogTests/SaveStatusScannerTests) left untouched"
    requirement: "ITEM-24"
    verification:
      - kind: other
        ref: "grep -c 'Thread.Sleep(1100)' tests/SavegameBunker.Tests/RestoreServiceTests.cs -> 2"
        status: pass
    human_judgment: false
  - id: D5
    description: "Full suite passes five consecutive times after this plan; Total (739) equals the Total before the plan"
    requirement: "ITEM-24"
    verification:
      - kind: integration
        ref: "dotnet test SavegameBunker.sln -c Release --no-build (5 consecutive runs, all 739/739 passed)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-11-18
status: complete
---

# Phase 02 Plan 14: Named Wait Budgets Summary

**TestWait gains three named timeout constants (DefaultTimeoutMs, RealIoTimeoutMs, NegativeProofDelayMs); every literal 15000/300/50 call site across five test files is renamed to the matching constant, and the one unasserted sweep poll in MainViewModelTests now asserts instead of silently continuing.**

## Performance

- **Duration:** 25 min
- **Completed:** 2026-11-18
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Added `DefaultTimeoutMs` (3000), `RealIoTimeoutMs` (15000) and `NegativeProofDelayMs` (300) to `TestWait.cs`, each with a doc comment explaining its budget; `WaitForAsync`'s default parameter now reads `DefaultTimeoutMs` instead of a bare `3000`.
- Renamed 19 `timeoutMs: 15000` call sites (3 in `MainViewModelGuardPinTests.cs`, 16 in `MainViewModelTests.cs`) to `timeoutMs: RealIoTimeoutMs`.
- Renamed every negative-proof delay (3x `Task.Delay(300)` in `StartupUnsecuredPromptTests.cs`, 1x `Task.Delay(300)` in `AddGameDialogViewModelTests.cs`, 1x `Task.Delay(50)` in `StartupUnsecuredPromptTests.cs`) to `Task.Delay(NegativeProofDelayMs)` — the 50ms wait grows to 300ms, a stricter negative proof.
- Fixed the one unasserted poll: the synchronous sweep-deadline loop in `MainViewModelTests.cs` now uses `DateTime.UtcNow.AddMilliseconds(DefaultTimeoutMs)` and is immediately followed by `Assert.DoesNotContain(vm.Games, row => row.ComparisonKind == BackupComparisonKind.Checking)`, so a timed-out poll fails right there instead of producing confusing failures three assertions later.
- Confirmed the kept fixed waits are untouched: `Thread.Sleep(1100)` x2 in `RestoreServiceTests.cs` (archive names have one-second resolution) and the 20ms NTFS-timestamp waits in `CatalogSessionTests.cs`, `GameCatalogTests.cs`, `SaveStatusScannerTests.cs`.
- Ran the mandatory five-consecutive-run flake check: all five runs green, `739/739` passed each time, matching the pre-plan baseline Total exactly.

## Task Commits

Each task was committed atomically:

1. **Task 1: Named constants end to end — TestWait, the guard pins and the negative proofs** - `6065f65` (test)
2. **Task 2: MainViewModelTests — every real-I/O timeout named, the unasserted poll asserted; flake check** - `46da5eb` (test)

_No separate plan-metadata commit — `commit_docs: false` in `.planning/config.json`, per plan instruction STATE.md/ROADMAP.md updates are owned by the orchestrator._

## Files Created/Modified
- `tests/SavegameBunker.Tests/TestWait.cs` - added the three named constants and doc comments; `WaitForAsync`'s default parameter now reads `DefaultTimeoutMs`
- `tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs` - 3 `timeoutMs: 15000` → `timeoutMs: RealIoTimeoutMs`
- `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` - 3 negative-proof delays renamed to `NegativeProofDelayMs`
- `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs` - 1 negative-proof delay renamed to `NegativeProofDelayMs`
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` - 16 `timeoutMs: 15000` → `timeoutMs: RealIoTimeoutMs`; sweep-poll deadline named and followed by an assertion

## Decisions Made
- The 50ms negative-proof wait grows to 300ms (`NegativeProofDelayMs`) rather than getting its own smaller constant — plan explicitly calls this out as "only stricter," not a behavior change.
- Kept the sweep-poll loop synchronous (no `await`) per its existing comment — the dispatcher-bound `GamesView` must not be touched from a different thread — and only named its deadline and added the trailing assertion, per plan scope.

## Deviations from Plan

None - plan executed exactly as written. Both tasks' acceptance-criteria greps and the automated `<verify>` commands all matched the plan's expected values exactly (19 renamed `timeoutMs: 15000` sites, 4+ `NegativeProofDelayMs` sites, 0 remaining literals outside `TestWait.cs`, 2 kept `Thread.Sleep(1100)` sites, five green flake-check runs).

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ITEM-24 closed: named wait budgets everywhere, the one unasserted poll now asserts, the justified fixed waits are untouched, and the suite is confirmed stable across five consecutive runs (739/739 each time).
- No blockers for Plan 02-15 (the `MainViewModelTestBase` split), which depends on this plan's `TestWait.cs` state.

## Self-Check: PASSED

- FOUND: tests/SavegameBunker.Tests/TestWait.cs (contains RealIoTimeoutMs)
- FOUND: commit 6065f65 (ancestor of HEAD)
- FOUND: commit 46da5eb (ancestor of HEAD)

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
