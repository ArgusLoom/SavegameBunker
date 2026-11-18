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

**Goal:** Close the three open findings from the 2026-11-18 audit scan that need a design
decision or test-harness work. The quick fixes from the same scan (archive timestamp culture,
workflow token hardening, release SDK pin, restore-picker cancel rescan) already landed.

1. **Startup prompt can be lost.** The construction-time sweep (`MainViewModel.cs:166`) can
   settle before `App.xaml.cs:206` arms the prompt after `await vm.ReportLoad()`, for example
   behind ReportLoad's error dialog or a slow backup-root reachability check. The prompt then
   fires on the next full sweep (Refresh), mid-session, which contradicts the documented
   behavior. Constraints: the prompt must never overlap ReportLoad's dialog, and
   `StartupUnsecuredPromptTests` arm after the sweep and drive `PromptUnsecuredGamesOnce`
   directly, so the fix changes that test contract.
2. **Concurrent bulk backups on first run.** `RunBulkBackupAsync` (`MainViewModel.cs:1357`)
   awaits `EnsureBackupRootAsync` (the `DriveHints` probe in `Task.Run`, then the picker) before
   any guard is raised, so the other bulk command's `CanExecute` stays true. Two loops can write
   the same games, and the first `EndBulkProgress` clears `IsBusy` while the second still runs.
   Only reachable before a backup root is configured.
3. **Flaky tests under load.** `StartupUnsecuredPromptTests.Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op`
   and `MainViewModelTests.A_cancelled_first_backup_folder_prompt_writes_nothing` time out at the
   3 s `WaitForAsync` when the suite runs right after a build. Both route through the unset-root
   `DriveHints` probe, which touches real drives.

### Clean-code and modernization backlog (2026-11-18 clean-code scan)

Structural items too large for the quick-fix pass. Patterns only where they remove real
duplication; no speculative abstraction (CLAUDE.md).

**Services**
4. **One path-comparison rule.** `BackupService.NormalizeDir`/`IsSameOrInside`,
   `PathResolver.ToTemplate` and `KnownGamesScanner` each normalize paths differently
   (`TrimEnd` turns `C:\` into `C:`); `DirectoryWalk` depends on `BackupService` only for this.
   Extract an internal `PathCompare` helper.
5. **`ReparseFile.HasAnyFile` ignores exclusions.** It is a second recursive walk beside
   `DirectoryWalk`; a save folder whose only files sit in a nested backup folder passes the
   empty-check and yields an empty archive. Build it on `DirectoryWalk` with early exit and the
   same exclusions (test first).
6. **Split `BackupService.Backup`** (~143 lines) into validate / write-and-verify /
   commit-with-suffix-ladder / compose-message; collapse the duplicated inner and outer catch.
7. **Untangle `DescribeVerificationFailure`** (three modes behind two optional flags; the
   name+length-only mode is used by tests only).
8. **Result records instead of `out` + result.** `Backup(..., out string?)` and
   `TryListBackups(..., out)` become e.g. `BackupOutcome(OperationResult, string? Path)`.
9. **One file-system-fault predicate and best-effort helpers.** Exception lists differ per file
   (`IOException` listed beside its own subclasses); stale-temp cleanup exists twice in two
   shapes.
10. **User-facing service messages through `Strings`.** `RestoreService`, `GameCatalog` and
    `BackupService` mix resourced and hard-coded English failure text, sometimes in one method;
    `ElapsedFormat` builds English outside the resource.
11. **`DialogService` constructs `new BackupService()`** for the picker's delete; pass the
    delegate from the view model. Decide whether the unreachable UI-thread branch of `Confirm`
    should route through the dialog queue or be removed.
12. **Shared parallelism constant** for the status sweep and the known-games scan.

**ViewModels**
13. **Operation scope instead of Started/Finished pairs.** `IRowActionHost` has nine members,
    four of them two counters; replace with a disposable operation scope and give the
    coordinator `CatalogSession` directly.
14. **One `RequeryOperationGates()`** instead of three different `NotifyCanExecuteChanged`
    wirings (`[NotifyCanExecuteChangedFor]` lists that already differ, hand calls in two lambdas).
15. **Bulk progress API.** Backup takes `IProgress<(int,string)>`, restore `Action<int,string>`;
    unify, and move the bulk-progress state out of `MainViewModel`. Drop the double empty-target
    check (view model and coordinator).
16. **Shared save-folder probe** for `AddGameDialogViewModel` and
    `ChangeSaveFolderDialogViewModel` (~35 duplicated lines); `BrowseAsync` returning
    `Task.CompletedTask` becomes a synchronous command.
17. **Shorter `StartStatusScan`, startup prompt and `FindGamesAsync`** via extracted helpers.
18. **`ChangeSaveFolderCommand`/`RemoveGameCommand` get a `CanExecute`** like Backup/Restore.

**Views**
19. **Toolbar button template.** Ten buttons repeat the same 9-line content block; the overflow
    menu repeats its header three times with three near-identical Click handlers.
20. **Shared row-action button style, empty-state template, dialog footer style** (footer
    margins/widths already disagree across the four dialogs).
21. **Spacing tokens.** 59 raw margin/size literals against 10 token uses; `lg`, `xl`,
    `radiusControl*` unused; uniform-Thickness tokens add unintended vertical space. Also
    `PaletteRed*` breaks the `SavegameBunker*` resource prefix.

**Tests**
22. **Shared fixtures.** Temp-dir setup/Dispose copied into ~20 classes, `WaitForAsync` 5x,
    `GameWithSaves` 5x, `RecordingDialogService` living inside `MainViewModelTests.cs`.
23. **Split `MainViewModelTests.cs`** (3,269 lines) along its existing sections.
24. **No fixed waits.** `Thread.Sleep` polling without a final assert, `Task.Delay(300)` as a
    proof, timeouts of 3 s and 15 s scattered; one named timeout constant.

**Requirements**: TBD
**Depends on:** Phase 1
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 2 to break down)
