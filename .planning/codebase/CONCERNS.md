---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# Codebase Concerns

**Analysis Date:** 2026-11-18

## Tech Debt

**MainViewModel is still a large class:**
- Issue: The view model is ~1,500 lines and still owns the bulk-progress state, the guard flags, the startup prompt and most command wiring. The Phase 1 extractions (`BulkBackupCoordinator`, `StatusSweepCoordinator`, `RowActionsCoordinator`) took part of it out, but the rest has not been split.
- Files: `src/SavegameBunker/ViewModels/MainViewModel.cs`, `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs`, `src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs`, `src/SavegameBunker/ViewModels/RowActionsCoordinator.cs`
- Impact: Guard state is spread across four `[NotifyCanExecuteChangedFor]` lists that already differ (`BulkBackupTotal` 7 commands, `IsBackupRunning` 5, `IsRestoreRunning` 5, `IsScanRunning` 1) plus hand-written `RequeryOperationGates` calls at `MainViewModel.cs:128-130`, `:191-193`, `:197-201`, `:213-214`. Every new command has to be added to the right list by hand.
- Fix approach: Backlog items 14 and 15 in `.planning/ROADMAP.md`: one `RequeryOperationGates()` method and one bulk-progress callback type (`Action<int,string>`). Do this after the defect fixes in Phase 2.

**Duplicated save-folder resolution in dialogs:**
- Issue: `AddGameDialogViewModel` and `ChangeSaveFolderDialogViewModel` each carry their own copy of the template-to-folder resolution logic.
- Files: `src/SavegameBunker/ViewModels/ChangeSaveFolderDialogViewModel.cs` (lines ~58-102), `src/SavegameBunker/ViewModels/AddGameDialogViewModel.cs` (lines ~187-195, ~379-407)
- Impact: About 40 duplicated lines. A fix to one path can easily miss the other.
- Fix approach: One pure static `ResolveFolder` helper (backlog item 16).

**Silent catch-all handlers and debug switches in the composition root:**
- Issue: `App.OnStartup` is an `async void` method. It is wrapped in a last-resort try/catch, and `#if DEBUG` self-test switches (`--selftest-*`, `RunCrashSelfTest`) live in production source.
- Files: `src/SavegameBunker/App.xaml.cs` (`OnStartup` around line 131; self-test filter and `RunCrashSelfTest` call near lines 150-215)
- Impact: Low on its own. Exceptions escaping the `async void` body are only caught by the outer try, so new code added after the first `await` must keep that in mind.
- Fix approach: Keep the try/catch as the only route. Do not add work after `await vm.ReportLoad()` outside it.

**Catalog rewrite drops hand-written comments:**
- Issue: `GameCatalog.SaveAsync` serializes the whole `Catalog` object. The seeded template tells the user that "adding a game from the application rewrites this file, and comments do not survive that."
- Files: `src/SavegameBunker/Services/GameCatalog.cs` (`SaveAsync`, around lines 187-240; `SeedTemplate` around line 330+)
- Impact: A user who annotated `games.json` by hand loses those notes the first time they add a game through the UI. This sits against the CLAUDE.md rule not to overwrite a file the user may have hand-edited. The rule is about a malformed or absent file, so this is a documented trade-off rather than a violation, but it is easy to miss.
- Fix approach: Either document it more prominently in `README.md` and the user guide, or write the file without touching unrelated text (larger change).

## Known Bugs

**Save subfolders are silently left out of a backup when the save folder sits inside the backup root:**
- Symptoms: Only top-level files of the save folder are archived. Subfolders are skipped and nothing is reported. A later restore replaces the whole save folder with that archive, so the subfolders are lost. The safety backup taken before the restore has the same gap.
- Files: `src/SavegameBunker/Services/DirectoryWalk.cs` (exclusion check around lines 106-108), `src/SavegameBunker/Services/BackupService.cs` (`excludedDirs` at line ~116)
- Trigger: Backup root `Documents`, save folder `Documents\My Games\X`. Every subfolder of `X` is inside an excluded directory and gets pruned by the "sub inside excluded" branch.
- Workaround: Move the backup root out of the save folder's ancestry.
- Planned fix: Backlog item 0 (`ROADMAP.md`). Pruning must keep only folders that are or contain the backup root, or that are or lie inside a per-game archive folder. Excluded folders must be reported as an informational line, not as unreadable paths.

**Restore can delete archives when the game's archive folder is the save folder:**
- Symptoms: Restore checks only whether the backup root is inside or equal to the save folder (`RestoreService.cs:68`). It does not check the per-game archive folder `<backupRoot>\<id>`. When that folder is or lies inside the save folder, the swap deletes every archive of the game, including the safety backup written just before.
- Files: `src/SavegameBunker/Services/RestoreService.cs` (guard around line 68; old-directory aside and delete around lines 140-205)
- Trigger: Backup root `D:\B`, save folder `D:\B\<id>` (or a parent of it, under the backup root).
- Workaround: None inside the app. Keep the save folder and the archive folder apart.
- Planned fix: Backlog item 0 / decision D-11 in the Phase 2 context. Refuse restore for that layout and add a test that every archive survives the refused restore. Backup in that layout stays allowed.

**Row action can run beside "Back up selected" while a folder probe is open:**
- Symptoms: `RunBulkBackupAsync` awaits the backup-root probe and picker (`EnsureBackupRootAsync`) before raising any guard. Row Backup, Refresh and Change backup folder stay enabled during that window. A row backup can then run alongside the bulk loop for the same game. Each run's `CleanStaleTempFiles` deletes the other's in-flight `.zip.tmp`, and the bulk `finally` clears that row's `IsBusy` early.
- Files: `src/SavegameBunker/ViewModels/MainViewModel.cs` (`RunBulkBackupAsync` around line 1330; `EnsureBackupRootAsync` around line 1460), `src/SavegameBunker/ViewModels/RowActionsCoordinator.cs` (guard pattern at line ~69), `src/SavegameBunker/Services/BackupService.cs` (`CleanStaleTempFiles`, lines ~429-446)
- Trigger: Click "Back up selected" with no backup root set, then click a row's Backup while the folder picker is open.
- Workaround: Pick the folder before starting bulk work.
- Planned fix: Backlog item 2. Raise a guard before the await, counted in `IsAnyOperationRunning`.

**Startup unsecured-games prompt can be lost until the next full sweep:**
- Symptoms: The status sweep starts in the `MainViewModel` constructor (`MainViewModel.cs:150`). The prompt is armed only after `await vm.ReportLoad()` in `App.OnStartup`. When a backup root is configured, `ReportLoad` awaits `Task.Run(() => _backup.IsBackupRootReachable(root))` (`MainViewModel.cs:721-722`). The sweep can settle during that await, before the prompt is armed. The prompt then fires on the next Refresh, mid-session.
- Files: `src/SavegameBunker/App.xaml.cs` (`RequestStartupUnsecuredPrompt` call near line 206), `src/SavegameBunker/ViewModels/MainViewModel.cs`, `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs`
- Trigger: Normal start with a configured backup root.
- Planned fix: Backlog item 1. Record "first full sweep settled" and run the check immediately on arming if it already settled. This changes the test contract, since those tests currently arm after the sweep.

**Network drives block the start-folder probe:**
- Symptoms: `DriveHints.SuggestBackupStartFolder` reads `drive.IsReady` for every drive, including mapped network drives. The code comment says network drives are excluded because `IsReady` blocks, but the probe reads it first anyway. Opening the backup-folder picker can hang on an unreachable share.
- Files: `src/SavegameBunker/Services/DriveHints.cs` (tuple construction at line ~71), `tests/SavegameBunker.Tests/DriveHintsTests.cs`
- Trigger: A disconnected mapped drive is present.
- Planned fix: Backlog item 3. Check `DriveType` before `IsReady`, and inject the drive probe as a delegate seam for tests (no new dependency).

**Crash dialog at last-resort startup shows a full stack trace:**
- Symptoms: If startup fails before the main window exists, the message box shows `{ex}`, which includes the stack trace. The rest of the app uses `CrashReport.Describe`, which deliberately never shows stack traces.
- Files: `src/SavegameBunker/App.xaml.cs` (catch block near lines 212-217), `src/SavegameBunker/Services/CrashReport.cs`
- Trigger: A failure in `GameCatalog.ResolvePath`, `LoadAsync` or window construction.
- Workaround: None; the dialog text can be copied for a bug report.
- Fix approach: Use `CrashReport.Describe(ex, "starting SavegameBunker")` for the body, and keep the full exception only in an opt-in log (no logging library is allowed, so a plain file write or the clipboard).

**Empty backup archive when the save folder holds only excluded content:**
- Symptoms: `ReparseFile.HasAnyFile` walks without the backup exclusions. A save folder whose only files sit in a nested backup folder passes the emptiness check, and a zero-entry archive is written and verifies (0 equals 0).
- Files: `src/SavegameBunker/Services/BackupService.cs` (`InspectSaveDir` at line ~382), `src/SavegameBunker/Services/ReparseFile.cs`
- Trigger: Save folder containing only `<backupRoot>\...` content.
- Planned fix: Backlog item 5. Fail when `ArchiveSaveDir` collects zero entries; test first.

## Security Considerations

**Hand-edited catalog can point a restore at a very broad folder:**
- Risk: Restore renames the resolved save folder aside and then deletes it after a successful swap. `GameCatalog.Validate` (lines ~259-313) checks only that `id`, `displayName` and `saveDirTemplate` are present and that ids are unique. It does not reject a template that resolves to a drive root, the user profile, `%DOCUMENTS%` or `%SAVEDGAMES%`. A hand-edited entry such as `"saveDirTemplate": "%DOCUMENTS%"` would make restore move the whole Documents folder aside and delete it. The "shared template" refusal exists only in the scanner (`KnownGamesScanner.SharedTemplates`), not in the catalog load path.
- Files: `src/SavegameBunker/Services/GameCatalog.cs`, `src/SavegameBunker/Services/RestoreService.cs`, `src/SavegameBunker/Services/PathResolver.cs`
- Current mitigation: The safety backup runs before the swap, so the data is still in the archive. The archive itself would be very large, and the deletion is still broad.
- Recommendations: Warn (not refuse, per CLAUDE.md's backup-destination rule, which covers destinations only) when a save folder resolves to a drive root or a whole known folder. Keep the restore order unchanged.

**Explorer fallback builds its argument string by hand:**
- Risk: `Process.Start("explorer.exe", $"/select,\"{path}\"")` concatenates the path into the command line. A path containing a double quote would split arguments.
- Files: `src/SavegameBunker/Services/DialogService.cs` (around line 230)
- Current mitigation: `ShellOpen.Classify` runs before the shell-execute branch. The Explorer fallback is reached only when no `.json` handler is registered. Paths come from the user's own catalog and folders.
- Recommendations: Pass the path through `ProcessStartInfo.ArgumentList` (`/select,` then the path as one element) or reject paths containing `"`.

## Performance Bottlenecks

**Every backup reads the save data several times:**
- Problem: A backup walks the save folder and hashes each file as it is copied. It then reopens the finished archive, decompresses every entry, and hashes it again. The comparison is name, length and hash against the source (`BackupService.cs`, the content check around lines 500-570). `ComputeSaveDataSize` and the status sweep add further walks.
- Files: `src/SavegameBunker/Services/BackupService.cs`, `src/SavegameBunker/Services/SaveStatusScanner.cs`, `src/SavegameBunker/Services/DirectoryWalk.cs`
- Cause: Verification is deliberate (the archive is the only copy, and .NET 10 does not check CRC on read). The cost is most visible on slow USB drives and network shares.
- Improvement path: Cancellation at stage boundaries (wishlist), and a single hash pass when the archive is rebuilt. Keep the verification; it is what makes the "safe" claim true.

**Unbounded growth of backup archives:**
- Problem: Each backup is a full zip. Nothing caps the number of archives per game, and the only removal is the single-archive delete in the restore picker.
- Files: `src/SavegameBunker/Services/BackupService.cs` (`ListBackups` around line 251, `DeleteArchive` around line 320)
- Cause: No retention policy exists. `.planning/WISHLIST.md` lists it as Tier 1.
- Improvement path: Retention by count or age, with an explicit UI. A future setting would need the `settings.json` decision first.

**Recursive directory walk without depth limit:**
- Problem: `DirectoryWalk.ForEachFile` recurses once per directory level. Reparse-point folders are skipped, so cycles are avoided, but extremely deep trees could still exhaust the stack.
- Files: `src/SavegameBunker/Services/DirectoryWalk.cs` (`Walk` recursion, lines ~52-110)
- Cause: Simplicity. Save trees are normally shallow.
- Improvement path: Convert to an explicit stack if a real case appears.

## Fragile Areas

**Restore swap sequence:**
- Files: `src/SavegameBunker/Services/RestoreService.cs` (lines ~84-205)
- Why fragile: The order is load-bearing: safety backup, extract to a temp sibling, verify, rename the old directory aside, rename the new one in, then delete the aside. Each step has its own rollback. A failure where the old directory is renamed aside and cannot be put back leaves `<saveDir>.savegamebunker-restore-old-<guid>` beside the save folder. The restore comment says the aside is "never auto-deleted", so these folders accumulate. Nothing lists or cleans them except a best-effort `CleanStaleRestoreTemp` for temp directories.
- Safe modification: Do not reorder steps and do not add a recursive delete before the swap (CLAUDE.md). Add tests for each rollback branch before changing any of them.
- Test coverage: `tests/SavegameBunker.Tests/RestoreServiceTests.cs` covers the normal path. The double-failure branch (aside cannot be restored) and the archive-folder-inside-save-folder case (D-11) need explicit tests.

**Cross-process catalog guard uses file timestamps:**
- Files: `src/SavegameBunker/Services/GameCatalog.cs` (`SaveAsync` comparison around lines 195-199), `src/SavegameBunker/ViewModels/CatalogSession.cs` (line ~63-73)
- Why fragile: `SaveGate` serializes writers only within one process. Two app instances (or a sync client touching the file) are caught only by comparing the last-write time. FAT32 has 2-second resolution, so a collision can be missed, and a touch can cause a false "changed on disk" refusal.
- Safe modification: Keep the timestamp check and the error message. A content hash plus a session lock would be the real fix (wishlist, accepted limitation).
- Test coverage: `tests/SavegameBunker.Tests/GameCatalogTests.cs` and `CatalogSessionTests.cs` use `Task.Delay(20)` to get distinct timestamps. That is timing-dependent on some volumes.

**Walk skips reparse points and symlinks without reporting them:**
- Files: `src/SavegameBunker/Services/DirectoryWalk.cs` (symlink `continue` around lines 65-66; reparse-point subfolder `continue` around lines 103-104)
- Why fragile: A junction or symbolic link inside a save folder is dropped from the backup with no warning, unlike unreadable paths, which are reported through `onSkipped`. OneDrive placeholder files are reparse points too (see `ReparseFile.cs`), so a file the user can see may be silently absent from the archive.
- Safe modification: Report these through the same channel as other deliberate exclusions (the informational line from backlog item 0) so the backup result explains the omission.
- Test coverage: `tests/SavegameBunker.Tests/BackupServiceTests.cs` covers the reparse-skip behaviour but not the reporting.

**Stale-temp cleanup can delete another run's file:**
- Files: `src/SavegameBunker/Services/BackupService.cs` (`CleanStaleTempFiles` at lines ~429-446, called from line ~97)
- Why fragile: Every `*.zip.tmp` in the game's backup folder is deleted at the start of a backup. Two backups of the same game (see the row-action concern above) can remove each other's staging file.
- Safe modification: Only remove `.zip.tmp` files older than a threshold, or guard with the per-game busy flag (backlog item 2).

**Dialog confirm path blocks a background thread on the UI dispatcher:**
- Files: `src/SavegameBunker/Services/DialogService.cs` (`Confirm`, around lines 118-139)
- Why fragile: The background branch waits synchronously on `InvokeAsync(...).Task.Unwrap()`. That is correct only while every caller runs under `Task.Run`. The UI-thread branch calls `GetAwaiter().GetResult()` on the dialog task, which would deadlock if ever reached. All current callers avoid it, but nothing enforces that.
- Safe modification: Keep callers on background threads. Backlog item 11 asks for the UI-thread branch to be removed or asserted. The restore picker's delete is constructed from `DialogService` with `new BackupService()` at line ~191 instead of an injected delegate, which bypasses the view model's seam.

**Strings must be preloaded on the UI thread:**
- Files: `src/SavegameBunker/App.xaml.cs` (`Strings.Preload()` call at the start of `OnStartup`), `src/SavegameBunker/Services/Strings.cs` (static `Dictionary` at line ~45)
- Why fragile: The string table is loaded lazily from XAML. Four status-sweep worker threads can reach it at once. The `App.OnStartup` comment records what went wrong without the preload.
- Safe modification: Keep the preload call first in `OnStartup`. Any new entry point (for example a test host or a second window opened before startup) must preload too.

**Dark-only high-contrast support:**
- Files: `src/SavegameBunker/App.xaml.cs` (high-contrast branch, around lines 139-160)
- Why fragile: `ApplicationTheme.HighContrast` applies one dark dictionary. A light high-contrast scheme still produces a dark window. Documented in `README.md` and in the code comment.
- Safe modification: Accept the limitation or replace the shell's high-contrast dictionary (a separate piece of work, wishlist).

## Scaling Limits

**Single-process write serialization:**
- Current capacity: One writer at a time per process (`SemaphoreSlim(1, 1)` in `GameCatalog.cs:43`).
- Limit: Two processes writing the same catalog rely on timestamp checks only.
- Scaling path: Content hash and a merge or lock strategy (wishlist).

**Backup throughput on removable media:**
- Current capacity: One full archive per game per backup, written sequentially with a verification read-back.
- Limit: Long bulk backups hold the UI busy state until finished; there is no cancellation (`.planning/WISHLIST.md`, Tier 2).
- Scaling path: Cooperative cancellation through the existing progress channel, limited to stage boundaries for restore.

## Dependencies at Risk

**WPF-UI 4.3.0 and CommunityToolkit.Mvvm 8.4.2 (the only shipping packages):**
- Risk: The app relies on WPF-UI resolving its own resource dictionaries reflectively. The csproj comments say trimming is therefore off. Upgrading WPF-UI may change the `PaletteRed*` keys the app overrides (backlog item 21 renames them to `SavegameBunker*`).
- Impact: A WPF-UI upgrade could silently change colours or break the dark high-contrast fallback.
- Migration plan: Pin the version (already pinned), and check `Themes/Styles.xaml` and `App.xaml` for keys the library now defines before upgrading.

**Target framework on .NET 10 LTS:**
- Risk: `ZipArchive` in .NET 10 does not validate CRC-32 on read; this app compensates with its own hash comparison. .NET 11 changes this behaviour.
- Impact: Moving to .NET 11 would make the verification double-check redundant, and may change exception types the code catches.
- Migration plan: Revisit `BackupService` verification and `RestoreService` verification when retargeting (wishlist, opportunistic).

## Missing Critical Features

**No backup-retention policy:**
- Problem: Archives grow without bound; no per-game cap or age-based prune exists.
- Blocks: Long-term use on small removable drives.

**No cancellation for backup or restore:**
- Problem: A long bulk operation owns the app until it finishes.
- Blocks: Stopping a backup started by mistake on a slow drive.

**No file logging:**
- Problem: Unexpected failures are shown to the user through `CrashReport` and then gone. There is no record to attach to a bug report.
- Blocks: Diagnosing issues reported after the fact (wishlist DIAG-01..02).

## Test Coverage Gaps

**Tests that depend on real drives or timing:**
- What's not tested: Startup and backup-folder picker logic with a controlled drive list.
- Files: `tests/SavegameBunker.Tests/StartupUnsecuredPromptTests.cs` (test `Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op`, around line 274 — drives a state the app cannot reach), `tests/SavegameBunker.Tests/MainViewModelTests.cs` (`A_cancelled_first_backup_folder_prompt_writes_nothing`), `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs` (`Task.Delay(300)` at line ~560), `tests/SavegameBunker.Tests/MainViewModelTests.cs` (`Thread.Sleep(5)` at line ~2488)
- Risk: Two tests fail under load at the 3-second `WaitForAsync` timeout. Raising the timeout hides the cause.
- Priority: High (the flake blocks CI; backlog item 3 and D-05).

**Restore guard and rollback branches:**
- What's not tested: The archive-folder-inside-save-folder layout (D-11), and the branch where the old directory cannot be renamed back after a failed swap.
- Files: `src/SavegameBunker/Services/RestoreService.cs`, `tests/SavegameBunker.Tests/RestoreServiceTests.cs`
- Risk: A future change to the guard could delete every archive of a game, including the safety backup.
- Priority: High.

**Reparse-point and symlink handling in backup:**
- What's not tested: That a skipped junction or symlink is reported to the user; that a save folder holding only excluded content is refused as empty.
- Files: `src/SavegameBunker/Services/DirectoryWalk.cs`, `src/SavegameBunker/Services/BackupService.cs`, `tests/SavegameBunker.Tests/BackupServiceTests.cs`
- Risk: Silent data omission in backups.
- Priority: Medium.

**Old-format verification helper used only by tests:**
- What's not tested: `DescribeVerificationFailure` has a name-and-length-only mode that exists only for tests (`BackupServiceTests.cs:272-322`). Production never exercises that mode.
- Files: `src/SavegameBunker/Services/BackupService.cs` (`DescribeVerificationFailure`)
- Risk: The tests verify a code path production does not use.
- Priority: Low (backlog item 7).

---

*Concerns audit: 2026-11-18*
