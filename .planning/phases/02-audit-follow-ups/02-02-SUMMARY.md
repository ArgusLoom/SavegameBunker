---
phase: 02-audit-follow-ups
plan: 02
subsystem: ui
tags: [wpf, mvvm, communitytoolkit, refactor, dialogs]

requires:
  - phase: 01-bugfixing-refactoring
    provides: DirectoryWalk.CountFiles, SizeFormat.Describe, PathResolver, the Strings resource helpers
provides:
  - internal static SaveFolderPreview.Describe(string folder) — the one resolve line both dialogs show
  - synchronous [RelayCommand] Browse() in both dialog view models (generated BrowseCommand unchanged)
affects: [02-audit-follow-ups later plans, future dialog work]

actuals:
  tokens: 5800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns: ["pure static Services helper shared by two view models instead of a copied private method"]

key-files:
  created:
    - src/SavegameBunker/Services/SaveFolderPreview.cs
  modified:
    - src/SavegameBunker/ViewModels/ChangeSaveFolderDialogViewModel.cs
    - src/SavegameBunker/ViewModels/AddGameDialogViewModel.cs
    - tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs
    - tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs

key-decisions:
  - "SaveFolderPreview is internal, so ServiceContractTests' public-surface reflection fact is unaffected"

patterns-established:
  - "Shared dialog status text lives in a pure static Services helper; view models only orchestrate"

requirements-completed: [ITEM-16]

coverage:
  - id: D1
    description: "Both dialogs show their resolve line through one internal static SaveFolderPreview.Describe; the two private ResolveFolder copies and the FolderMissingStatus field are gone"
    requirement: "ITEM-16"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs#resolve-line facts"
        status: unknown
      - kind: other
        ref: "dotnet build SavegameBunker.sln -c Release (0 Warning(s), 0 Error(s))"
        status: pass
    human_judgment: true
    rationale: "The xUnit suite targets net10.0-windows and cannot run on this Linux build host (missing Microsoft.WindowsDesktop.App) — all test runs deferred to the owner's Windows machine."
  - id: D2
    description: "Browse is synchronous [RelayCommand] private void Browse() in both dialogs; generated property stays BrowseCommand so both XAML bindings are untouched"
    requirement: "ITEM-16"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs#Browse_puts_the_picked_folder_in_as_a_template"
        status: unknown
      - kind: unit
        ref: "tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs#Browse_puts_the_picked_folder_in_as_a_template"
        status: unknown
      - kind: other
        ref: "grep over both XAML files: Command=\"{Binding BrowseCommand}\" present twice"
        status: pass
    human_judgment: true
    rationale: "New pinning facts authored but not executed on this host (Linux, no WindowsDesktop.App); deferred to the owner's machine."

metrics:
  duration: 6 min
  completed: 2026-11-18
status: complete
plan_head_before: b040ebaf27a691d5d607c7038ec2d0076f27a505
plan_head_after: 23a5a5c
---

# Phase 02 Plan 02: Shared save-folder resolve line Summary

**One internal static `SaveFolderPreview.Describe` now feeds both dialogs' resolve lines and Browse is synchronous in both, with zero package additions and unchanged strings or bindings (ITEM-16).**

## Performance

- **Duration:** 6 min
- **Started:** 2026-11-18T20:06:14Z
- **Completed:** 2026-11-18T20:06:44Z (wall clock incl. builds; plan work ~15 min)
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- `Services/SaveFolderPreview.cs` — new `internal static class SaveFolderPreview` with `Describe(string folder)`, holding the previously duplicated ~20-line resolve routine exactly (same Strings keys, same missing-folder text).
- `ChangeSaveFolderDialogViewModel` and `AddGameDialogViewModel` now call `SaveFolderPreview.Describe(folder)` from `ReportResolveAsync` (still `Task.Run` behind the unchanged `_resolveToken` guard); their private `ResolveFolder` copies and the Add dialog's `FolderMissingStatus` field are deleted; Browse is `[RelayCommand] private void Browse()` in both, generated `BrowseCommand` unchanged so both XAML bindings hold.
- One pinning fact per dialog: `Browse_puts_the_picked_folder_in_as_a_template`, asserting synchronously that the picked folder lands as `PathResolver.ToTemplate(...)` in `SaveFolder`.
- Usings this change made unused (`System.IO` in both view models) removed.

## Task Commits

1. **Task 1: SaveFolderPreview end to end through the Change save folder dialog** - `8b8c85a` (refactor)
2. **Task 2: The Add game dialog uses the same helper and a synchronous Browse** - `23a5a5c` (refactor)

**Plan diff check:** `git diff --stat b040eba..23a5a5c` touches exactly the five files the plan lists.

## Files Created/Modified
- `src/SavegameBunker/Services/SaveFolderPreview.cs` - the shared resolve-line helper (internal, never throws, callers run it off the UI thread)
- `src/SavegameBunker/ViewModels/ChangeSaveFolderDialogViewModel.cs` - helper call, synchronous Browse
- `src/SavegameBunker/ViewModels/AddGameDialogViewModel.cs` - helper call, synchronous Browse, dead static field removed
- `tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs` - Browse pinning fact
- `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs` - Browse pinning fact

## Decisions Made
- `SaveFolderPreview` is `internal` (plan-directed): ServiceContractTests reflects only over public Services types, so the public-surface fact is untouched.
- Deleted `FolderMissingStatus` after confirming by grep that the removed `ResolveFolder` was its only remaining use.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- `dotnet test` cannot run on this Linux build host: the test host aborts with "framework Microsoft.WindowsDesktop.App ... rid=linux-x64 not found" for every filter, including the full suite. All plan `<verify>` test steps are therefore **deferred to the owner's Windows machine with result: [pending]**:
  - Filtered run (ChangeSaveFolderDialogViewModelTests | AddGameDialogViewModelTests | ServiceContractTests): result: [pending]
  - Full suite (`dotnet test SavegameBunker.sln -c Release`): result: [pending]
  - New facts `Browse_puts_the_picked_folder_in_as_a_template` (both classes): result: [pending]
- The Release build itself ran here: 0 Warning(s), 0 Error(s), after both tasks.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- ITEM-16 closed at the code level; only the Windows test runs remain outstanding.
- The csproj still carries exactly two PackageReferences (T-02-SC honored); no registry, service-contract, or restore-order constraint was touched.

---
*Phase: 02-audit-follow-ups*
*Completed: 2026-11-18*
