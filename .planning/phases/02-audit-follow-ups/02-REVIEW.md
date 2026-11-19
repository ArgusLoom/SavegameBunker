---
phase: 02-audit-follow-ups
reviewed: 2026-11-19T00:00:00Z
depth: standard
files_reviewed: 61
files_reviewed_list:
  - CHANGELOG.md
  - SPEC.md
  - src/SavegameBunker/App.xaml
  - src/SavegameBunker/App.xaml.cs
  - src/SavegameBunker/MainWindow.xaml
  - src/SavegameBunker/Services/BackupService.cs
  - src/SavegameBunker/Services/DialogService.cs
  - src/SavegameBunker/Services/DirectoryWalk.cs
  - src/SavegameBunker/Services/DriveHints.cs
  - src/SavegameBunker/Services/FileSystemFault.cs
  - src/SavegameBunker/Services/IDialogService.cs
  - src/SavegameBunker/Services/PathCompare.cs
  - src/SavegameBunker/Services/PathResolver.cs
  - src/SavegameBunker/Services/ReparseFile.cs
  - src/SavegameBunker/Services/RestoreService.cs
  - src/SavegameBunker/Services/SaveFolderPreview.cs
  - src/SavegameBunker/Themes/Styles.xaml
  - src/SavegameBunker/ViewModels/AddGameDialogViewModel.cs
  - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
  - src/SavegameBunker/ViewModels/ChangeSaveFolderDialogViewModel.cs
  - src/SavegameBunker/ViewModels/GameActions.cs
  - src/SavegameBunker/ViewModels/MainViewModel.cs
  - src/SavegameBunker/Views/AddGameDialog.xaml
  - src/SavegameBunker/Views/ChangeSaveFolderDialog.xaml
  - src/SavegameBunker/Views/RestorePickerWindow.xaml
  - src/SavegameBunker/Views/ScanWindow.xaml
  - tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs
  - tests/SavegameBunker.Tests/AddGameFlowTests.cs
  - tests/SavegameBunker.Tests/BackupServiceTests.cs
  - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs
  - tests/SavegameBunker.Tests/CatalogSessionTests.cs
  - tests/SavegameBunker.Tests/ChangeSaveFolderDialogViewModelTests.cs
  - tests/SavegameBunker.Tests/DriveHintsTests.cs
  - tests/SavegameBunker.Tests/DropFolderTests.cs
  - tests/SavegameBunker.Tests/GameCatalogTests.cs
  - tests/SavegameBunker.Tests/HelpPathTests.cs
  - tests/SavegameBunker.Tests/KnownGamesScannerTests.cs
  - tests/SavegameBunker.Tests/MainViewModelBulkTests.cs
  - tests/SavegameBunker.Tests/MainViewModelBusyTests.cs
  - tests/SavegameBunker.Tests/MainViewModelGuardPinTests.cs
  - tests/SavegameBunker.Tests/MainViewModelRestoreTests.cs
  - tests/SavegameBunker.Tests/MainViewModelRowListTests.cs
  - tests/SavegameBunker.Tests/MainViewModelStartupHealthTests.cs
  - tests/SavegameBunker.Tests/MainViewModelStatusLineTests.cs
  - tests/SavegameBunker.Tests/MainViewModelTestBase.cs
  - tests/SavegameBunker.Tests/MainViewModelTests.cs
  - tests/SavegameBunker.Tests/PathCompareTests.cs
  - tests/SavegameBunker.Tests/PathResolverTests.cs
  - tests/SavegameBunker.Tests/RecordingDialogService.cs
  - tests/SavegameBunker.Tests/ResourceKeyTests.cs
  - tests/SavegameBunker.Tests/RestorePickerViewModelTests.cs
  - tests/SavegameBunker.Tests/RestoreServiceTests.cs
  - tests/SavegameBunker.Tests/RowActionsCoordinatorTests.cs
  - tests/SavegameBunker.Tests/SaveStatusScannerTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/ShellOpenTests.cs
  - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
  - tests/SavegameBunker.Tests/TempDir.cs
  - tests/SavegameBunker.Tests/TestGames.cs
  - tests/SavegameBunker.Tests/TestWait.cs
findings:
  critical: 0
  warning: 1
  info: 1
  total: 2
  necessity: 0
status: issues_found
---

# Phase 2: Code Review Report

**Reviewed:** 2026-11-19
**Depth:** standard
**Files Reviewed:** 61
**Status:** issues_found

## Summary

Reviewed every production and test file in Phase 2's (audit-follow-ups) scope: the item-0/D-10/D-11
exclusion-and-restore-refusal rewrite in `BackupService`/`DirectoryWalk`/`PathCompare`/`RestoreService`,
the `FileSystemFault` consolidation, the `DriveHints`/`MainViewModel` startup-prompt and
bulk-preparation-guard fixes (items 1-3), the `PathCompare`/`SaveFolderPreview` extractions (items 4,
16), the `DialogService` delegate-injection and `Confirm` cleanup (item 11), `RequeryOperationGates`
(item 14), the unified bulk-progress callback (item 15), and the toolbar/row/dialog-footer XAML
dedup plus the `SavegameBunkerDangerColor`/`SavegameBunkerDangerBrush` rename (items 19-21).

Cross-checked the described fixes against the live code rather than trusting the SUMMARY files: every
item's stated change is actually present (`PathCompare.Normalize` keeps a drive root; `DirectoryWalk`'s
`excludedWithinRoot` split matches the refined D-10 rule; `RestoreService.Restore` now guards on
`gameArchiveDir` rather than only `backupRoot`; `MainViewModel.IsBulkPreparing` is wired into
`IsAnyOperationRunning`/`CanBackupRow`/`CanRestoreRow`/`RequeryOperationGates`; `ToolbarButton`'s
`ContentTemplate` replaced the ten duplicated button bodies; `DialogFooterButton` unifies the four
dialogs' confirm/cancel width; `SavegameBunkerDangerBrush` replaced `PaletteRedBrush` everywhere,
including `ResourceKeyTests`). Rebuilt (`dotnet build -c Release`: 0 warnings, 0 errors) and ran the
full suite (`dotnet test -c Release`: **739/739 passing**), matching the claimed baseline.

One correctness gap survives the item-0 rewrite and was confirmed by reproduction (not asserted from
reading alone): a save folder that is exactly a game's own per-game archive folder
(`saveDir == <backupRoot>\<id>`) — a layout item 0/D-11 explicitly keeps "supported for Backup" — packs
every earlier completed archive of that same game into each new backup, because the exclusion rule
only prunes *subfolders*, never files sitting directly in the walk root. This was already named as an
open, unresolved question in `02-RESEARCH.md` ("R equals T... Existing degenerate hazard; see Open
Question 2") but was not covered by `CONTEXT.md`'s decisions, is not pinned by a test, and is not
disclosed in `SPEC.md`'s "Known limits of the promise" the way the project's other edge cases are. It
is not a data-loss risk (Restore already refuses this exact layout per D-11), so it is reported as a
Warning, not a Blocker. The other finding is a small piece of resource-dictionary dead weight that
item 21's own edit left behind.

The two findings previously disclosed as already-tracked (the restore-picker delete-confirmation modal
blocker, and the two unrelated screenshot/demo-data script issues) are intentionally **not**
re-reported here.

## Warnings

### WR-01: Backup of a save folder equal to its own archive folder repacks every earlier archive

**File:** `src/SavegameBunker/Services/DirectoryWalk.cs:63-93` (file-enumeration loop never checks `excludedDirs`), `src/SavegameBunker/Services/BackupService.cs:112-121` (`excludedDirs` only ever prunes directories), `SPEC.md:221-231` (documents this layout as supported for Backup without naming the hazard)
**Issue:** `DirectoryWalk.ForEachFile`'s exclusion rule (the D-10 refinement this phase shipped) only prunes entries returned by `Directory.EnumerateDirectories(current)` — the `foreach (var sub in subdirs)` loop. The sibling `foreach (var f in files)` loop, which walks `Directory.EnumerateFiles(current)` for the *walk root itself*, has no exclusion check at all. When a game's save folder is exactly its own per-game archive folder (`saveDir == Path.Combine(backupRoot, game.Id)` — the "R equals T" layout SPEC.md now documents as honoured for Backup, refused only for Restore per D-11), every `.zip` already written for that game sits as an ordinary file directly inside the walk root and gets swept into the next backup, growing without bound. Confirmed by reproduction against the built `SavegameBunker.dll`:
  ```
  r1 ok=True archive=...\bk\demo\2026-11-19_030704.zip
  Archive1 entries: save.dat
  r2 ok=True archive=...\bk\demo\2026-11-19_030704-1.zip
  Archive2 entries: 2026-11-19_030704.zip (131 bytes), save.dat (15 bytes)
  ```
  Archive2 (the second backup of the same unchanged save folder) contains Archive1 as a packed-in file. Every subsequent backup would nest one layer deeper. The in-flight `.zip.tmp` staging file is also a direct child of the same folder in this layout; in the reproduction it happened to be reported as an (inaccurately labeled) "could not be read" skip rather than being deliberately excluded — a second, smaller symptom of the same root cause. No test in `BackupServiceTests.cs` exercises `saveDir == <backupRoot>\<id>` for Backup (only the Restore-refusal side of this layout, `RestoreServiceTests.cs:820`, is pinned). `02-RESEARCH.md:113` already named this exact gap ("R equals T: archives and the `.zip.tmp` staging file are root-level files, which the walk never excludes. Existing degenerate hazard; see Open Question 2") but `02-CONTEXT.md`'s decisions resolve Open Question 1 (D-10) without addressing Open Question 2, so it shipped unfixed and undocumented.
**Fix:** Either (a) also compare each file's parent-relative check against `excludedDirs` so a root-level file that lives inside an excluded folder's own archive folder is skipped — simplest: when `rootDir` itself equals one of the `excludedDirs` entries (the one case left open), skip files whose extension is `.zip` or `.zip.tmp` in that walk root, the same way the empty-check and verification walk already special-case reparse points; or (b) extend `SPEC.md`'s "Known limits of the promise" section to disclose the hazard explicitly (matching how the reparse-point and zip-cap edge cases are documented there) if the team decides this layout is rare enough to leave as a documented limitation rather than a code fix. Either way, add a `BackupServiceTests` fact pinning whichever behavior is chosen, so the next change to this walk cannot silently reopen or silently "fix" it without a test noticing.

## Info

### IN-01: Orphaned `xs` spacing token left by item 21's own edit

**File:** `src/SavegameBunker/Themes/Styles.xaml:57` (`<Thickness x:Key="xs">4</Thickness>`)
**Issue:** Item 21 replaced the misused four-sided `{StaticResource xs}` margins at `MainWindow.xaml`'s bulk-progress `ProgressBar` and the restore-step `TextBlock` with explicit one-sided literals (`Margin="0,4,0,0"`), per the plan's own candidate values. Those were `xs`'s only two call sites in `src/SavegameBunker` (confirmed by `grep -r "StaticResource xs\}"` returning no matches anywhere in the project). The token's definition at `Styles.xaml:57` was left in place, unlike `lg`, `xl`, `radiusControlValue` and `radiusControl`, which the same item correctly deleted for being unused. `xs` is now exactly as unused as those four, just not removed.
**Fix:** Delete the `<Thickness x:Key="xs">4</Thickness>` line, matching the cleanup already done for `lg`/`xl`/`radiusControlValue`/`radiusControl`.

---

_Reviewed: 2026-11-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
