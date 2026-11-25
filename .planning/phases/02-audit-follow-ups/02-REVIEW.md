---
phase: 02-audit-follow-ups
reviewed: 2026-11-25T00:00:00Z
depth: standard
files_reviewed: 52
files_reviewed_list:
  - .github/workflows/release.yml
  - CHANGELOG.md
  - SPEC.md
  - docs/userguide/de/docs/reference/keyboard-shortcuts.md
  - docs/userguide/en/docs/reference/keyboard-shortcuts.md
  - src/SavegameBunker/MainWindow.xaml
  - src/SavegameBunker/MainWindow.xaml.cs
  - src/SavegameBunker/Models/BackupArchive.cs
  - src/SavegameBunker/Models/BackupComparison.cs
  - src/SavegameBunker/Models/OperationResult.cs
  - src/SavegameBunker/Models/SaveStatus.cs
  - src/SavegameBunker/SavegameBunker.csproj
  - src/SavegameBunker/Services/BackupComparer.cs
  - src/SavegameBunker/Services/BackupService.cs
  - src/SavegameBunker/Services/DialogService.cs
  - src/SavegameBunker/Services/DirectoryWalk.cs
  - src/SavegameBunker/Services/FileSystemFault.cs
  - src/SavegameBunker/Services/PathCompare.cs
  - src/SavegameBunker/Services/ReparseFile.cs
  - src/SavegameBunker/Services/RestoreService.cs
  - src/SavegameBunker/Services/SaveFolderPreview.cs
  - src/SavegameBunker/Services/SaveStatusScanner.cs
  - src/SavegameBunker/Themes/Strings.en.xaml
  - src/SavegameBunker/Themes/Styles.xaml
  - src/SavegameBunker/ViewModels/AboutDialogViewModel.cs
  - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
  - src/SavegameBunker/ViewModels/BulkRunRules.cs
  - src/SavegameBunker/ViewModels/CatalogSession.cs
  - src/SavegameBunker/ViewModels/GameActions.cs
  - src/SavegameBunker/ViewModels/GameRowViewModel.cs
  - src/SavegameBunker/ViewModels/MainViewModel.cs
  - src/SavegameBunker/ViewModels/RowActionsCoordinator.cs
  - src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs
  - src/SavegameBunker/Views/AboutDialog.xaml
  - src/SavegameBunker/Views/AboutDialog.xaml.cs
  - src/SavegameBunker/Views/RestorePickerWindow.xaml
  - src/SavegameBunker/Views/RestorePickerWindow.xaml.cs
  - tests/SavegameBunker.Tests/AboutDialogViewModelTests.cs
  - tests/SavegameBunker.Tests/BackupComparerTests.cs
  - tests/SavegameBunker.Tests/BackupServiceTests.cs
  - tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs
  - tests/SavegameBunker.Tests/CatalogSessionTests.cs
  - tests/SavegameBunker.Tests/DirectoryWalkTests.cs
  - tests/SavegameBunker.Tests/MainViewModelBulkTests.cs
  - tests/SavegameBunker.Tests/MainViewModelTestBase.cs
  - tests/SavegameBunker.Tests/MainViewModelTests.cs
  - tests/SavegameBunker.Tests/PathCompareTests.cs
  - tests/SavegameBunker.Tests/ResourceKeyTests.cs
  - tests/SavegameBunker.Tests/RestorePickerViewModelTests.cs
  - tests/SavegameBunker.Tests/RestoreServiceTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
  - tests/SavegameBunker.Tests/StringsTests.cs
  - tests/SavegameBunker.Tests/TempDir.cs
  - tests/SavegameBunker.Tests/TestGames.cs
findings:
  critical: 0
  warning: 3
  info: 1
  total: 4
  necessity: 0
status: issues_found
---

# Phase 02-audit-follow-ups: Code Review Report

**Reviewed:** 2026-11-25
**Depth:** standard
**Files Reviewed:** 52
**Status:** issues_found

## Summary

This phase's diff (`e9cf897..HEAD`, 26 commits) touches almost every file in the required-reading
list: the directory-walk fault handling (WR-02 "skip a folder whose listing fails midway"), the
bulk-backup warning/rescan plumbing (WR-03/WR-04), the new About window, the catalog write lock, the
restore layout-refusal sharing between pre-flight and the actual restore, and the partial-archive
(`-partial`, `LastBackupPartial`) model additions. The code is unusually well exercised — 16+ prior
review passes are visible in the git log, and the new behavior in this diff is backed by specific,
targeted xUnit facts (`DirectoryWalkTests`, `BackupComparerTests`, `StartupUnsecuredPromptTests`,
etc.) that pin exactly the edge cases the CHANGELOG claims were fixed.

No security vulnerability, crash, or data-loss defect was found. The findings below are two real
correctness/quality gaps in the newly changed code (a double-reporting edge case in the directory
walk's new fault-tolerance logic, and a message-precedence collision in the row status text), one
behavior change whose safety depends on a file outside this phase's scope (`GameCatalog.cs`, not
in the required-reading list), and one test-coverage gap for the new About button. The
necessity-review reference (`necessity-review.md`) was loaded; no finding in this diff met its bar
for an impossible-state/already-defended/cargo-culted/phantom-requirement/generality-without-
instantiation classification, so `necessity: 0`.

## Warnings

### WR-01: `DirectoryWalk.ForEachFile` can double-report one directory as unreadable

**File:** `src/SavegameBunker/Services/DirectoryWalk.cs:86-93` and `128-136`
**Issue:** The new `listingFaulted` guard (added in this phase, see `git log` commit
"skip a folder whose listing fails midway instead of aborting the walk") is meant to report a
directory as skipped **at most once**, "whichever of the two listings (files, subfolders) hit it"
(the comment at line 84-85). That guard is shared correctly between the two `GuardListing` calls
(line 95 and line 138) via the local `ListingFaulted()` function — but the **subdirectory listing's
own open-call catch block** (lines 128-136) calls `Skip(current)` directly instead of going through
`ListingFaulted()`:
```csharp
IEnumerable<string> subdirs;
try
{
    subdirs = Directory.EnumerateDirectories(current);
}
catch (Exception ex) when (FileSystemFault.Is(ex))
{
    Skip(current);   // <-- bypasses listingFaulted
    return;
}
```
If the **files** listing already faulted mid-iteration (`GuardListing(files, ListingFaulted)` at
line 95 already called `ListingFaulted()`, setting `listingFaulted = true` and calling `Skip(current)`
once), and the share/volume is now fully gone by the time the walk tries to **open** the
subdirectories enumerator, that open call throws immediately and this second catch block calls
`Skip(current)` a second time for the identical path — exactly the "a network share dropped… while
a save folder was being read" scenario this phase's fix targets (CHANGELOG, "A folder that fails
partway through being listed is now skipped on its own"). The duplicate entry propagates into
`skippedPathsFound`/`skipped` (a plain `List<string>`, not deduplicated) and from there into
`BackupService.DescribeSkippedPathsWarning`'s `{count}` and `DirectoryWalk.FormatSkippedList`'s
"first five, comma-joined" output — a backup or status row can report one more unreadable path than
actually exists, and the duplicate can crowd a genuinely different path out of the "and N more"
window. `DirectoryWalkTests` only exercises `GuardListing` in isolation, and
`BackupServiceTests`/`SaveStatusScanner` tests only exercise a single fault per directory, so this
combination is untested.
**Fix:** Route the subdirectory-listing open-call catch through the same guard, e.g.:
```csharp
catch (Exception ex) when (FileSystemFault.Is(ex))
{
    ListingFaulted();
    return;
}
```

### WR-02: A row that is both oversized and has skipped/left-out subfolders silently loses the "unusually large" warning

**File:** `src/SavegameBunker/ViewModels/GameRowViewModel.cs:196-202` and `217-228`
**Issue:** `ApplyStatus`'s `Found` branch sets `StatusText` to the oversized warning
(`str_game_oversized`) when `status.Oversized` is true (line 196-202). Immediately after the
`switch`, two `if`/`else if` blocks unconditionally overwrite `StatusText` — first for
`SkippedPaths.Count > 0` (pre-existing), and now also for the phase's new `LeftOutPaths.Count > 0`
case (lines 223-228, added by this diff). Neither branch checks whether `status.Oversized` is also
true, so a save folder that is simultaneously unusually large *and* partially unreadable, or
unusually large *and* also the backup folder (pruned subfolders), shows only the
unreadable-paths/left-out-subfolders sentence — the SPEC's oversized-folder safety signal ("make
sure it's the right one") disappears from the row for as long as the other condition holds, with no
way for the user to learn the folder is also outsized short of opening it in Explorer.
`GameSeverity` stays `Warning` either way, so the icon doesn't reveal the loss — only the text does.
**Fix:** Compose the sentences instead of overwriting, e.g. append rather than replace:
```csharp
if (status.Kind is (SaveStatusKind.NoSaves or SaveStatusKind.Found) && status.SkippedPaths.Count > 0)
{
    GameSeverity = RowSeverity.Warning;
    StatusText = JoinNonEmpty(StatusText, Strings.Format("str_game_unreadablePaths", ...));
}
```
or, at minimum, document that oversized is deliberately the lowest-priority warning and accept the
loss (currently undocumented).

### WR-03: The window-close guard no longer blocks while a catalog file write is in flight

**File:** `src/SavegameBunker/MainWindow.xaml.cs:120` (uses the new
`src/SavegameBunker/ViewModels/MainViewModel.cs:359-360` property)
**Issue:** `MainWindow_Closing` was changed in this phase from guarding on
`ViewModel.IsAnyOperationRunning` to the newly introduced, narrower
`ViewModel.IsAnyBackupOrRestoreRunning`, which excludes `IsCatalogCommandRunning` (Refresh, Change
backup folder, Add game, Change save folder, Remove game — `MainViewModel.cs:369-371`). The
in-code rationale ("a catalog command may merely be waiting on its confirmation, and the catalog
write itself is a single file save") only covers the *confirmation-wait* portion of these commands;
it does not establish that the actual file write (`GameCatalog.SaveAsync`, not in this phase's
reviewed file set) is atomic/crash-safe if the process exits mid-write. If the user closes the main
window while, say, `RemoveGameAsync`'s `await _session.RemoveGameAsync(row.Game)` is genuinely still
writing `games.json`, WPF's default `ShutdownMode` tears the process down once `Closing`/`Closed`
return without `e.Cancel = true` — and nothing in the reviewed files proves the write survives that.
CLAUDE.md's own rule ("Never overwrite a file the user may have hand-edited") and the project's
demonstrated discipline elsewhere (backup archives are always written to a temp name and renamed
only after verification) suggest `GameCatalog.SaveAsync` likely follows the same pattern, but that
file is outside this phase's required-reading scope, so it could not be confirmed here, and no test
in the reviewed suite exercises a window-close during a catalog write.
**Fix:** Confirm `GameCatalog.SaveAsync` writes via a temp file + rename (matching
`BackupService`'s own discipline) before relying on this gate change; if it does not, either make it
do so or restore `IsAnyOperationRunning` (i.e., include `IsCatalogCommandRunning`) as the
`MainWindow_Closing` gate.

## Info

### IN-01: The toolbar access-key pair test does not cover the new About button

**File:** `tests/SavegameBunker.Tests/StringsTests.cs:124-144`
**Issue:** This phase adds the About button and its `str_toolbar_about`/`str_toolbar_about_access`
resource pair (CHANGELOG: "An About window, opened from a new About button"). The mnemonic-uniqueness
fact (`The_eight_toolbar_mnemonics_are_all_different_letters`, line ~147) was updated to include
`str_toolbar_about_access`, but the `ToolbarAccessKeyPairs`/`Every_toolbar_access_label_is_its_clean_
label_plus_one_underscore` fact (lines 124-144) — the one that actually checks "access label = clean
label + one underscore" for each pair — was not extended to the new pair. The values themselves are
correct (`"About"` / `"Abo_ut"`), so nothing is currently broken, but the convention this phase relies
on for every other toolbar button is unguarded for the one it just added.
**Fix:** Add `["str_toolbar_about", "str_toolbar_about_access"]` to `ToolbarAccessKeyPairs`.

---

_Reviewed: 2026-11-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
