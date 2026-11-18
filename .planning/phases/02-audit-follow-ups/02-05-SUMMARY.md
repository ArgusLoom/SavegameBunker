---
phase: 02-audit-follow-ups
plan: 05
subsystem: ui
tags: [driveinfo, folder-picker, wpf, xunit, network-drive]

# Dependency graph
requires:
  - phase: 02-audit-follow-ups
    provides: plan 02-01's CHANGELOG.md `[Unreleased] ### Fixed` block (the only dependency)
provides:
  - DriveHints.ShouldProbeReadiness(DriveType) — IsReady probed only for Fixed and Removable
  - internal MainViewModel constructor injecting the start-folder probe (Func<string?>)
  - drive-independent picker test helper (_suggestedStartFolder) in MainViewModelTests
  - reachable bulk-path picker-cancel fact (arrangement reused by plan 02-06)
  - fact proving an unset backup root never reaches the startup prompt
affects: [02-06, 02-validate]

# Actuals (#2632)
actuals:
  tokens: 4600
  tasks: 2
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Phase 1 seam style: internal constructor overload delegating to the public one — no new dependency type beyond a Func<string?>"

key-files:
  created: []
  modified:
    - src/SavegameBunker/Services/DriveHints.cs
    - src/SavegameBunker/ViewModels/MainViewModel.cs
    - tests/SavegameBunker.Tests/DriveHintsTests.cs
    - tests/SavegameBunker.Tests/MainViewModelTests.cs
    - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
    - CHANGELOG.md

key-decisions:
  - "D-04: deletion NOT taken — the unreachable startup-picker fact was reworked into a reachable bulk-path picker-cancel fact, because no test covered cancelling the picker on the bulk path (BackupCommand only covers the row path)"
  - "The drive probe is injected as a Func<string?> through an internal MainViewModel constructor; App keeps the public constructor (grep-gated), no new dependency type"

patterns-established:
  - "Picker tests assert the injected start folder and never enumerate the machine's real drives"

requirements-completed: [ITEM-3, D-04, D-05, D-13]

coverage:
  - id: D1
    description: "IsReady is probed only for Fixed and Removable drives; Network/CDRom/Ram/NoRootDirectory/Unknown are skipped"
    requirement: "D-05"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/DriveHintsTests.cs#Readiness_is_probed_only_for_fixed_and_removable_drives"
        status: unknown
    human_judgment: false
  - id: D2
    description: "Picker path is drive-independent: tests inject the start folder; the picked folder, not the suggestion, is stored"
    requirement: "D-05"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#The_first_backup_folder_prompt_opens_on_the_detected_start_folder"
        status: unknown
    human_judgment: false
  - id: D3
    description: "Cancelling the picker on the bulk path (Back up selected, unset root) writes nothing and leaves no operation running"
    requirement: "D-13"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/MainViewModelTests.cs#Back_up_selected_with_an_unset_backup_root_opens_the_picker_and_cancelling_writes_nothing"
        status: unknown
    human_judgment: false
  - id: D4
    description: "An unset backup root never reaches the startup prompt (no row can be unsecured with FolderNotAvailable)"
    requirement: "D-04"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs#An_unset_backup_root_never_reaches_the_prompt"
        status: unknown
    human_judgment: false
  - id: D5
    description: "Full suite passes five times in a row (02-VALIDATION.md flake check)"
    verification: []
    human_judgment: true
    rationale: "Windows-only test runs — this Linux build host cannot execute the xUnit/WPF suite (no Microsoft.WindowsDesktop.App); all five runs are pending on the owner's Windows machine"

# Metrics
duration: 27min
completed: 2026-11-18
status: complete
plan_head_before: 18032ec526e27e2593f0d6154f207f6fca27bb04
plan_head_after: ef77357
---

# Phase 2 Plan 5: Defect item 3 — drive probe, picker seam and the unreachable startup test Summary

**DriveHints no longer reads IsReady on network or optical drives (stale SMB mappings can no longer stall the first-backup folder picker), the picker tests run on an injected start folder instead of the machine's real drives, and the unreachable startup-prompt test became a reachable bulk-path picker-cancel fact.**

## Performance

- **Duration:** 27 min
- **Started:** 2026-11-18T20:14:36Z
- **Completed:** 2026-11-18T20:41:30Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- `DriveHints.ShouldProbeReadiness` gates the `IsReady` read to Fixed and Removable drives; `SuggestBackupStartFolder` reads `DriveType` first and only probes readiness for those two types — a disconnected mapped network drive can no longer block the picker (ITEM-3, D-05).
- The start-folder probe is injected as a `Func<string?>` through an `internal MainViewModel` constructor (Phase 1 seam style); App's public-constructor call is unchanged.
- Picker tests are drive-independent: the injected `_suggestedStartFolder` reaches the picker, and the picked folder — not the suggestion — is what gets stored.
- The unreachable `Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op` is replaced by a reachable fact: an unset root never reaches the prompt (every row settles on `BackupFolderNotAvailable`, so `BulkRunRules.NeedsBackup` has nothing to offer).
- New reachable bulk-path fact `Back_up_selected_with_an_unset_backup_root_opens_the_picker_and_cancelling_writes_nothing` — plan 02-06 reuses this arrangement.
- CHANGELOG `[Unreleased] ### Fixed` bullet: choosing a backup folder no longer stalls on a disconnected network drive.

## Task Commits

Each task was committed atomically:

1. **Task 1: Drive probe end to end** - `92fcc8b` (fix)
2. **Task 2: D-04 / D-13 rework + CHANGELOG** - `ef77357` (test)

**Plan metadata:** the `docs(02-05)` summary commit immediately after `ef77357` on `dev/v1.1`.

## Files Created/Modified
- `src/SavegameBunker/Services/DriveHints.cs` — `ShouldProbeReadiness` predicate + DriveType-before-IsReady loop
- `src/SavegameBunker/ViewModels/MainViewModel.cs` — `_suggestBackupStartFolder` field, internal constructor seam, `Task.Run(_suggestBackupStartFolder)`
- `tests/SavegameBunker.Tests/DriveHintsTests.cs` — theory `Readiness_is_probed_only_for_fixed_and_removable_drives` (7 InlineData rows)
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` — `_suggestedStartFolder`, CreateViewModel seam, drive-independent assertions, new bulk-path cancel fact
- `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` — unreachable fact replaced by `An_unset_backup_root_never_reaches_the_prompt`; `configuredBackupRoot` doc rewritten
- `CHANGELOG.md` — one new Fixed bullet

## Decisions Made
- **D-04: rework, not deletion.** `A_cancelled_first_backup_folder_prompt_writes_nothing` covers the row path (`BackupCommand`) only; no fact cancelled the picker on the bulk path, so per D-13 the unreachable fact's intent was preserved by reworking it into the reachable bulk-path fact.
- **Seam shape:** `Func<string?>` through an internal constructor, matching the plan's Phase 1 seam style; the public constructor chains to it with `DriveHints.SuggestBackupStartFolder`.
- No `WaitForAsync` timeout was changed anywhere (D-05: the flake is cured by removing the blocking probe, not by raising timeouts).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- **This Linux build host cannot run the xUnit suite** (`net10.0-windows` test host needs `Microsoft.WindowsDesktop.App`, unavailable here). Result: [pending] for every test run, including the five-run flake check (02-VALIDATION.md). The `dotnet build` gate **is** verified green here: `SavegameBunker.sln -c Release` builds 0 Warning(s) / 0 Error(s).
- All grep-based acceptance criteria for both tasks passed on this host:
  - `ShouldProbeReadiness` occurrences: 2 (≥2) ✔
  - `Task.Run(_suggestBackupStartFolder)`: 1 ✔ · internal ctor signature: 1 ✔
  - `DriveHints.SuggestBackupStartFolder()` in MainViewModelTests: 0 ✔ · App's `new MainViewModel(load, dialogService)`: 1 ✔ · `timeoutMs:` count: 16 (unchanged) ✔
  - bulk fact: 1 ✔ · old startup fact: 0 ✔ · new startup fact: 1 ✔ · CHANGELOG bullet: 1 ✔

## Flake Check (five consecutive full-suite runs)

Windows-only — result: [pending]. Record the five Totals here when the suite runs on the owner's Windows machine:

1. [pending]
2. [pending]
3. [pending]
4. [pending]
5. [pending]

## Windows-Only Test Runs

- `dotnet test … DriveHintsTests|MainViewModelTests|MainViewModelGuardPinTests|StartupUnsecuredPromptTests` — result: [pending]
- `dotnet test … MainViewModelTests|StartupUnsecuredPromptTests|DriveHintsTests` — result: [pending]
- Five consecutive full-suite runs — result: [pending]

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 02-06 can reuse the bulk picker-cancel arrangement from `Back_up_selected_with_an_unset_backup_root_opens_the_picker_and_cancelling_writes_nothing` for item 2's guard fact.
- No stubs introduced; no NuGet packages added (shipping csproj unchanged at two PackageReferences).

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
