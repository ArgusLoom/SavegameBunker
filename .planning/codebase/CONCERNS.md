---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
# Codebase Concerns

**Analysis Date:** 2026-11-17

## Tech Debt

**Oversized view model:**
- Issue: `MainViewModel` carries most of the application's orchestration (catalog load, status sweeps, backup/restore commands, unsecured-game prompt, dialog routing). At 1933 lines it is the single largest file in the repo.
- Files: `src/SavegameBunker/ViewModels/MainViewModel.cs`
- Impact: Changes to one command risk side effects on unrelated state; the file is hard to review and hard to test in isolation. Sibling classes (`CatalogSession`, `GameActions`, `GameRowViewModel`) already exist and absorb some of this, so the split is partial.
- Fix approach: Continue extracting cohesive units (status sweep, unsecured-prompt logic, command wiring) into the existing `ViewModels/` helpers; keep the public bindings on `MainViewModel` stable so XAML does not change.

**Hand-maintained known-games table:**
- Issue: `src/SavegameBunker/Data/known-games.json` is a static snapshot (~286 titles) that is embedded in the assembly and goes stale as games move their save locations.
- Files: `src/SavegameBunker/Data/known-games.json`, `src/SavegameBunker/Services/KnownGamesCatalog.cs`, `src/SavegameBunker/Services/KnownGamesScanner.cs`
- Impact: Users see "not found" or wrong paths for newer or relocated titles with no in-app signal that the table is old.
- Fix approach: Already recorded in `.planning/WISHLIST.md` (online-sourced table with bundled fallback). Until then, record the table's snapshot date in the file and show it in the Find games dialog.

**Parallel string tables with no coverage tooling:**
- Issue: UI text lives in both `src/SavegameBunker/Themes/Strings.en.xaml` and the code-side `src/SavegameBunker/Services/Strings.cs`, and call sites mix `Strings.Get`/`Strings.Format` with literal English messages (for example in `RestoreService.cs`, `BackupService.cs`).
- Files: `src/SavegameBunker/Services/Strings.cs`, `src/SavegameBunker/Themes/Strings.en.xaml`, `src/SavegameBunker/Services/RestoreService.cs`, `src/SavegameBunker/Services/BackupService.cs`
- Impact: Some messages cannot be localized without touching code, and the mixed approach makes it unclear which path a new message should use.
- Fix approach: Route all user-facing text through `Strings`; `tests/SavegameBunker.Tests/ResourceKeyTests.cs` and `StringsTests.cs` already guard keys and can be extended to catch unused or missing keys.

## Known Bugs

**Orphaned restore "old" directories are never reclaimed automatically:**
- Symptoms: After a restore whose final swap fails and the rollback also fails, or whose best-effort delete of the aside directory fails, a `<save>.savegamebunker-restore-old-<guid>` directory containing the user's previous live save data remains next to the save folder. Disk use grows silently.
- Files: `src/SavegameBunker/Services/RestoreService.cs` (`TryDeleteDirectory(oldAsideDir)`, the catch around the second `Directory.Move`), `CleanStaleRestoreTemp`
- Trigger: A locked file inside the save folder during the aside rename or the delete, or a sync client holding the directory.
- Workaround: None in-app. The file is kept deliberately (see the remarks on `CleanStaleRestoreTemp`), so removing it by hand is the only path. The app never tells the user it exists.
- Fix approach: Surface leftover `-old-` directories in the Restore picker or the health line so the user can decide, rather than auto-deleting them.

**Temp-file sweep can remove another process's in-flight staging file:**
- Symptoms: Two copies of the app writing into the same backup folder can delete each other's `*.zip.tmp` staging file, making one backup fail with a file-not-found error.
- Files: `src/SavegameBunker/Services/BackupService.cs` (`CleanStaleTempFiles`, which enumerates `*.zip.tmp` in `targetDir` with no age or ownership check)
- Trigger: Running two instances against the same `backupRoot`, or a second instance started while a backup runs.
- Workaround: Do not run two instances against one backup folder.
- Fix approach: Only sweep staging files older than a threshold (for example one hour), or name staging files with the owning process id and skip live ones.

**Restore verification does not detect bit-level corruption:**
- Symptoms: A zip with a damaged entry body but intact central directory passes verification and is restored as-is.
- Files: `src/SavegameBunker/Services/BackupService.cs` (`DescribeVerificationFailure`), `src/SavegameBunker/Services/RestoreService.cs`
- Trigger: Bit rot or a damaged transfer to a USB drive.
- Workaround: None in-app. Documented as an accepted limitation in `SPEC.md` *Known limits* and `.planning/WISHLIST.md`. The reason is .NET 10 not validating CRC-32 on read.
- Fix approach: Tracked in WISHLIST; take it when retargeting to .NET 11.

## Security Considerations

**Unsigned release binaries:**
- Risk: Users receive a `.exe` whose origin cannot be verified by Authenticode; SmartScreen may warn on first run.
- Files: `.github/workflows/release.yml` (computes `sha256sum` into a `.sha256` sidecar only; no signing step is present)
- Current mitigation: SHA-256 sidecar published with the zip.
- Recommendations: Add Authenticode signing in `release.yml` when a certificate is available; until then, document checksum verification in `README.md`.

**Path template expansion trusts the catalog:**
- Risk: `games.json` is user-editable and its `saveDir` templates are expanded through `PathResolver`, then read and deleted-around by backup and restore. A template pointing at a broad folder (for example `%USERPROFILE%`) will back up or, through restore, replace far more than intended.
- Files: `src/SavegameBunker/Services/PathResolver.cs`, `src/SavegameBunker/Services/GameCatalog.cs`, `src/SavegameBunker/Services/RestoreService.cs`
- Current mitigation: The backup folder may not sit inside the save folder during restore (`BackupService.IsSameOrInside`); reparse points are skipped during archiving.
- Recommendations: Validate that the resolved save folder is not a drive root or a known-folder root before restore, and show the resolved path in the confirmation prompt (`RestorePrompt` already carries it).

**Junction and reparse-point handling is manual:**
- Risk: Archive and scan walks must skip reparse points; the code does this by hand in several places, so a missed walk could follow a junction into Documents or SSH keys.
- Files: `src/SavegameBunker/Services/BackupService.cs` (own walk, `excludedDirs`), `src/SavegameBunker/Services/ReparseFile.cs`, `src/SavegameBunker/Services/SaveStatusScanner.cs`, `src/SavegameBunker/Services/KnownGamesScanner.cs`
- Current mitigation: Own walk in `ArchiveSaveDir`, `ReparseFile.HasAnyFile`.
- Recommendations: Centralise the walk into one enumerator used by backup, scan and status so the rule is written once.

## Performance Bottlenecks

**Full-tree walks on every status sweep:**
- Problem: Each status sweep enumerates every game's save folder to decide "changed since backup".
- Files: `src/SavegameBunker/Services/SaveStatusScanner.cs`, `src/SavegameBunker/ViewModels/MainViewModel.cs`
- Cause: No cached mtime or manifest; the check is recomputed from the filesystem every time.
- Improvement path: Cache the newest write time per game and invalidate on the catalog change or a backup; keep the full walk for explicit refreshes only.

**Whole-archive verification doubles backup I/O:**
- Problem: Every backup reopens the staged zip and re-walks the save folder before it is accepted.
- Files: `src/SavegameBunker/Services/BackupService.cs` (`DescribeVerificationFailure`)
- Cause: Verification is the only integrity check available on .NET 10 (see Known Bugs above).
- Improvement path: Keep verification; accept the cost explicitly in `docs/DECISIONS.md`. Cancellation of long backups is noted in `.planning/WISHLIST.md`.

## Fragile Areas

**Restore swap sequence:**
- Files: `src/SavegameBunker/Services/RestoreService.cs`
- Why fragile: The order safety backup, extract to temp, verify, rename aside, rename in, delete aside is load-bearing per `CLAUDE.md`. Each step has its own failure branch, and a failure in the middle depends on the rollback catch being correct.
- Safe modification: Do not reorder steps or add early returns between the aside rename and the final rename. Any new step must go before the aside rename.
- Test coverage: `tests/SavegameBunker.Tests/RestoreServiceTests.cs` covers the main paths; the double-failure rollback (second move fails and the rollback also fails) is not covered.

**Dialog marshalling from background threads:**
- Files: `src/SavegameBunker/Services/DialogService.cs` (lines around 128-139 use `GetAwaiter().GetResult()` and lines around 286 use `Dispatcher.Invoke`)
- Why fragile: Services call the dialog service from thread-pool threads; a blocking call that waits on the UI thread can deadlock if the UI thread is itself waiting on that background work. The current design relies on the dialog marshalling never running on a thread that is blocked on the same work.
- Safe modification: Do not add a synchronous wait inside any code path that the UI thread can await. Prefer async dialog methods.
- Test coverage: `DialogService` has no unit tests (it is WPF-bound); behaviour is only exercised through view-model tests that substitute a fake `IDialogService`.

**Async void startup:**
- Files: `src/SavegameBunker/App.xaml.cs` (`protected override async void OnStartup`)
- Why fragile: An exception escaping an `async void` method crashes the process without the normal WPF exception path. The method does wrap its body in try/catch, so the risk is limited to code added outside that block.
- Safe modification: Keep all new startup work inside the existing try block.

**Catalog cross-process guard:**
- Files: `src/SavegameBunker/Services/GameCatalog.cs` (`expectedLastWriteTimeUtc` check before `File.Replace`)
- Why fragile: The guard compares a file timestamp, not content, so a sync client touching the file can cause a false refusal, and FAT32's 2-second resolution can miss a collision. Recorded as an accepted limitation in `.planning/WISHLIST.md`.
- Safe modification: Do not remove the timestamp check; any change must keep the "refuse, do not overwrite" behaviour required by `CLAUDE.md`.

**Local wall-clock backup names:**
- Files: `src/SavegameBunker/Services/BackupService.cs` (`DateTime.Now.ToString("yyyy-MM-dd_HHmmss")`), comparison in `src/SavegameBunker/Services/BackupComparer.cs`
- Why fragile: Names and comparisons use local time, so a DST fall-back or a clock change can produce misordered or colliding archive names. The collision case surfaces as an error rather than data loss, by design.
- Safe modification: Keep the two-argument `File.Move` so collisions fail loudly.

## Scaling Limits

**Backup folder growth:**
- Current capacity: Every backup is a full zip; no retention limit exists in code.
- Limit: Bounded only by free space on the backup volume.
- Scaling path: Retention cap per game (last N or by age) with a prune UI, as listed in `.planning/WISHLIST.md`. Archive size is already capped on restore (`MaxArchiveEntries`, `MaxArchiveUncompressedBytes` in `RestoreService.cs`) but not on backup.

## Dependencies at Risk

**WPF-UI pinned at 4.3.0:**
- Risk: The shell library owns the High Contrast dictionary and dark-only theme behaviour; upgrades change visual output.
- Impact: Dark-only high-contrast support and the light-theme toggle rely on library internals (see `App.xaml.cs` high-contrast accent repointing).
- Migration plan: Pin and test visually on each bump; the `.github/dependabot.yml` configuration will propose updates, so review them against the high-contrast path before merging.

## Missing Critical Features

**No in-app visibility of backup failures over time:**
- Problem: A failed startup prompt or failed scheduled backup is reported once in a dialog and not persisted.
- Blocks: Diagnosing "it stopped backing up" reports. `.planning/WISHLIST.md` lists file logging as the first diagnostics item.

## Test Coverage Gaps

**Restore rollback path:**
- What's not tested: The case where the second rename (temp to save folder) fails and the rollback rename of the aside directory also fails.
- Files: `src/SavegameBunker/Services/RestoreService.cs`, `tests/SavegameBunker.Tests/RestoreServiceTests.cs`
- Risk: The user's live save folder can end up missing with the data only in an `-old-` directory, unnoticed.
- Priority: High

**Concurrent backup folder use:**
- What's not tested: Two service instances writing to one `backupRoot` at once, including the `*.zip.tmp` sweep.
- Files: `src/SavegameBunker/Services/BackupService.cs`, `tests/SavegameBunker.Tests/BackupServiceTests.cs`
- Risk: Silent interference described in Known Bugs above.
- Priority: Medium

**WPF-bound code:**
- What's not tested: `DialogService`, `App.xaml.cs` startup sequence, `Behaviors/CornerClip.cs`, and the views under `src/SavegameBunker/Views/`.
- Files: `src/SavegameBunker/Services/DialogService.cs`, `src/SavegameBunker/App.xaml.cs`, `src/SavegameBunker/Behaviors/CornerClip.cs`
- Risk: Marshalling or startup regressions reach users before the suite catches them.
- Priority: Medium

---

*Concerns audit: 2026-11-17*
