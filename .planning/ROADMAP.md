# Roadmap: SavegameBunker

## Milestones

- ✅ **v1.0 — Initial Release** — shipped 2026-11-17
- ✅ **v1.0.1 — Hardening & Refactoring** — released

## v1.0 — Initial Release

A self-contained Windows desktop app that backs up PC game save data to a folder the user
chooses and restores it again: catalog management, a known-games scan, bulk backup/restore with
selection and search, startup health checks, and keyboard/text-scaling accessibility. Full detail
in `CHANGELOG.md`'s `[1.0.0]` entry.

## v1.0.1 — Hardening & Refactoring

Behavior-preserving hardening of the v1.0 codebase: the post-release audit rounds (R1/R2,
2026-11-17) produced a fix batch already merged; this milestone completes the remaining
audit-derived defects and pays down the single structural debt item — `MainViewModel.cs`
(1,885 lines) — via an incremental, behavior-locked split.

**Goal:** Same behavior everywhere (the 629-test suite stays green, verified on Windows),
remaining R2 defects closed, and `MainViewModel` reduced to a composition root of sealed
coordinators with no new dependencies and no abstraction beyond the named seams.

## Phases

### Phase 1: Bugfixing & Refactoring

**Goal:** Close the remaining R2 audit defects and split `MainViewModel.cs` (1,885 lines)
into sealed, single-responsibility coordinators — behavior-locked at every step, verified by
the unchanged 629-test suite.

**Requirements**: REF-01, REF-02, REF-03, REF-04, REF-05
**Depends on:** —
**Plans:** 5/5 plans complete

Plans:
**Wave 1**
- [x] 01-01-PLAN.md — Baseline pin: tag + branch, Release 0/0, green suite and CI recorded; six guard characterization pins

**Wave 2** *(blocked on Wave 1 completion)*
- [x] 01-02-PLAN.md — Pure helpers (BulkRunRules) + REF-05 reflection-driven never-throw contract test

**Wave 3** *(blocked on Wave 2 completion)*
- [x] 01-03-PLAN.md — BulkBackupCoordinator (bulk backup + bulk restore) + REF-04a rename-retry-limit message

**Wave 4** *(blocked on Wave 3 completion)*
- [x] 01-04-PLAN.md — StatusSweepCoordinator (sweep engine + advisory I/O) + REF-04b per-row unreadable-path warning

**Wave 5** *(blocked on Wave 4 completion)*
- [x] 01-05-PLAN.md — RowActionsCoordinator + composition-root pass + owner decision on REF-02's ~700-line ceiling

**Execution order is strictly sequential (Plan 01→05):** baseline test pin → pure helpers →
bulk-backup coordinator → status-sweep coordinator → row-actions service → rest form. Each
plan ends with: Linux Release build 0/0, a dispatched `test.yml` run green, one commit per
concern; the Owner's Windows suite run is the verification of record at phase close.

### Phase 2: Audit follow-ups

**Goal:** Close the open findings from the 2026-11-18 audit scan that need a design decision or
test-harness work, plus the clean-code items from the same day's scan that remove real
duplication or real defects. The quick fixes from the audit scan (archive timestamp culture,
workflow token hardening, release SDK pin, restore-picker cancel rescan) already landed.

*Revised 2026-11-18 after checking every item against the code: causes and line numbers
corrected, one missed defect added as item 0, seven items dropped (listed at the end with
reasons). Line numbers refer to `dev/v1.1` at 7f6ddd5.*

#### Defects

0. **Save subfolders are silently left out of a backup when the save folder sits inside the
   backup root.** `DirectoryWalk.cs:106-108` skips every subfolder that is the same as or
   *inside* an excluded directory, and the exclusions are the backup root and the game's target
   folder (`BackupService.cs:116`). Nothing rejects a save folder inside the backup root
   (`RestoreService.cs:68` checks only the reverse), and CLAUDE.md requires that no path be
   refused. Example: backup root `Documents`, save folder `Documents\My Games\X`: every
   subfolder of `X` is pruned, only top-level files are archived, and no skip is reported.
   Restore then replaces the whole save folder with that archive, so the subfolders are lost;
   the safety backup before it is equally incomplete. Fix direction (settle in planning): when the
   backup root is an ancestor of the save folder, stop treating "inside the backup root" as a
   reason to prune; keep pruning a subfolder that is or contains the backup root, and any
   folder that is or lies inside a per-game archive folder (`<backupRoot>\<id>`). Report what
   was excluded instead of skipping silently. Test first. Must land before
   item 4, because correcting the drive-root case there would extend the loss to a backup root
   like `E:\`.
1. **Startup prompt can be lost.** `MainViewModel` starts the status sweep in its constructor
   (`MainViewModel.cs:150`); `App.xaml.cs:206` arms the prompt only after
   `await vm.ReportLoad()`. ReportLoad's error dialog is not the cause: `DialogService.Error` is
   fire-and-forget and `Confirm` queues behind it, so the two can never overlap. The real gap is
   ReportLoad's `await Task.Run(() => _backup.IsBackupRootReachable(root))`
   (`MainViewModel.cs:721-722`), which runs on every load with a configured backup root, the
   most common start. The sweep can settle during that await, before the prompt is armed; the
   prompt then fires on the next full sweep (Refresh), mid-session. Seeded, problem and fatal
   loads and an unset root never await, so they cannot race; an unreachable root marks every
   row `FolderNotAvailable`, so nothing is prompted either way. Fix: record "first full sweep
   settled" (one bool) and run the check immediately on arming if it already settled.
   `StartupUnsecuredPromptTests` arm after the sweep and drive `PromptUnsecuredGamesOnce`
   directly, so this changes that test contract.
2. **A row action can run beside "Back up selected" while its folder probe/picker is open.**
   `RunBulkBackupAsync` (`MainViewModel.cs:1330`) awaits `EnsureBackupRootAsync` (`:1460`,
   the `DriveHints` probe in `Task.Run`, then the picker) before any guard is raised. The
   toolkit only disables the running command itself, so row Backup, Refresh and
   ChangeBackupRoot stay enabled. A row Backup then runs alongside the bulk loop on the same
   game: each run's `CleanStaleTempFiles` can delete the other's in-flight `.zip.tmp`, and the
   bulk `finally` clears that row's `IsBusy` early. (The originally described pairing of two
   bulk commands cannot happen: with no root set every row is `FolderNotAvailable`
   (`BackupComparer.cs:30`), so "Back up all changed" has no targets and returns before the
   probe.) Fix: raise a guard before the await, as `RowActionsCoordinator.cs:69` already does,
   counted in `IsAnyOperationRunning`. Overlaps item 14.
3. **`DriveHints` blocks on network drives; two tests flake under load.**
   `SuggestBackupStartFolder` reads `drive.IsReady` for every drive (`DriveHints.cs:69`),
   network drives included, although its own comment excludes network drives because `IsReady`
   blocks. Check `DriveType` before `IsReady`; this is a user-facing fix and likely also cures
   the timeouts of
   `StartupUnsecuredPromptTests.Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op`
   and `MainViewModelTests.A_cancelled_first_backup_folder_prompt_writes_nothing` at the 3 s
   `WaitForAsync`. The first test drives a path the app cannot reach (no root means no row
   needs a backup, so the prompt never shows); rework or justify it. Overlaps items 2 and 24.

#### Clean-code backlog (2026-11-18 clean-code scan)

Structural items too large for the quick-fix pass. Patterns only where they remove real
duplication; no speculative abstraction (CLAUDE.md). Original numbering kept; dropped numbers
are listed at the end.

**Services**

4. **Path-comparison bugs at drive roots.** `PathResolver.ToTemplate` (`PathResolver.cs:150`)
   trims `C:\` to `C:`, which resolves to the current directory; `BackupService.IsSameOrInside`
   appends a separator to a root that still ends in one, so nothing is ever "inside" a drive
   root (`E:\` becomes `E:\\`). `KnownGamesScanner` trims both sides the same way and is
   correct. Extract one internal `PathCompare` helper for the broken call sites and
   `DirectoryWalk` (which depends on `BackupService` only for this). After item 0.
5. **Empty archive when the save folder holds only excluded content.**
   `ReparseFile.HasAnyFile` (used at `BackupService.cs:382`) walks without the exclusions, so a
   save folder whose only files sit in a nested backup folder passes the empty-check and yields
   a zero-entry archive that verifies (0 equals 0). Restore refuses that layout
   (`RestoreService.cs:68`), so no data is lost. Fix: fail when `ArchiveSaveDir` collected zero
   entries, rather than rebuilding `HasAnyFile` on `DirectoryWalk` (which has no early exit).
   Test first.
6. **Shorten `BackupService.Backup`** (~140 lines, `BackupService.cs:67-206`) by extracting only
   the rename/suffix ladder. The inner and outer catch are not duplicates (the inner one also
   deletes the temp file); leave them.
7. **Untangle `DescribeVerificationFailure`** (three modes behind two optional flags). The
   name+length-only mode is used by tests only (`BackupServiceTests.cs:272-322`): move those
   tests onto a production mode and delete that mode.
9. **One file-system-fault predicate.** Exception lists differ per file and list subclasses
   beside `IOException` (e.g. `DirectoryWalk.cs:53`). The two stale-temp cleanups do different
   jobs (files in the archive folder vs. folders beside the save folder); leave them separate.
11. **`DialogService` constructs `new BackupService()`** for the restore picker's delete
    (`DialogService.cs:191`); pass the delegate from the view model instead. The UI-thread
    branch of `Confirm` is unreachable (all four callers use `Task.Run`); remove it or assert.
    Do not route it through the dialog queue: it blocks the UI thread and would deadlock. Also
    check, not yet verified: the picker's delete confirmation is queued to the main window's
    dialog host while the modal picker is open.

**ViewModels**

14. **One `RequeryOperationGates()`** instead of four `[NotifyCanExecuteChangedFor]` lists that
    already differ (`BulkBackupTotal` 7 commands, `IsBackupRunning` 5, `IsRestoreRunning` 5,
    `IsScanRunning` 1) plus hand calls in four places (`MainViewModel.cs:128-130`, `:191-193`,
    `:197-201`, `:213-214`). The `IsBusy` branch never notifies `BackupCommand`/
    `RestoreCommand` although `CanBackupRow` reads `row.IsBusy` (`:1132`); invisible today only
    because busy rows collapse those buttons. `MainViewModelGuardPinTests` pins the behavior.
    After item 2.
15. **One bulk-progress callback type.** Backup takes `IProgress<(int,string)>`, restore
    `Action<int,string>`. The difference has a reason (the backup loop runs in `Task.Run`,
    `BulkBackupCoordinator.cs:99`; restore on the caller's context), so unify by giving backup
    an `Action<int,string>` invoked through `_onUi`, as the loop already does for row writes.
    That removes the late-arrival guard (`MainViewModel.cs:1337-1343`) and makes the tests
    deterministic. Keep the bulk-progress state in `MainViewModel` (XAML binds it directly) and
    keep both empty-target checks (the view-model one stops a zero-target run from opening the
    folder picker; the coordinator one protects `targets[0]`).
16. **Share the save-folder resolution** of `AddGameDialogViewModel` and
    `ChangeSaveFolderDialogViewModel` (~40 duplicated lines,
    `ChangeSaveFolderDialogViewModel.cs:58-102` vs. `AddGameDialogViewModel.cs:187-195`,
    `:379-407`) as one pure static `ResolveFolder`; no stateful probe object. `BrowseAsync`
    returning `Task.CompletedTask` becomes a synchronous `Browse()` (the generated name stays
    `BrowseCommand`).

**Views**

19. **Toolbar button template.** Ten buttons (`MainWindow.xaml:36-161`) repeat the same 6-line
    content block; use a `ContentTemplate` on the `ToolbarButton` style (icon in `Tag`, label
    as Content; two labels are bound, all use `_` access keys, so check access keys by hand).
    The overflow menu repeats a 13-line header three times (`:551-590`); share it. Leave the
    three 4-line Click handlers as they are.
20. **Shared row-action button style and dialog footer style.** The `IconActionButton` +
    `IsBusy` collapse block is duplicated (`MainWindow.xaml:447-455`, `:521-529`). Dialog
    footer button MinWidths are 80/110/90/90; use one shared minimum that fits "Update folder".
21. **Spacing-token cleanup, not full tokenization.** 57 raw Margin/Padding literals vs. 12
    token uses, mostly one-sided (`0,0,8,0`, `0,0,0,4`); tokenizing them all would need ~10
    directional tokens and read worse. Instead: fix the misused uniform tokens (`md` at
    `MainWindow.xaml:202`, `sm` at `:215`/`:634`, `xs` at `:223`/`:605`; visible layout
    change, check in the running app) and delete the unused `lg`, `xl`, `radiusControl`,
    `radiusControlValue` (`Styles.xaml:60-61`, `:72`, `:74`). `PaletteRed*` breaks the
    `SavegameBunker*` prefix, but WPF-UI 4.3.0 defines the same keys, so `App.xaml:39-40`
    currently overrides the library's red app-wide; renaming drops that override. Rename the
    four references deliberately and check visually.

**Tests**

22. **Shared fixtures.** Temp-dir setup is copied into 21 files (20 with `Dispose`, bodies
    vary: deny-rule removal, junction cleanup); one `TempDir : IDisposable` beside
    `TestFileSystem.cs` that always does the full cleanup. `WaitForAsync` exists 5x plus a
    variant (`RestorePickerViewModelTests.cs:381`). `GameWithSaves` exists 5x with two
    signatures (`(fileName, content)` in the service tests, `(id, displayName)` in the
    coordinator tests), so two helpers. Move `RecordingDialogService`
    (`MainViewModelTests.cs:15`, used by 7 files) to its own file.
23. **Split `MainViewModelTests.cs`** (3,272 lines) along its existing section markers. Pure
    move; review with `git diff --color-moved`. After 22 and 24.
24. **No unasserted polls; named timeouts.** Fix the `Thread.Sleep` poll loop without a final
    assert (`MainViewModelTests.cs:2486-2488`). Replace the 3000 ms helper default and the
    15000 ms call-site timeouts (12 sites) with named constants. Keep the fixed waits that prove
    something did *not* happen (`Task.Delay(300)` at `AddGameDialogViewModelTests.cs:560` and
    `StartupUnsecuredPromptTests.cs:184`, `Delay(50)` at `:266`) behind a named constant, and
    keep `Thread.Sleep(1100)` (`RestoreServiceTests.cs:374`, `:522`) and the 20 ms
    NTFS-timestamp waits: archive names have one-second resolution.

**Order:** 0 → 4 → 5 (all touch `DirectoryWalk`); 2 → 14; 3 → 24; tests 22 → 24 → 23.
Everything else is independent.

#### Considered and dropped (2026-11-18 review)

- **8. Result records instead of `out`.** `TryListBackups` is the standard Try pattern;
  `Backup(..., out)` has one production caller (`RestoreService.cs:96`), and
  `ServiceContractTests` look methods up by signature. Churn without payoff.
- **10. User-facing service messages through `Strings`.** `SPEC.md:19` rules out localization;
  moving ~50 messages buys nothing.
- **12. Shared parallelism constant.** The status sweep (`StatusSweepCoordinator.cs:86`) and the
  known-games scan (`KnownGamesScanner.cs:30`) are unrelated tuning knobs in different layers.
  At most name the sweep's literal.
- **13. Operation scope instead of Started/Finished pairs.** The four counter members are used
  in two `try/finally` blocks only (`RowActionsCoordinator.cs:76/97`, `:151/172`); restore's
  `finally` must still clear `RestoreStep` first, so a disposable scope saves ~4 lines.
  `EnsureBackupRootAsync` has to stay on the host (picker, property changes).
- **17. Shorter `StartStatusScan`, startup prompt and `FindGamesAsync`.** 55/42/54 lines,
  linear and mostly comments; helpers would only add indirection.
- **18. `CanExecute` on `ChangeSaveFolderCommand`/`RemoveGameCommand`.** Both run via
  `.Execute()` from Click handlers (`MainWindow.xaml.cs:45`, `:51`), which never consult
  `CanExecute`; the flyout is disabled via `!IsBusy` (`MainWindow.xaml:538`) and both methods
  guard `IsBusy` (`MainViewModel.cs:1169`, `:1224`). Only the stale comment at
  `MainWindow.xaml:286-287` needs correcting.
- **20 (part). Shared empty-state template.** Two copies that differ
  (`MainWindow.xaml:620-638`, `:644-655`).

**Requirements**: none registered — scope is items 0–7, 9, 11, 14–16, 19–24 above, the
`MainWindow.xaml:286-287` comment, and decisions D-01..D-13 (`02-CONTEXT.md`)
**Depends on:** Phase 1
**Plans:** 15 plans

Plans:
**Wave 1**
- [x] 02-01-PLAN.md — Item 0: save subfolders archived (D-10), left-out note (D-01), restore refuses a save folder holding the archives (D-11, D-02), SPEC/CHANGELOG (D-03)
- [x] 02-02-PLAN.md — Item 16: shared SaveFolderPreview.Describe, synchronous Browse
- [x] 02-03-PLAN.md — D-07 baseline: demo data, reusable capture tooling, before screenshots from the unchanged UI

**Wave 2** *(blocked on Wave 1 completion)*
- [x] 02-04-PLAN.md — Items 19–20: toolbar/menu templates, RowActionButton, DialogFooterButton (D-12); after-19-20 screenshots and access-key pass (D-07)
- [x] 02-05-PLAN.md — Item 3: DriveType before IsReady, drive-probe seam (D-05), reachable bulk picker-cancel fact (D-04, D-13)

**Wave 3** *(blocked on Wave 2 completion)*
- [ ] 02-06-PLAN.md — Items 1–2: startup prompt evaluated on arming after the first sweep; IsBulkPreparing guard

**Wave 4** *(blocked on Wave 3 completion)*
- [ ] 02-07-PLAN.md — Items 4–5: PathCompare at drive roots, ToTemplate root fix, no empty archives
- [ ] 02-08-PLAN.md — Items 14–15: RequeryOperationGates, one bulk progress callback type

**Wave 5** *(blocked on Wave 4 completion)*
- [ ] 02-09-PLAN.md — Items 7, 6, 9: two verification modes, rename ladder extracted, FileSystemFault predicate
- [ ] 02-10-PLAN.md — Item 11: picker delete action from the caller, Confirm asserts its thread contract
- [ ] 02-11-PLAN.md — Item 21 + item-18 comment: SavegameBunkerDanger* rename (D-06), one-sided margins, unused tokens; after-21 screenshots

**Wave 6** *(blocked on Wave 5 completion)*
- [ ] 02-12-PLAN.md — Item 22 (part 1): TempDir, TestWait, RecordingDialogService file; view-model test classes

**Wave 7** *(blocked on Wave 6 completion)*
- [ ] 02-13-PLAN.md — Item 22 (part 2): TestGames builders; service and coordinator test classes

**Wave 8** *(blocked on Wave 7 completion)*
- [ ] 02-14-PLAN.md — Item 24: named wait budgets, asserted poll

**Wave 9** *(blocked on Wave 8 completion)*
- [ ] 02-15-PLAN.md — Item 23: split MainViewModelTests.cs into seven classes in one collection
