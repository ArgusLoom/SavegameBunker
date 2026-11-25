---
phase: 01-bugfixing-refactoring
reviewed: 2026-11-25T00:00:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - CHANGELOG.md
  - src/SavegameBunker/Models/SaveStatus.cs
  - src/SavegameBunker/Services/BackupService.cs
  - src/SavegameBunker/Services/DirectoryWalk.cs
  - src/SavegameBunker/Services/SaveStatusScanner.cs
  - src/SavegameBunker/Themes/Strings.en.xaml
  - src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs
  - src/SavegameBunker/ViewModels/BulkRunRules.cs
  - src/SavegameBunker/ViewModels/GameRowViewModel.cs
  - src/SavegameBunker/ViewModels/MainViewModel.cs
  - src/SavegameBunker/ViewModels/RowActionsCoordinator.cs
  - src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs
  - tests/SavegameBunker.Tests/BackupServiceTests.cs
  - tests/SavegameBunker.Tests/ServiceContractTests.cs
  - tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs
findings:
  critical: 0
  warning: 5
  info: 7
  total: 12
  necessity: 2
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-11-25T00:00:00Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed the 15 files that changed after the 2026-11-19 review (Phase 02 work plus the 16
manual review-pass commits), each as it stands now, using `git diff f8665df..HEAD` to focus on
what changed. Supporting files read for cross-reference: `BackupComparer`, `BackupArchive`,
`BackupComparison`, `ReparseFile`, `FileSystemFault`, `PathCompare`, `ElapsedFormat`,
`RestoreService.ExceedsArchiveLimits`, `CatalogSession` (write lock only).

The walk/exclusion rules (`ArchiveWalkRules`, `DirectoryWalk` prune logic, own-archive-name
filtering), the claim/release ordering, the token-checked sweep writes, the restore ordering and
the two-dependency / no-registry constraints all hold up. Every `Strings` key used by the changed
code exists in `Strings.en.xaml`, and no reference to a removed key remains in source. No Critical
defects were found: no injection, traversal, secret or data-destroying path.

The findings are concentrated in three places. First, the verdict timestamp that the
save-vs-backup comparison trusts (WR-01). Second, a gap between `DirectoryWalk`'s documented
"never aborts, only skips" behaviour and what it does on a mid-enumeration IO error (WR-02). Third,
the bulk loops, which still diverge from the single-row paths in what they clean up and what they
tell the user (WR-03, WR-04). The previous review's WR-01 (bulk restore cleanup) is still open and
is restated here as WR-03. Its two default-branch necessity findings are carried as IN-06.

`$HOME/.claude/gsd-core/references/necessity-review.md` was read, so the `necessity:` key is
populated.

## Warnings

### WR-01: A backup's timestamp is taken after verification, so a save rewritten during the backup reads "Up to date"

**File:** `src/SavegameBunker/Services/BackupService.cs:198-200` (comment at 112-115; verification at 190-196)
**Issue:** The archive name (which `ParseTakenAt` turns into `BackupArchive.TakenAt`) is stamped
with `DateTime.Now` after the zip is written *and* re-read and re-hashed. `BackupComparer.Compare`
reports `UpToDate` whenever `TakenAt >= newest save mtime`. For a large save, the gap between "file
was read" and "timestamp taken" spans the whole rest of the archive pass plus the verification
read-back. A game that autosaves in that window and rewrites a fixed-size file in place (typical
for binary saves) has an mtime earlier than `TakenAt` but content that is not in the archive.
Verification does not catch it: with `expectedHashes` set (the backup path) the content check
compares the archive entry against the hash recorded *from the same read*, and the source
comparison is by name and length only (`BackupService.cs:736-746`). The row then says "Up to date"
for a backup that does not contain the newest save, and nothing re-flags it until the file is
written again. The comment at lines 112-115 picks the late stamp to avoid a false "changed since
backup", but the late stamp trades that harmless false positive for a silent false negative in the
one signal the app uses to say a game is protected.
**Fix:** Stamp with the instant the archive pass *began*, so any later write reads as "changed
since backup":
```csharp
var backupStartedAt = DateTime.Now;               // before ArchiveSaveDir
...
var timestamp = backupStartedAt.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
```
The collision ladder in `MoveStagedArchiveIntoPlace` already handles two backups that share a
start second. If the late stamp exists to cover a case I could not reproduce from the code, pin
that case with a test and document why the start-time stamp was rejected.

### WR-02: `DirectoryWalk` does not guard `MoveNext`, so one mid-listing IO error aborts the whole walk

**File:** `src/SavegameBunker/Services/DirectoryWalk.cs:82` and `:125` (claim at 11-18)
**Issue:** The remarks justify the one-level-at-a-time walk with "that lazy recursive enumerator
throws from `MoveNext` partway through a locked subtree and would abort the whole walk instead of
skipping just that subtree." The non-recursive enumerators have the same property:
`Directory.EnumerateFiles(current)` and `EnumerateDirectories(current)` are inside `try`, but
the `foreach (var f in files)` and `foreach (var sub in subdirs)` that call `MoveNext` are not. An
`IOException` after the listing was opened (a network share dropping, a removable volume pulled, a
cloud-sync provider failing) escapes `Walk`, unwinds the whole recursion and leaves `ForEachFile`.
Callers keep the service contract (every caller wraps it), so nothing crashes. The visible effects
are that one flaky subfolder turns into "Backup failed", a `PathError` row, or a `0` size, instead
of a skipped path listed in the warning, which is what this class promises and what the skip
reporting is built for. `ReparseFile.HasAnyFile` has the same shape.
**Fix:** Drive the enumeration by hand so a throwing `MoveNext` is treated like a throwing listing:
```csharp
using var e = files.GetEnumerator();
while (true)
{
    string f;
    try { if (!e.MoveNext()) break; f = e.Current; }
    catch (Exception ex) when (FileSystemFault.Is(ex)) { Skip(current); break; }
    ...
}
```
Do the same for `subdirs`. Alternatively, correct the remarks to say only the *opening* of a
listing is guarded.

### WR-03: Bulk restore leaves rows stuck on "Checking" and a restore bar stuck on if an item throws (previous WR-01, still open)

**File:** `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs:259-267`, `src/SavegameBunker/ViewModels/MainViewModel.cs:1699-1714`
**Issue:** `RestoreAsync` resets every target to `Checking` and bumps `ScanToken` at lines 203-207.
Its `finally` releases `IsBusy` and calls `onFinished`, but nothing re-scans on the exception path,
and `RestoreStep`/`RestoreStepText` are cleared only after a call *returns* (lines 238-239). The
re-scan lives in `RestoreSelectedAsync` *after* `await _bulk.RestoreAsync(...)`
(`MainViewModel.cs:1710`), so it never runs when the coordinator throws. The backup path is safe
because its `onFinished` callback performs the rescan from the `finally` (`MainViewModel.cs:1624-1628`).
If anything in the loop throws (`GameActions.RestoreAsync`, `TryPlanNewestOrdinaryAsync`, a dialog
that throws), the rows stay on `Checking`. That keeps `IsAnyRowChecking` true, so `CanBackupAllChanged`,
`CanBackupSelected` and `CanRestoreSelected` stay false until the user clicks Refresh. The thrown
row also keeps `IsRestoring` true with a frozen step bar. The service contract says nothing should
throw, but this finally block exists precisely for the case where something does.
**Fix:** Mirror the backup path: clear the progress fields in the `finally` and move the rescan
into `onFinished`.
```csharp
finally
{
    foreach (var row in targets)
    {
        row.IsBusy = false;
        row.RestoreStep = 0;
        row.RestoreStepText = "";
    }
    onFinished();   // EndBulkProgress(); StartStatusScan(only: targets);
}
```
Then drop the separate `StartStatusScan(only: targets)` at `MainViewModel.cs:1710`.

### WR-04: Bulk backup reports "all N backed up" and drops each game's incompleteness and restore-cap warnings

**File:** `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs:124-135`, `src/SavegameBunker/ViewModels/RowActionsCoordinator.cs:87-88`
**Issue:** On `result.Success` the bulk loop discards `result.Message`. That message is the only
place `BackupService.Backup` says that paths were unreadable and are NOT in the archive
(`BackupService.cs:226-227`), that folders were pruned (`:228-231`), and that the archive is
larger than restore will accept (`:232-235`). The single-row path hands the whole result to
`_host.Report`, so the user sees the warning. A bulk run (including the startup prompt's "Yes")
produces a `-partial` archive or an archive this app refuses to restore, then the closing outcome
says "Backed up all N games" (`BulkRunRules.BackupOutcome`, success branch). The only later hint is
the row's verdict after the rescan, and for the oversize case there is none until a restore is
refused.
**Fix:** Have the coordinator use the internal overload that returns the skipped paths and also
record the oversize warning, and carry them into `BulkBackupRun`:
```csharp
var result = _backup.Backup(row.Game, backupRoot, "", out _, out var skipped, byteProgress[row]);
if (result.Success && skipped.Count > 0) partial.Add(row.DisplayName);
```
Then have `BackupOutcome` name the partial games, or surface their messages the way
`failureDetails` already does for failures.

### WR-05: Success-message warnings run into each other with no separating punctuation

**File:** `src/SavegameBunker/Services/BackupService.cs:334-335`, `:354-355`, `:225-235`
**Issue:** `DescribeSkippedPathsWarning` and `DescribeNotBackedUpWarning` end with a bare path list
and no period, while `DescribeLeftOutNote` and the oversize warning begin directly after. A message
with both comes out as `...NOT in this backup: save.dat Left out: data/Backups (backup folder).`
or `...NOT in this backup: slot1 This archive is larger than SavegameBunker can restore.` The last
listed path then reads as part of the next sentence, and a path with a space in it becomes
ambiguous, in the message whose job is to say exactly what is missing from the backup. The new tests
(`BackupServiceTests.cs`, "Left out: game-b (backup folder)." together with "1 subfolder(s) are NOT
in this backup: game-a") only use `Assert.Contains` on each half, so they pass on the run-on form.
**Fix:** End both warnings with a period (as `DescribeLeftOutNote` already does) and add one test
asserting the exact combined string for a skipped-plus-left-out backup.

## Info

### IN-01: A permanently unreadable file makes its game a perpetual "back up all changed" target

**File:** `src/SavegameBunker/ViewModels/BulkRunRules.cs:30-32`
**Issue:** `NeedsBackup` now includes `LastBackupPartial`. For a game with a file that can never be
read (a launcher-held lock, an ACL-denied file), every backup is `-partial`, so the verdict stays
`LastBackupPartial`. The toolbar button stays visible, the startup prompt asks about the game on
every launch, and each "Back up all changed" writes one more partial archive with no way to
acknowledge or retire the condition. This is a design consequence rather than a code error, and
the intent (a partial copy must not read as protected) is sound. Consider whether the prompt and
bulk target should skip a game whose newest partial archive already names the same unreadable set,
or at least note it in SPEC.md as accepted.
**Fix:** Decide and document; no code change is required if the nag is intended.

### IN-02: Doc comments still describe the two-verdict rule after `LastBackupPartial` joined it

**File:** `src/SavegameBunker/ViewModels/BulkRunRules.cs:7-9, 20-29`, `src/SavegameBunker/ViewModels/MainViewModel.cs:895-897, 1519-1520, 1636-1637`
**Issue:** `NeedsBackup` says "exactly `SavesChangedSinceBackup` and `NeverBackedUp`" and lists four
excluded kinds (six of the seven verdicts), the class summary calls it the "unsecured verdict pair",
and `GetUnsecuredRows` / `BackupAllChangedAsync` repeat "exactly the two verdicts". The code
matches three kinds. The same drift exists in `BackupComparison.cs` ("five verdicts") and
`BackupComparer.cs:17-20` ("sixth kind"). Per CLAUDE.md, doc comments explain behaviour, and this
one now explains the wrong set.
**Fix:** Update the comments to the three-kind set and name `LastBackupPartial` in the exclusion
reasoning.

### IN-03: `-partial`, `-N` and `-pre-restore` archives take their `TakenAt` from the file's mtime, not the name

**File:** `src/SavegameBunker/Services/BackupService.cs:511-518`
**Issue:** `ParseTakenAt` requires the whole name to be a bare timestamp. Every archive carrying a
suffix, now including all `-partial` ones, falls back to `File.GetLastWriteTime`, even though the
name already holds the exact timestamp. If the backup folder is copied or synced by a tool that
does not preserve mtimes, a partial archive's `TakenAt` jumps to the copy time. `BackupComparer`
then orders it above genuinely newer archives and judges `LastBackupPartial` / `UpToDate` off it.
The fallback was written for hand-renamed files, not for the app's own names.
**Fix:** When `IsOwnArchiveFileName(Path.GetFileName(filePath))` holds, parse the 17-character
prefix; keep the mtime fallback for everything else.

### IN-04: Three new string keys sit under the "Offline user guide outcomes" comment

**File:** `src/SavegameBunker/Themes/Strings.en.xaml:66-72`
**Issue:** `str_write_idTaken`, `str_row_busy` and `str_catalog_busy` were inserted between
`str_help_opened_online` and `str_help_failed`, under a comment that says the block is about the
user guide. They belong to the catalog-write and row-busy messages. It also splits the help group.
**Fix:** Move them next to the catalog/row messages and restore the help keys to one contiguous
block.

### IN-05: Impossible-state guard: `try/catch` around `Directory.Exists` in `IsBackupRootReachable`

**File:** `src/SavegameBunker/Services/BackupService.cs:376-380`
**Category:** necessity
**Issue:** `Directory.Exists` is documented to return `false` for any failure (invalid path,
permissions, network error) and never to throw, so the `catch { return false; }` can never run. It
exists to honour the never-throw service contract, which `Directory.Exists` already satisfies by
itself. The justification rests on the BCL documentation, not on anything provable from this
repository, so this is Info.
**Fix:** Return `Directory.Exists(backupRoot)` directly and keep the contract note in the summary.

### IN-06: Impossible-state guard: `default:` arms duplicating the `Checking` case (carried over from the previous review)

**File:** `src/SavegameBunker/ViewModels/GameRowViewModel.cs:205-212`, `:280-284`
**Category:** necessity
**Issue:** Both switches already name every member of their enum. `SaveStatusKind` values come only
from `SaveStatus`'s four factories, and `BackupComparisonKind` values only from `BackupComparison`'s
factories. A repo-wide search finds no cast, parse or deserialization that builds either enum, so
`default:` can never run. A defensive `default` on an enum switch is also conventional, so this is
Info rather than a Warning.
**Fix:** Drop `default:` (keeping `case ...Checking:` alone), or leave a one-line comment recording
the closed-set invariant.

### IN-07: No test pins the new sweep behaviour that makes the backup-root-aware scan and memoized advisory work

**File:** `tests/SavegameBunker.Tests/StatusSweepCoordinatorTests.cs:62-109, 136-164`
**Issue:** The coordinator now passes `hasRoot ? root : null` into `SaveStatusScanner.Scan`, switched
to `TryListBackups(root, row.Game, ...)`, and memoizes `ComputeSaveDataSize` per game in
`ComputeAdvisoryAsync`. The tests here never put a backup folder inside a save folder. They also
never put the same game in both `selectedGames` and `changedGames`, nor use a non-null root. A
regression that dropped the root argument (so every fresh archive again reads "saves changed") or
the memoization would pass. `BackupServiceTests` covers the scanner and size paths directly, but not
through the coordinator.
**Fix:** Add one `WalkAsync` test over a backup folder nested in the save folder asserting the row
settles `UpToDate` after a backup. Add one `ComputeAdvisoryAsync` test with a game in both lists and
a non-null root.

---

_Reviewed: 2026-11-25T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
