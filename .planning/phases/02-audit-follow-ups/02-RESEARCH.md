# Phase 2: Audit follow-ups - Research

**Researched:** 2026-11-18
**Domain:** Behavior-preserving defect fixes and cleanup in a hand-rolled WPF/MVVM app (.NET 10, WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, xUnit 2.9.3)
**Confidence:** HIGH for the service-layer and view-model items (read against live code and reproduced with a probe); MEDIUM for the XAML items (no UI run possible in this session)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Item 0 — deliberately excluded folders**
- **D-01:** When the backup walk deliberately leaves out a folder inside the save folder (one that is or contains the backup root, or is or lies inside a per-game archive folder `<backupRoot>\<id>`), the backup result message carries a plain **informational** line naming each left-out path and why (e.g. "Left out: <path> (backup folder)"). It must NOT reuse the unreadable-path warning wording of `DescribeSkippedPathsWarning` ("could not be read and are NOT in this backup"), because the exclusion is the user's chosen layout, not a defect. Excluded folders are not counted as skipped/unreadable paths.
- **D-02:** The plan must add an explicit test proving restore never deletes a folder the backup deliberately excluded. Today `RestoreService.cs:68` refuses when the backup root is inside the save folder; the test must cover every exclusion case of D-01 (backup root inside the save folder, per-game archive folder inside the save folder), not rely on reasoning that the guard happens to cover them.
- **D-03:** Item 0 is an observable behavior change (save subfolders that used to be pruned are now archived; exclusions are reported). Record it in `SPEC.md` §3 Backup (with the reasoning inline, per CLAUDE.md) and add a `### Fixed` entry under `## [Unreleased]` in `CHANGELOG.md`, written in the existing user-facing style. Update `docs/DECISIONS.md` only if the change revises a recorded design decision.

**Item 3 — unreachable test and flaky timeouts**
- **D-04:** `StartupUnsecuredPromptTests.Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op` drives a state the app cannot reach. Rework it so its intent ("cancelling the folder picker is a no-op that writes nothing") is exercised through a path the app actually reaches. If an existing test (e.g. `MainViewModelTests.A_cancelled_first_backup_folder_prompt_writes_nothing`) already covers exactly that reachable path, delete the unreachable test instead and say so in the plan.
- **D-05:** Fix `DriveHints.SuggestBackupStartFolder` to check `DriveType` before `IsReady` first. If the two named tests still depend on the machine's real drives after that fix, inject the drive probe as a delegate seam (the Phase 1 coordinator seam style, no new dependency) so the tests stop touching real drives. Do NOT resolve the flake by raising timeouts alone.

**Item 21 — palette and visible layout**
- **D-06:** Rename `PaletteRedColor`/`PaletteRedBrush` to app-prefixed keys (`SavegameBunkerDangerColor`/`SavegameBunkerDangerBrush`) and update all references (`App.xaml`, `Themes/Styles.xaml:148`, `:162`, `Views/AddGameDialog.xaml:97`). App text keeps the WCAG-AA red `#FFDC6C64`; WPF-UI controls get the library's own red back. Research must list which WPF-UI 4.3.0 controls/templates consume `PaletteRed*`, so the visible effect of dropping the override is known before execution.
- **D-07:** Visual verification for items 19, 20 and 21 is by **before/after screenshots** taken by the executor from the running app (main window with rows, toolbar, overflow menu, row action buttons in busy and idle state, each dialog footer). Items 19/20 must show no visible difference; item 21's spacing changes and the red change must be visible only where intended. Access keys (`_` mnemonics) on the templated toolbar buttons are checked explicitly. The owner reviews the image pairs; no interactive owner checkpoint is required for this.

**Plan structure and workflow**
- **D-08:** Work stays on the current branch `dev/v1.1`; no phase branch. Record the full-suite test count as the baseline at the start, keep the suite green after every plan; CI on the PR to `main` remains the gate.
- **D-09:** Defect fixes land before the test-harness items 22–24. Tests written for the defects use the current helpers; item 22 later migrates them along with everything else.

### Claude's Discretion
- Grouping of items into plans and waves (user chose "planner decides"), within the ROADMAP order constraints: 0 → 4 → 5; 2 → 14; 3 → 24; 22 → 24 → 23; and D-09.
- Exact wording of the D-01 info line, within the existing message style.
- Item 11: remove or assert the unreachable UI-thread branch of `DialogService.Confirm`.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. Items 8, 10, 12, 13, 17, 18 and the empty-state part of 20 are "Considered and dropped" in ROADMAP.md and stay out, except the one-line stale-comment correction at `MainWindow.xaml:286-287`.
</user_constraints>

## Phase Requirements

No requirement IDs are registered for this phase (`null`). The ROADMAP item numbers are the scope: defects 0, 1, 2, 3 and clean-code items 4, 5, 6, 7, 9, 11, 14, 15, 16, 19, 20, 21, 22, 23, 24, plus the stale comment at `MainWindow.xaml:286-287`.

## Project Constraints (from CLAUDE.md)

- Exactly two shipping NuGet packages (WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2); no third. xUnit stays the only test-only exception. **This phase needs no new package.**
- Error contract: services never throw across their boundary, they return `OperationResult`. `ServiceContractTests` enforces it by reflecting over the **public** `SavegameBunker.Services` surface, so every new Services helper must be `internal` (a public one fails the surface fact until a hostile-input case is added). [VERIFIED: tests/SavegameBunker.Tests/ServiceContractTests.cs:142-146, `if (!type.IsPublic || type.Namespace != "SavegameBunker.Services") continue;`]
- Restore order is load-bearing: safety backup, extract to temp sibling, remove old directory, rename new one in. Nothing in this phase may reorder it.
- No code path may refuse, redirect or relocate a backup based on where the backup folder points. (Item 0 reports what was left out; it never refuses a destination. Restore may still refuse to replace a folder that holds the archives.)
- UI boundary: view models never touch dialogs directly; everything goes through `IDialogService`. Threading: filesystem work off the UI thread; `AsyncRelayCommand` handlers must not throw.
- Everything written to disk is English. Classes `sealed` unless there is a reason; file-scoped namespaces; XML doc comments on public types/methods explaining why.
- `SPEC.md` is the public spec (revise with reasoning inline); `CHANGELOG.md` for shipped changes; `.planning/` is tracked but code/tests/shipped docs must not depend on it.
- Tests: `WaitForAsync`, never a snapshot, because the test host has no `SynchronizationContext` and `Progress<T>` reports land on the thread pool.
- Surgical changes: every changed line traces to an item; remove only what your change made unused; mention, do not delete, unrelated dead code.
- No `.claude/skills/` or `.agents/skills/` directory exists in this repo. [VERIFIED: `ls` returned "No such file or directory" for both]

## Summary

Every cited `file:line` in the ROADMAP still matches the live code at HEAD `57811c1` (commits `6df0955` and `38e5ba3` shifted nothing the backlog cites). The baseline is **695 tests**; Release build is 0 warnings / 0 errors. The first full run on this machine showed the two known flaky tests failing at the 3 s `WaitForAsync` (693 passed, 2 failed); a second full run passed all 695, and both tests pass in isolation. So the flake is real and reproducible on this machine (it has two unreachable network mappings, `G:` and `M:`, among its drives), but intermittent. [VERIFIED: `dotnet test SavegameBunker.sln -c Release -tl:off`, two runs this session]

Three findings go beyond the ROADMAP and change planning. (1) **Item 0 is confirmed, and restore has a second, unguarded data-loss layout that D-02 anticipates but the current guard does not cover:** a save folder equal to `<backupRoot>\<id>` passes `RestoreService.cs:68`, and restore then deletes every archive of that game (reproduced with a probe). (2) **The ROADMAP fix direction for item 0 ("keep pruning any folder that is or lies inside a per-game archive folder") would keep silently losing subfolders for a save folder inside `<backupRoot>\<id>`** (for example `D:\Games` as backup root, `D:\Games\doom\saves` as save folder, id `doom`) and restore would then delete them; the walk should prune "inside an excluded folder" only when the excluded folder is inside or equal to the walk root. (3) **Item 5's fix direction as written breaks a pinned test**: `Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path` expects success with zero archive entries when the only file is locked, so "fail on zero entries" must be gated on "and nothing was skipped as unreadable".

**Primary recommendation:** Run the Services lane first and strictly in order (item 0 with the D-02 restore guard, then 4, 5, 7, 6, 9, 11), the view-model lane (1, 2, then 14, 15, 3, 16) in parallel with the view lane (19, 20, 21, comment fix), and only then the test-harness lane 22, 24, 23. Reproduce each defect as a failing test first; the probe results in this document give the exact failing assertions.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Which folders a backup walk leaves out, and reporting them (0) | Services (`DirectoryWalk`, `BackupService`) | ViewModel (displays `OperationResult.Message`) | Archive contents and message are service output; `Report()` already routes the message. |
| Restore refusing a layout that would delete the backups (0 / D-02) | Services (`RestoreService`) | — | The guard sits before any write; restore ordering stays untouched. |
| Path comparison at drive roots (4) | Services (new `internal PathCompare`) | `PathResolver`, `RestoreService`, `DirectoryWalk` consume it | One normalizer for every call site. |
| Empty-archive failure (5) | Services (`BackupService.Backup`) | — | Needs `ArchiveSaveDir`'s own result. |
| Startup-prompt arming (1), bulk preflight guard (2), operation gates (14), bulk progress callback (15), start-folder seam (3) | ViewModel (`MainViewModel`, `BulkBackupCoordinator`) | Services (`DriveHints` for the `DriveType` fix) | State and command gating live in the view model; the coordinator stays WPF-free. |
| Shared save-folder resolution (16) | Services-level pure static (no WPF) | Two dialog view models call it | Pure function of a template string. |
| Dialog-host plumbing (11) | `DialogService` (the only WPF-aware class) | `GameActions`/`RestorePickerViewModel` supply the delete delegate | Keeps view models testable. |
| Toolbar template, row-button style, dialog footer style, palette and spacing tokens (19-21) | Views (XAML, `Themes/Styles.xaml`, `App.xaml`) | — | Pure presentation. |
| Shared fixtures, split, named timeouts (22-24) | Test project | — | No production change. |

## Standard Stack

### Core (all pre-existing; nothing is added)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.404 installed; `global.json` pins 10.0.400 with `rollForward: latestFeature` | Build and test | [VERIFIED: `dotnet --version`, global.json] |
| WPF-UI | 4.3.0 | Fluent controls, `ui:TitleBar`, `ui:Button` | [VERIFIED: src/SavegameBunker/SavegameBunker.csproj:59] |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]` partial properties, `[RelayCommand]`, `partial void OnXChanged` hooks | [VERIFIED: csproj:60; hook pattern in use at MainViewModel.cs:492, `partial void OnSearchTextChanged(string value)`] |
| xUnit | 2.9.3 (Microsoft.NET.Test.Sdk 18.12.0, runner 4.0.1) | Test framework | [VERIFIED: tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Raising 3 s timeouts to cure the flake | A drive-probe delegate seam | D-05 forbids timeout-only fixes; the seam also removes real-drive dependence from ~11 tests. |
| A hand-written `FileSystemFault` type | A one-line static predicate | Item 9 only needs `ex is IOException or UnauthorizedAccessException`. |

**Installation:** none. **Version verification:** no package is recommended beyond the two already pinned.

## Package Legitimacy Audit

This phase installs no external packages (two-NuGet rule in CLAUDE.md; none needed). No audit rows. **Packages removed due to [SLOP] verdict:** none. **Packages flagged [SUS]:** none.

## Per-Item Findings

Legend for "premise": **holds** = ROADMAP description matches live code; **partial** = holds but the stated fix direction needs refinement; **count off** = a number differs. All line numbers below are live (HEAD `57811c1`).

### Defects

**Item 0 — save subfolders silently pruned (premise: holds; fix direction needs refinement)**
- Live shape. `DirectoryWalk.cs:106-108` prunes `BackupService.IsSameOrInside(sub, excluded) || BackupService.IsSameOrInside(excluded, sub)`; `BackupService.cs:116` builds `var excludedDirs = new[] { NormalizeDir(backupRoot), NormalizeDir(targetDir) };`. The walk root's own files are enumerated with no exclusion check at all (`Walk(rootDir)` lists `Directory.EnumerateFiles(current)` first). [VERIFIED: files read this session]
- Reproduced with a probe (`BackupService`/`RestoreService` built from the live project): backup root `s1`, save folder `s1\My Games\X` holding `top.dat` and `profiles\p1.dat` yields `ok=True msg=Backed up x.` and **entries=top.dat** only: the subfolder is gone and nothing is reported. [VERIFIED: scratch probe, 2026-11-18]
- Which relations of walk root R, backup root B and game archive folder T=`B\id` actually need pruning:
  - B strictly inside R: prune the subfolder that **equals or contains** B. No "inside" direction needed.
  - B equals R (backup folder IS the save folder, or a shared save folder): every subfolder is "inside B"; other games' archive folders are direct children and cannot be told from real subfolders, so prune them all. Two pinned tests depend on this: `A_backup_folder_inside_or_equal_to_the_save_folder_is_honoured_and_never_archived` (the `""` row) and `A_backup_root_equal_to_a_shared_save_folder_packs_no_other_games_archive_dir`. [VERIFIED: BackupServiceTests.cs:747-770 and the shared-folder test]
  - R strictly inside B (the item 0 bug): nothing under R is an archive of this app unless R is inside or equal to T. Do **not** prune by "inside B".
  - R strictly inside T (`B\id\saves`): archives are direct files of T, outside R. Reproduced: backup of `s3\x\saves` (with `sub\b.dat`) archives **only a.dat**, and restore then leaves only `a.dat`; `sub\b.dat` is gone. [VERIFIED: scratch probe] Pruning "inside T" here repeats the data loss, so D-01's literal wording for this case needs refinement (Open Question 1).
  - R equals T: archives and the `.zip.tmp` staging file are root-level files, which the walk never excludes. Existing degenerate hazard; see Open Question 2.
- Recommended rule (one place, `DirectoryWalk`, which already knows `rootDir`): keep "excluded folder inside or equal to this subfolder" for every excluded dir; apply "this subfolder inside an excluded dir" only to excluded dirs that are themselves inside or equal to `rootDir`. Proposed shape (new code, not a quote):
  ```csharp
  // inside the Walk(sub) decision, replacing DirectoryWalk.cs:106-108
  var pruned = excludedDirs is not null && excludedDirs.Any(excluded =>
      PathCompare.IsSameOrInside(excluded, sub)
      || (PathCompare.IsSameOrInside(excluded, rootDir) && PathCompare.IsSameOrInside(sub, excluded)));
  ```
- Reporting (D-01): add an optional `Action<string>? onExcluded` to `ForEachFile` carrying the path relative to `rootDir` (same convention as `Skip`), invoked only from the archive pass in `ArchiveSaveDir`; do **not** pass it on the verification re-walk (`DescribeVerificationFailure`, which re-enumerates with the same `excludedDirs`) or the list is collected twice. Keep it a separate list from `skippedPaths`. Format with `DirectoryWalk.FormatSkippedList` (first five plus "and n more") to avoid a third list formatter; append to `createdMessage` next to `DescribeSkippedPathsWarning`'s line, not inside it. Use inline English like the existing warning (no new `Strings` key: item 10 is dropped).
- The shared-folder case (B equals R) will name every top-level subfolder on every backup; this is correct per D-01 but noisy, which is why the truncated list matters.
- Tests first (all in `BackupServiceTests.cs`/`RestoreServiceTests.cs`, current helpers per D-09): item 0 reproduction (assert the subfolder entry exists and the message carries the info line, not the "NOT in this backup" wording); the two pinned tests above must stay green; an R-inside-T case; D-02 cases below.
- **D-02 restore cases the plan must pin** (D-01 exclusion cases mapped to restore behavior):
  1. B inside R: refused today by `RestoreService.cs:68`, `if (BackupService.IsSameOrInside(backupRoot, saveDir))`. Add the "nothing deleted" assertion explicitly.
  2. R equals `B\id` (T equals R, "per-game archive folder inside the save folder" in D-02's wording): **not refused today.** The probe backed up and then restored a save folder `s2\x` under backup root `s2`: restore returned `ok=True`, and the folder afterwards held only `a.dat`, meaning every archive, including the safety backup and the archive just restored from, was deleted with the old folder. [VERIFIED: scratch probe] Fix: refuse when the game's archive folder is inside or equal to the save folder, i.e. check `IsSameOrInside(Path.Combine(backupRoot, game.Id), saveDir)` (it is a strict superset of today's check, because B inside R implies T inside R). Reuse the existing `str_restore_backupFolderInsideSaveFolder` message (its wording still fits: "move the backup folder").
  3. R inside B (item 0 layout): restore must succeed and round-trip the now-archived subfolders. Positive test.
  4. R inside T: restore replaces R only (archives are siblings); with the recommended rule the subfolders are archived, so assert round-trip.
- Docs (D-03): the paragraph to revise is SPEC.md:218-222 (the `backupRoot` inside the save directory bullet) under "### 3. Backup"; add a `### Fixed` bullet under `## [Unreleased]` (CHANGELOG.md already has one `### Fixed` block there, in a bold-lead-sentence style). `docs/DECISIONS.md` row "The backup folder is whatever the user chose... no warning" is not contradicted; update only if the plan adds the new restore refusal as a recorded decision (recommended: one row under "Backup and restore").
- Risk: HIGH value, MEDIUM complexity. Order: first; items 4 and 5 touch the same walk. Verify: `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~BackupServiceTests|FullyQualifiedName~RestoreServiceTests"`.

**Item 1 — startup prompt can be lost (premise: holds)**
- Live shape. `MainViewModel.cs:150` `StartStatusScan();` in the constructor; `App.xaml.cs` awaits `vm.ReportLoad()` and only then calls `vm.RequestStartupUnsecuredPrompt();`; `ReportLoad` awaits `Task.Run(() => _backup.IsBackupRootReachable(root))` at `MainViewModel.cs:720-721`; the sweep's closing callback runs `OnUi(PromptUnsecuredGamesOnce)` for full sweeps only (`:1099-1100`), and `PromptUnsecuredGamesOnce` exits immediately when `!_startupPromptPending` (`:764`). `RequestStartupUnsecuredPrompt` is a bare `_startupPromptPending = true;` (`:735`). [VERIFIED]
- Fix (as ROADMAP): add one `bool _firstFullSweepSettled`, set it in the isFullSweep branch just before `OnUi(PromptUnsecuredGamesOnce)`; make `RequestStartupUnsecuredPrompt()` set pending, then call `PromptUnsecuredGamesOnce()` if the flag is already set. The consumption flags inside `PromptUnsecuredGamesOnce` keep it once-per-session.
- Also set the flag for a full sweep over **zero rows** (the early `if (snapshot.Count == 0) return;` at `:1066-1067` skips the isFullSweep branch). Otherwise an empty catalog leaves the prompt armed forever and a later Refresh after adding games can pop the "startup" prompt mid-session (pre-existing quirk; cheap to close while here) (Open Question 10).
- Test contract change (ROADMAP is right): every existing `StartupUnsecuredPromptTests` fact calls `RequestStartupUnsecuredPrompt()` **before** forcing row kinds with `ApplyBackupComparison`, after the construction sweep has settled (the helper waits for it). With the fix, arming evaluates immediately against the real swept verdicts (backup root exists, no archives, so rows are `NeverBackedUp`), consuming the one shot before the test's synthetic kinds apply. Each fact must be reordered (set the synthetic kinds first, then arm) or the helper must expose arming separately. `No_unsecured_games_means_no_dialog` would otherwise fail (real rows are unsecured, so a Confirm appears). Add one new fact: construct, wait for settle, arm afterwards with unsecured rows, and expect the prompt without calling `PromptUnsecuredGamesOnce` manually.
- Threading note: in the app both the settle callback and arming run on the UI thread, so the plain bool is safe; in the test host `OnUi` runs inline on the pool thread, so arm only after the helper's `WaitForAsync` (as the tests already do) to avoid a double-evaluation race.
- Existing fact `A_refresh_of_an_empty_catalog_settles_with_no_row_checking_and_the_load_message` (MainViewModelGuardPinTests.cs, arms then refreshes an empty catalog and expects no Confirm) stays green under either empty-catalog choice.
- Risk: LOW. Verify: `--filter "FullyQualifiedName~StartupUnsecuredPromptTests"`.

**Item 2 — row action beside "Back up selected" while probe/picker is open (premise: holds)**
- Live shape. `RunBulkBackupAsync` (`MainViewModel.cs:1330`) awaits `EnsureBackupRootAsync()` (`:1333`, defined `:1460`) before the coordinator claims anything; `EnsureBackupRootAsync` returns synchronously when a root is configured (`:1462-1465`) and otherwise does `await Task.Run(DriveHints.SuggestBackupStartFolder)` then `_dialog.PickFolder` (`:1467-1468`). `CanBackupRow` is `row is not null && !row.IsBusy && !IsBulkBackupRunning` (`:1132`); `CanRestoreRow` identical (`:1147`); `IsAnyOperationRunning` is `IsAnyRowBusy || IsBackupRunning || IsRestoreRunning || IsBulkBackupRunning` (`:279-280`). The window close gate is `ViewModel.IsAnyOperationRunning` (MainWindow.xaml.cs:120). [VERIFIED]
- The race window exists only when no root is configured (otherwise the await completes synchronously): "Back up selected" is selection-driven and unfiltered by verdict, so it can run with an unset root, which is exactly when the probe and picker run.
- Recommended design: a dedicated observable `IsBulkPreparing` (private-set partial property, the same pattern as `IsBackupRunning`) raised before `EnsureBackupRootAsync`, cleared in a `finally` that wraps the whole `RunBulkBackupAsync` body, counted in `IsAnyOperationRunning`, and also read by `CanBackupRow`/`CanRestoreRow`. Do **not** reuse `BulkBackupTotal` (it drives the visible progress panel and `IsBulkBackupRunning`) and do **not** mark target rows `IsBusy` early (it would show a spinner on each row and move the coordinator's pinned claim order). `MainViewModelGuardPinTests` pins reset-to-Checking, then counters, then `IsBusy` inside the coordinator; the new guard sits before all of that and must not change it.
- Deterministic test (no timing): add an `Action? OnPickFolder` hook to `RecordingDialogService` mirroring `OnReport`, set an unset-root catalog with one selected row, execute `BackupSelectedCommand`, and inside the hook capture `vm.IsAnyOperationRunning`, `vm.BackupCommand.CanExecute(row)`, `vm.RefreshCommand.CanExecute(null)`, `vm.ChangeBackupRootCommand.CanExecute(null)`. Assert all blocked inside the hook, all released afterwards, and that cancelling the picker (`FolderToReturn = null`) leaves the guard released.
- Order: 2 then 14. Risk: MEDIUM (touches gating). Verify: `--filter "FullyQualifiedName~MainViewModelGuardPinTests|FullyQualifiedName~MainViewModelTests"`.

**Item 3 — `DriveHints` blocks on network drives; two flaky tests (premise: holds)**
- Live shape. `DriveHints.cs:71`: `volumes.Add((drive.RootDirectory.FullName, drive.DriveType, drive.IsReady));` reads `IsReady` for every drive type. The class doc says network drives are excluded because `IsReady` blocks. [VERIFIED]
- Fix: read `IsReady` only for `DriveType.Removable` or `DriveType.Fixed` (CD-ROM drives are also slow to answer and never qualify); everything else gets `IsReady: false`. `ChooseStartFolder` already requires `v.IsReady` and excludes network, so selection is unchanged. Existing `DriveHintsTests` (110 lines) exercise `ChooseStartFolder` with synthetic tuples and stay valid.
- Baseline evidence: first full run failed exactly the two named tests at `WaitForAsync` (3 s); second full run and isolated runs passed. This machine's drives: `C:` and `D:` fixed, `G:`/`M:` network (`IsReady` false), `W:`/`X:` network (`IsReady` true). [VERIFIED: Win32_LogicalDisk and `DriveInfo` probe]
- Seam (D-05, recommended to adopt now rather than conditionally): the only real-drive touch in the two tests is `await Task.Run(DriveHints.SuggestBackupStartFolder)` at `MainViewModel.cs:1467`. Add an optional trailing constructor parameter to `MainViewModel` (`internal`-visible via existing test access, default `DriveHints.SuggestBackupStartFolder`) held as a `Func<string?>` field and used at `:1467`. About eleven tests reach the picker with an unset root (`backupRoot: ""` at MainViewModelTests.cs:402, 417, 430, 457, 475, 1124, 2074, 2111 and others) and two call `DriveHints.SuggestBackupStartFolder()` directly in an assertion (`The_first_backup_folder_prompt_opens_on_the_detected_start_folder`, `A_chosen_backup_folder_is_honoured_exactly_as_picked_whatever_detection_returned`); those two assertions would compare against the injected fake instead. If the plan prefers the conditional form from D-05, gate the seam on a repeated stress run (see Validation Architecture) after the `DriveType` fix.
- Item 3 test rework (D-04): `StartupUnsecuredPromptTests.Yes_with_an_unset_backup_root_opens_the_picker_and_cancelling_is_a_no_op` is unreachable: with no root every row settles on `BackupComparison.FolderNotAvailable()` (BackupComparer.cs:30-31) and `BulkRunRules.NeedsBackup` accepts only `SavesChangedSinceBackup`/`NeverBackedUp` (BulkRunRules.cs:30-31), so the prompt never appears. `A_cancelled_first_backup_folder_prompt_writes_nothing` covers the **row Backup** path (`vm.BackupCommand.Execute(vm.Games[0])`), not the **bulk** path. No existing test cancels the picker through `BackupSelectedCommand` with an unset root. Recommendation: do not simply delete; rework the unreachable test into a reachable bulk fact (selected row, unset root, `BackupSelectedCommand`, `FolderToReturn = null`, assert no zip, no catalog write, no error, `IsBulkBackupRunning` false), and reuse it as the harness for item 2's guard assertions. If the owner prefers deletion, D-04 permits it only with the plan stating that the row-path test is the equivalent, which is not exactly true; flag it (Open Question 7).
- Order: 3 then 24 (named timeouts come after the seam). Risk: LOW-MEDIUM.

### Clean-code items: Services

**Item 4 — path-comparison bugs at drive roots (premise: holds)**
- Live shape. `PathResolver.cs:150`: `var target = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar);` and `:154` the same for candidates; `BackupService.cs:793-807` `NormalizeDir` uses `Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))` and `IsSameOrInside` appends `Path.DirectorySeparatorChar` to the parent. [VERIFIED]
- Probe: `Path.TrimEndingDirectorySeparator(Path.GetFullPath("E:/"))` returns `E:\` (a root is kept), so `IsSameOrInside` builds `E:\\` and nothing is ever "inside" a drive root; `GetFullPath("E:/").TrimEnd('\\')` returns `E:`, which resolves to the current directory on that drive. [VERIFIED: scratch probe]
- Fix: `internal static class PathCompare` in Services (internal, so `ServiceContractTests` is unaffected) with a `Normalize` that keeps a drive root intact and an `IsSameOrInside` that adds a separator only when the parent does not already end with one. Callers: `DirectoryWalk` (stops depending on `BackupService`), `RestoreService.cs:68` and `:76`, and `PathResolver.ToTemplate` (replace the `TrimEnd` pair, and compute `remainder` from the normalized lengths: `target[(best.Value.Length + 1)..]` at `:165` assumes the candidate had its separator trimmed).
- Items 0 and 4 interact: once `IsSameOrInside` works at a drive root, a backup root like `E:\` makes every save folder on `E:` "inside the backup root", which is exactly the item 0 loss; hence the order 0 then 4. After item 0's rule, a root-level backup root no longer prunes anything for a save folder below it.
- `KnownGamesScanner.cs:87` and `:108` compare trimmed strings on both sides and stay as they are (ROADMAP: correct).
- Tests: new `PathCompareTests.cs` (`E:\`, `E:\Saves`, `E:\Saves\X`, `E:\x` vs `E:\xy` prefix lookalike, trailing separators, case differences); `PathResolverTests` gains a drive-root `ToTemplate` fact (`ToTemplate` of `E:\` should not return `E:`). Existing `PathResolverTests` ToTemplate facts (lines 128-221) must stay green.
- Risk: MEDIUM (shared by restore guards). Verify: `--filter "FullyQualifiedName~PathResolverTests|FullyQualifiedName~PathCompareTests|FullyQualifiedName~RestoreServiceTests|FullyQualifiedName~BackupServiceTests"`.

**Item 5 — empty archive when the save folder holds only excluded content (premise: holds; fix direction needs a gate)**
- Live shape. `InspectSaveDir` (`BackupService.cs:378-388`) calls `ReparseFile.HasAnyFile(saveDir)` with no exclusions, so a save folder whose only file sits in a nested backup folder passes the empty-check. Probe: save folder `s4` whose only content is `s4\bk\old.zip` with `backupRoot = s4\bk` returned `ok=True msg=Backed up x. entries=[]`. [VERIFIED: scratch probe]
- ROADMAP fix: "fail when `ArchiveSaveDir` collected zero entries". As written this breaks `Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path` (BackupServiceTests.cs:239-259): a save folder whose single `save.dat` is locked yields zero entries **and** one skipped path, and the test requires `result.Success` plus `"NOT in this backup"`. Gate the failure on `entryHashes.Count == 0 && skippedPaths.Count == 0`. [VERIFIED: test and `GameWithSaves` read this session]
- On failure: `TryDelete(tempPath)` (as the other failure paths do) and return `OperationResult.Fail(...)` with inline English wording naming the save folder and that it holds only the backup folder; the empty-folder message `"Save folder is empty: {saveDir}"` (BackupService.cs:82) is asserted by two tests, leave it alone. `EnsureWritable` has already created `targetDir` by this point, which is acceptable (it is also created before the "archive failed" failures).
- Test first: save folder containing only `bk\old.zip` with `backupRoot = <save>\bk` must fail and leave no archive and no `.tmp`.
- Order: after 0 and 4. Risk: LOW.

**Item 6 — shorten `BackupService.Backup` (premise: holds)**
- Live shape. `Backup` spans `BackupService.cs:67-207` (the signature at 67; closes at 207). The rename ladder is `:148-191` (the `for (var attempt = 0; ; attempt++)` with two `catch (IOException) when (...)` clauses and the surrounding `catch (Exception ex)`); the inner failure path deletes the temp file at `:179`, the outer at `:189`. [VERIFIED]
- Extract only the ladder into a private static method returning the final path or a failure (for example `(string? FinalPath, OperationResult? Failure)`); leave the two catch levels as they are. `MaxRenameRetries` and the exact message at `:180-181` stay byte-identical (REF-04 pinned it: the ladder tries the bare name plus `-1`..`-100`).
- Pins: the rename-cap tests (101 occupied names fail, 100 succeed as `-100.zip`) and the same-second collision tests in `BackupServiceTests`. Risk: LOW. Do it after 0 and 5 so the extraction does not rebase over their edits.

**Item 7 — untangle `DescribeVerificationFailure` (premise: holds)**
- Live shape. Signature (`BackupService.cs:487-490`): `(string archivePath, string sourceDir, IReadOnlyCollection<string>? excludedDirs = null, IReadOnlyCollection<string>? skipped = null, IReadOnlyDictionary<string, string>? expectedHashes = null, bool verifyContent = false)`. Production callers: backup (`:135-136`, passes `excludedDirs, skippedPaths, entryHashes`) and restore (`RestoreService.cs:136`, `verifyContent: true`). The name-and-length-only mode (both content inputs absent) is reached only by four tests, `BackupServiceTests.cs:262-323`. [VERIFIED: grep of all call sites]
- Recommended shape: delete the `verifyContent` flag and make `expectedHashes == null` mean "hash the on-disk file against the archive entry" (the restore mode); a non-null map is the backup mode. Two modes, one optional parameter.
- Test migration is not a pure signature change. Under a content mode the order of checks matters: the content loop runs before the count/length comparison. `..._names_the_byte_counts_after_a_source_file_grows` asserts `"bytes in the archive"`/`"bytes in the save folder"`; with the disk-hash mode a grown file trips `"content differs ... (hash mismatch)"` first. Use the backup mode for the entry-count and byte-count tests (compute `expectedHashes` with `SHA256.HashData` over the original file content, entry name `save.dat`, hex via `Convert.ToHexString`; the archive entry's hash equals it, the source grew afterwards, so the length message is reached), and the restore mode for "returns null when the archive matches" and the unreadable-archive test. Entry-count test (extra file added) reaches `"entries"` in either mode.
- Risk: LOW-MEDIUM (test rewrite). `RestoreServiceTests.cs:475` mentions the method only in a comment.

**Item 9 — one file-system-fault predicate (premise: holds)**
- Live shape. `UnauthorizedAccessException`/`IOException` filters: `BackupService.cs:293` (`FileNotFoundException or IOException or UnauthorizedAccessException`), `:384-387` (four separate catches), `:681` (`UnauthorizedAccessException or IOException`); `DirectoryWalk.cs:53, :68, :82, :95` (lists that vary: `:53`/`:82` name `DirectoryNotFoundException` and `PathTooLongException` beside `IOException`); `DriveHints.cs:73-74`; `ReparseFile.cs:42-43, :53-54, :64-67`. [VERIFIED: grep]
- Fact that makes it a safe consolidation: `DirectoryNotFoundException`, `FileNotFoundException` and `PathTooLongException` all derive from `IOException`; `UnauthorizedAccessException` derives from `SystemException`. [VERIFIED: runtime reflection, `BaseType` of each printed `IOException`/`SystemException`] So `ex is IOException or UnauthorizedAccessException` is behavior-identical to every current list.
- Shape: one `internal static bool IsFileSystemFault(Exception ex)` (put it in the same `PathCompare`-style internal helper file or a small `FileSystemFault` class), used as `catch (Exception ex) when (...)`. Leave `DeleteArchive`'s separate `IOException`/`UnauthorizedAccessException` catches (`BackupService.cs:332`, `:339`) alone: they produce different messages. Leave the two stale-temp cleanups separate (ROADMAP).
- Order: after 5, 6, 7 (same files). Risk: LOW. Verify: full suite (many tests pin skip behavior: locked, denied-ACL, junction).

**Item 11 — `DialogService` constructs `new BackupService()` (premise: holds; unverified sub-check still open)**
- Live shape. `DialogService.cs:190-192`: `var vm = new RestorePickerViewModel(gameDisplayName, backups, this, path => new BackupService().DeleteArchive(path));`. The only caller of `ShowRestorePickerDialog` is `GameActions.cs:115`, and `GameActions` already holds `_backup`. All four `Confirm` callers run it under `Task.Run`: `MainViewModel.cs:781` (inside `_ = Task.Run(() => ...)`), `:1228` (`await Task.Run(() => _dialog.Confirm(`), `GameActions.cs:166` (inside `Task.Run(() => _restore.Restore(... prompt => _dialog.Confirm(`), `RestorePickerViewModel.cs:115` (`await Task.Run(() => _dialogs.Confirm(`). So the UI-thread branch at `DialogService.cs:135-138` is unreachable. [VERIFIED]
- Delegate: ROADMAP says pass it from the view model. That means a new parameter on `IDialogService.ShowRestorePickerDialog` and four implementers to update: `DialogService`, `RecordingDialogService` (MainViewModelTests.cs:15), `ThrowingDialogService` (AddGameDialogViewModelTests.cs:12) and the nested `ThrowingConfirmDialogService` (StartupUnsecuredPromptTests.cs:214). The smaller alternative (constructor-inject the delegate into `DialogService` from `App.OnStartup`) touches no test fake; either satisfies the item. Recommend the ROADMAP form unless the diff size matters to the owner.
- `Confirm`'s UI-thread branch (discretion): replace with `Debug.Assert(!_owner.Dispatcher.CheckAccess(), "...")` and the single `InvokeAsync(...).Task.Unwrap()` path, keeping the existing doc comment about why it must not run inline. Do not route it through the dialog queue (ROADMAP: it would deadlock). `DialogService` is outside `ServiceContractTests`' surface and is not unit-testable (WPF), so verification is the Release build plus a manual run.
- The "check, not yet verified" item: the picker's delete confirmation. Facts: the only `ContentDialogHost` in the app is `MainWindow.xaml:666` (`RootDialogHost`), wired once at `App.xaml.cs:189` (`contentDialogService.SetDialogHost(window.RootDialogHost);`); `RestorePickerWindow` is opened with `ShowDialog()` and `Owner = _owner` (DialogService.cs:193-194) and contains no dialog host; its delete button runs `RestorePickerViewModel.DeleteArchiveAsync` which calls `_dialogs.Confirm(...)` (`:115`). In WPF a modal window disables the other windows of the thread, so a content dialog drawn in the main window may not be answerable. This cannot be decided from code alone and I could not run the UI; the CHANGELOG notes deleting backups from the restore list as a working feature, which argues it works in practice. [ASSUMED] Treat it as a manual check (open picker, press a row's delete button, confirm the dialog can be answered) with a `checkpoint:human-verify`, and say in the plan what happens if it fails (it would be a new defect outside this phase's listed items; surface it, do not silently expand scope).

### Clean-code items: ViewModels

**Item 14 — one `RequeryOperationGates()` (premise: holds)**
- Live shape. The lists: `BulkBackupTotal` (`MainViewModel.cs:433-439`: Refresh, ChangeBackupRoot, BackupAllChanged, BackupSelected, RestoreSelected, Backup, Restore = 7), `IsBackupRunning` (`:625-629`, 5), `IsRestoreRunning` (`:669-673`, 5), `IsScanRunning` (`:655`, only `FindGamesCommand`). Hand calls: `:128-130` (Games.CollectionChanged: BackupAllChanged, BackupSelected, RestoreSelected), `:191-193` (ComparisonKind), `:197-201` (IsBusy: the three plus Refresh and ChangeBackupRoot), `:213-214` (IsSelected: BackupSelected, RestoreSelected). `IsBusy` never notifies `BackupCommand`/`RestoreCommand` although `CanBackupRow` reads `row.IsBusy` (`:1132`). [VERIFIED]
- Shape: one private `RequeryOperationGates()` that calls `NotifyCanExecuteChanged` on the seven gate commands; hook it from `partial void OnBulkBackupTotalChanged`, `OnIsBackupRunningChanged`, `OnIsRestoreRunningChanged` (and the item 2 flag) and call it from the `IsBusy` branch; delete the three attribute lists. `IsScanRunning`'s single `[NotifyCanExecuteChangedFor(nameof(FindGamesCommand))]` is not an operation gate (`CanFindGames` reads only `IsScanRunning`, `:923`) and should stay as it is. The `ComparisonKind`/`Games.CollectionChanged`/`IsSelected` branches notify a **subset** on purpose (selection and verdict never change Refresh/ChangeBackupRoot); keep them narrow or accept extra requeries, but do not claim they are duplicates of the gate.
- The extra `BackupCommand`/`RestoreCommand` notification on `IsBusy` is a deliberate, small behavior change (visible only as correct enablement); note it in the plan.
- Tests: `Setting_row_IsBusy_raises_CanExecuteChanged_on_the_toolbar_commands` (MainViewModelTests.cs:1655) and `BackupSelected_command_raises_CanExecuteChanged_when_a_rows_IsSelected_flips` (`:1993`) are the existing pins; add one fact asserting `BackupCommand` and `RestoreCommand` raise `CanExecuteChanged` when a row's `IsBusy` flips, and one per op-flag if cheap. Order: after 2. Risk: LOW-MEDIUM.

**Item 15 — one bulk-progress callback type (premise: holds)**
- Live shape. Backup: `Progress<(int Index, string Name)>` with the late-arrival guard (`MainViewModel.cs:1337-1343`, `if (!IsBulkBackupRunning) return;`) passed to `BulkBackupCoordinator.BackupAsync(..., IProgress<(int Index, string Name)> progress, ...)`; the loop calls `progress.Report((i + 1, row.DisplayName));` inside `Task.Run` (`BulkBackupCoordinator.cs:99-104`). Restore: `Action<int, string> onItem` called directly (`:214`), wired to `SetBulkProgress`. `_onUi` already wraps every row write in the loop (`:109`, `:125`, `:130`). [VERIFIED]
- Shape: replace the parameter with `Action<int, string> onItem` and call `_onUi(() => onItem(i + 1, row.DisplayName))` (capture the index in a local first); pass `SetBulkProgress` from the view model; delete the guard. Keep the bulk-progress state in `MainViewModel` and keep both empty-target checks (`MainViewModel.cs:1265` in `BackupAllChangedAsync`/`:1390` and the coordinator's `if (targets.Count == 0) return null;`). `CreateByteProgress` (`Progress<long>`, self-guarded by `row.BackupBytesTotal > 0`) stays.
- Tests: `BulkBackupCoordinatorTests.cs` has `RecordingIndexProgress : IProgress<(int Index, string Name)>` (`:32`); replace it with a list filled by a lambda. Late-arrival waits in `MainViewModelTests` become deterministic. Risk: LOW-MEDIUM. Order: after 2 (same method).

**Item 16 — share save-folder resolution (premise: holds)**
- Live shape. `ChangeSaveFolderDialogViewModel.cs:90-102` `ResolveFolder` is identical to `AddGameDialogViewModel.cs:394-407` except the missing-folder text: Add keeps a cached `private static readonly string FolderMissingStatus = Strings.Get("str_common_folderNotFound");` (`:30`), Change calls `Strings.Get("str_common_folderNotFound")` inline. Both `BrowseAsync` bodies end in `return Task.CompletedTask;` (`:59-66` and `:186-195`). The token-guarded `StartResolve`/`ReportResolveAsync` pair is also duplicated (`AddGameDialogViewModel.cs:379-393`, `ChangeSaveFolderDialogViewModel.cs:74-88`). [VERIFIED]
- Shape: one pure `internal static string Describe(string folder)` (new small class in Services or ViewModels; internal; no state) used by both. The resolve token pair is not part of the ROADMAP item (no "stateful probe object"); leave it. `BrowseAsync` becomes a synchronous `private void Browse()` with `[RelayCommand]`, so the generated property stays `BrowseCommand` and becomes a plain `RelayCommand`. XAML binds `Command="{Binding BrowseCommand}"` (AddGameDialog.xaml:90-91 and ChangeSaveFolderDialog.xaml:141-142); no test references `BrowseCommand` (grep of `tests/` returned none). Risk: LOW.
- Keep the resource lookup lazy or cached consistently (`Strings.Get` throws on an unknown key by design, per ServiceContractTests' exemption list); do not change message text.

### Clean-code items: Views

All XAML items are style deduplication; D-07 requires before/after screenshots. No UI run was possible in this session, so every XAML claim below is from source reading.

**Item 19 — toolbar button template (premise: partial)**
- Live shape. Ten `ui:Button`s at `MainWindow.xaml:36-161` (ThemeToggle, OpenCatalog, Help, FindGames, AddGame, Refresh, ChangeBackupRoot, BackupAllChanged, BackupSelected, RestoreSelected), each with the same six-line `StackPanel`/`Viewbox`/`Path`/`AccessText` block; `ToolbarButton` style at `Styles.xaml:301-335`. Two labels are bound (`{Binding BackupSelectedLabel}` `:147`, `{Binding RestoreSelectedLabel}` `:159`). [VERIFIED]
- **Premise gap:** ROADMAP says all use `_` access keys. Two of the ten do not: `str_toolbar_toggleTheme` is "Switch theme" and `str_toolbar_backUpAllChanged` is "Back up all changed" (no underscore). The other eight keys carry one (`_Add game`, `_Scan`, `_Refresh`, `_Open the games list`, `_Change backup folder`, `_Back up selected`, `Res_tore selected`, `_Help`). [VERIFIED: Strings.en.xaml:30, :33, :41-49] The templated button must render both kinds identically.
- WPF-UI 4.3.0's `Button.xaml` `ContentPresenter` has `ContentTemplate="{TemplateBinding ContentTemplate}"` and `RecognizesAccessKey="True"`, so a `ContentTemplate` set on `ToolbarButton` is honoured. [CITED: github.com/lepoco/wpfui `src/Wpf.Ui/Controls/Button/Button.xaml` @4.3.0, lines 55-61 and 127-133]
- Shape (proposed, new XAML): `ContentTemplate` as a `Setter` in `ToolbarButton`, a `DataTemplate` whose `AccessText Text="{Binding}"` and whose `Path Data="{Binding Tag, RelativeSource={RelativeSource AncestorType=ui:Button}}"`; each button then reduces to `Content="{DynamicResource str_toolbar_findGames_access}" Tag="{StaticResource Icon.Scan}"` plus its existing command/tooltip/automation attributes. `ToolbarGlyphPath`'s `Fill` binds `Foreground` of the nearest `Control` ancestor (Styles.xaml:343-347); the template's visual position is unchanged so hover/pressed/disabled colouring should be identical. [ASSUMED: needs the screenshot comparison]
- Access keys: mnemonics registered by `AccessText` inside a `ContentTemplate` should still target the button, but this is the one behavioral risk of the change; D-07 already requires an explicit manual check (press Alt then the letter for each of the eight keyed buttons, plus the two bound-label buttons whose text changes with the selection count). [ASSUMED]
- Overflow menu: three 13-line `ui:MenuItem.Header` blocks at `:551-563`, `:564-576`, `:578-590`, differing in the Click handler, the glyph key (`Icon.OpenFolder`, `Icon.SaveFolder`, `Icon.Remove`) and the text key. Share the header through the `OverflowMenuItem` style (header template with icon in `Tag`) the same way. The `ContextMenu` renders in its own visual-tree root (comment at `:545-550`), so keep `DynamicResource` for text and `StaticResource` for geometries as today. Leave the three four-line Click handlers (ROADMAP). Risk: MEDIUM (visual identity, access keys).

**Item 20 — row-action button style and dialog footer style (premise: holds; conflicts with D-07)**
- Row-action duplication: `MainWindow.xaml:447-455` and `:521-529` are the same `Style BasedOn="{StaticResource IconActionButton}"` with a `DataTrigger Binding="{Binding IsBusy}" Value="True"` collapse. Extract one keyed style (for example `RowActionButton`, BasedOn `IconActionButton`) and use `Style="{StaticResource ...}"`; the `ui:DropDownButton` at `:534` deliberately uses `IconActionButton` directly and a different `IsEnabled` binding, so it stays. The comment at `:531-533` ("replacing the Style attribute with a Style element drops the icon sizing") describes the hazard to keep respecting.
- Dialog footers: `MinWidth` values are AddGame 80/80 (AddGameDialog.xaml:21, :23), ChangeSaveFolder 110/80 (:127, :130), Scan 90/90 (ScanWindow.xaml:21, :23), RestorePicker 90/90 (RestorePickerWindow.xaml:26, :28); top margins also differ (24, 24, 16, 24). "Update folder" is the longest label (`str_changeSaveFolder_confirm`, Strings.en.xaml:175); that is why ChangeSaveFolder needs 110. **One shared minimum that fits "Update folder" necessarily widens the other dialogs' buttons**, so item 20 cannot satisfy D-07's "Items 19/20 must show no visible difference" for the footers (Open Question 4). Recommendation: treat the footer width change as the item's one intended visible change, say so in the plan, and show it in the screenshot pairs; do not unify the top margins (ScanWindow's 16 differs by comment-documented intent at RestorePickerWindow.xaml:22-24).
- Risk: LOW (row style), MEDIUM (footer widths).

**Item 21 — spacing-token cleanup and palette rename (premise: holds)**
- Misused uniform tokens (all confirmed live): `md` at `MainWindow.xaml:202` (CatalogDisplay text, status bar), `sm` at `:215` (bulk-progress Border, next to `Padding="8,6"`) and `:634` (HyperlinkButton), `xs` at `:223` (ProgressBar) and `:605` (restore-step text). Each is a one-directional gap using a four-sided token, which also changes row/strip height. Replacing them with explicit one-sided literals (candidates: `16,0` at `:202`, `0,0,0,8` or `0,0,0,0` at `:215`, `0,4,0,0` at `:223` and `:605`, `8,0,0,0` at `:634`) changes layout on purpose; choose values from the screenshots, not from this document. [ASSUMED: candidate values]
- Unused tokens to delete (grep of `src/` for every key found no use outside the definitions): `lg` and `xl` (`Styles.xaml:60-61`), `radiusControlValue` (`:72`), `radiusControl` (`:74`); `radiusSurface`/`radiusSurfaceValue` are used (MainWindow.xaml:236-237, AddGameDialog.xaml:73) and stay. The comment at `RestorePickerWindow.xaml:22-24` ("lg spacing token (Margin="0,24,0,0")") refers to the deleted token and will go stale; update that comment (it is caused by this change). [VERIFIED: grep]
- Palette rename (D-06). Current: `App.xaml:39-40` defines `PaletteRedColor` `#FFDC6C64` and `PaletteRedBrush`; references at `Styles.xaml:148`, `:162` and `AddGameDialog.xaml:97`. **`tests/SavegameBunker.Tests/ResourceKeyTests.cs:75` asserts `Assert.Contains(@"x:Key=""PaletteRedBrush""", appXaml);`; it must change to `SavegameBunkerDangerBrush` in the same commit**, and the `ownedPrefixes = new[] { "Palette", "SavegameBunker" }` guard at `:48` loses its only `Palette` consumer (leaving the prefix is harmless; the file's doc comment at `:11` names `PaletteRedBrush` and should be updated). [VERIFIED: file read]
- WPF-UI 4.3.0 consumers of `PaletteRed*` (what the visible effect of dropping the override is): the library defines `PaletteRedColor` as `#F44336` and the brush in `Resources/Palette.xaml`; consumers are `Controls/Button/Button.xaml` (`Appearance="Danger"`: `Background`, `MouseOverBackground` at 0.9 opacity, `PressedBackground` at 0.7), `Controls/Snackbar/Snackbar.xaml` (the Danger appearance background), `Controls/Badge/Badge.xaml` (Danger appearance) and `Controls/TitleBar/TitleBar.xaml` (`PART_CloseButton`, `MouseOverBackground="{DynamicResource PaletteRedBrush}"` with white glyph). [CITED: github.com/lepoco/wpfui @4.3.0, `Palette.xaml:16,37`, `Button.xaml:385-395`, `Snackbar.xaml:261`, `Badge.xaml:53`, `TitleBar.xaml:199`] Because `App.xaml`'s own keys sit above its merged dictionaries, the app's `#FFDC6C64` currently overrides all of these app-wide. In this app: no `Appearance="Danger"` button, no `ui:Badge`, and `DialogService` only shows snackbars with `ControlAppearance.Success` (DialogService.cs:49, :51). [VERIFIED: grep of src/] **So the only visible change from the rename is the close button hover of the title bar on all five windows** (`MainWindow`, `AddGameDialog`, `ChangeSaveFolderDialog`, `RestorePickerWindow`, `ScanWindow`: each has a `ui:TitleBar`), from `#FFDC6C64` to the library's `#F44336`. App error text and icons keep `#FFDC6C64` through the new `SavegameBunkerDangerBrush`.
- Risk: LOW, but it is the one change whose test (`ResourceKeyTests`) fails to compile or assert if forgotten. Also correct the stale comment at `MainWindow.xaml:286-287` ("Neither command needs a CanExecute guard: BackupAsync/RestoreAsync already return early on a null or busy row."): both commands now declare `CanExecute` (`CanBackupRow`, `CanRestoreRow`, MainViewModel.cs:1122, :1138), so the Ctrl+B/Ctrl+R `KeyBinding`s are gated by them. One-line correction only (item 18's carve-out).

### Clean-code items: Tests

**Item 22 — shared fixtures (premise: count off by one in the benign direction)**
- Live counts. `GetTempPath` appears in 21 test files; 20 of them build `Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"))` and implement `IDisposable` (the 21st is `PathResolverTests`, which has no Dispose); `Dispose` bodies vary (deny-rule removal in `BackupServiceTests`, `MainViewModelTests`, `MainViewModelGuardPinTests`, `RestoreServiceTests`, `SaveStatusScannerTests`, `ServiceContractTests`, `KnownGamesScannerTests`; `RemoveJunctions` in `BackupServiceTests`, `RestoreServiceTests`, `SaveStatusScannerTests`, `KnownGamesScannerTests`; plain `Directory.Delete` elsewhere). `WaitForAsync` is defined five times with the same body and a `3000` default (AddGameDialogViewModelTests.cs:58, ChangeSaveFolderDialogViewModelTests.cs:38, MainViewModelGuardPinTests.cs:42, MainViewModelTests.cs:176, StartupUnsecuredPromptTests.cs:80) plus the variant `WaitForPendingDelete` (RestorePickerViewModelTests.cs:381-387). `GameWithSaves` is defined five times: `(fileName = "save.dat", content = "payload")` in BackupServiceTests.cs:47 and RestoreServiceTests.cs:46; `(id, displayName)` in BulkBackupCoordinatorTests.cs:46, RowActionsCoordinatorTests.cs:72, StatusSweepCoordinatorTests.cs:30. `RecordingDialogService` (MainViewModelTests.cs:15) is used by seven files. [VERIFIED: grep]
- Shape: `TempDir : IDisposable` beside `TestFileSystem.cs` that always runs deny-rule removal, junction cleanup, then `Directory.Delete` with the existing tolerated-exception behavior (swallow `IOException`/`UnauthorizedAccessException`); one shared `WaitForAsync`; two `GameWithSaves` helpers; `RecordingDialogService` into `RecordingDialogService.cs`. Test classes currently carry `private readonly string _dir;` and `_dir` is referenced throughout, so keep a `_dir` alias (for example `_dir = temp.Path`) to keep the diff mechanical.
- Risk: LOW-MEDIUM (mechanical, 20 files). Must come after the defect tests (D-09), before 24 and 23.

**Item 24 — unasserted polls and named timeouts (premise: count off)**
- `MainViewModelTests.cs:2486-2488`: the `while (...) Thread.Sleep(5);` loop has no assert after it (it sits in a synchronous test that cannot `await` because `GamesView` is dispatcher-bound); add a final `Assert.False(vm.Games.Any(... Checking))` after the loop. [VERIFIED: grep and read]
- **Timeout sites: ROADMAP says 12; live code has 19** `timeoutMs: 15000` call sites (16 in `MainViewModelTests.cs`, 3 in `MainViewModelGuardPinTests.cs:115, :170, :207`). Default `3000` appears in five helper signatures and once more inline (`RestorePickerViewModelTests.cs:383`, `AddMilliseconds(3000)`). [VERIFIED: `grep -c "timeoutMs: 15000"`]
- Named constants live in the shared helper from item 22 (for example `TestWait.Default = 3000`, `TestWait.RealIo = 15000`, `TestWait.NegativeProof = 300`). Keep the three "prove it did not happen" waits behind a named constant (`AddGameDialogViewModelTests.cs:560` `Task.Delay(300)`, `StartupUnsecuredPromptTests.cs:184` `Task.Delay(300)` and `:266` `Task.Delay(50)`); keep `Thread.Sleep(1100)` (`RestoreServiceTests.cs:374`, `:522`: archive names have one-second resolution) and the 20 ms NTFS-timestamp waits (`CatalogSessionTests.cs:76`, `GameCatalogTests.cs:427`, `SaveStatusScannerTests.cs:54`). Order after 22 and after item 3.

**Item 23 — split `MainViewModelTests.cs` (premise: holds)**
- 3272 lines, 155 `[Fact]`/`[Theory]` markers. Section markers (`// ---` comments) at lines 750, 1756, 1847, 2037, 2187, 2251, 2614, 2749, 2776, 2842. [VERIFIED: grep]
- Pitfall that changes risk: xUnit v2/v3 run tests of **one class** serially but **different classes in parallel** by default ("a test collection per test class"). [CITED: https://xunit.net/docs/running-tests-in-parallel] Splitting one class into ten multiplies the number of classes contending for real disk and the thread pool, which is exactly the load under which the 3 s waits flake. Mitigations: put the split classes in one `[Collection("MainViewModel")]` (keeps them serial, as today) and/or measure with the stress loop below. The shared private helpers (`CreateViewModel`, `CreateMultiGameViewModelAsync`, `CreateNamedViewModel`, `CreateViewModelWithSavedCatalogAsync`, ...) need one non-sealed fixture base class or a `MainViewModelFixture` helper; a base class is a documented reason to leave a class non-sealed under the CLAUDE.md convention.
- Review with `git diff --color-moved` (pure move), as the ROADMAP says. Order: last (after 22 and 24).

## Architecture Patterns

### System Architecture Diagram (item 0 and the restore guard, the one flow with a design decision)

```
Backup(game, backupRoot)
  PathResolver.TryResolve(SaveDirTemplate) --> saveDir (R)
        |
  InspectSaveDir(R): Empty / Unreadable / HasFiles
        |
  targetDir (T) = <backupRoot>\<id>; EnsureWritable creates T
        |
  excludedDirs = { backupRoot, T }
        |
  ArchiveSaveDir --> EnumerateRelativeFiles --> DirectoryWalk.ForEachFile(R, excludedDirs, onSkipped, onExcluded)
        |                 per subfolder S:
        |                   prune if excluded is-or-inside S                       (S contains/equals an excluded dir)
        |                   prune if S inside excluded AND excluded inside-or-equal R   (NEW rule)
        |                   else recurse;  pruned S --> onExcluded(relative S)  [archive pass only]
        |
  zero entries AND zero skipped? --> Fail (item 5)
        |
  DescribeVerificationFailure(same excludedDirs, no onExcluded) --> rename ladder --> Ok(created + skipped warning + "Left out: ..." line)

Restore(game, backupRoot, archive)
  resolve saveDir --> REFUSE if T inside-or-equal saveDir (supersedes today's "backupRoot inside saveDir")
        --> archive-in-T check --> limits --> safety backup --> confirm --> extract temp --> verify --> move old aside --> move new in
```

### Recommended structure for new code
```
src/SavegameBunker/Services/
├── PathCompare.cs            # internal: Normalize + IsSameOrInside (item 4), optionally IsFileSystemFault (item 9)
└── (SaveFolderStatus.cs)     # internal pure static Describe(folder) (item 16), or place under ViewModels/
tests/SavegameBunker.Tests/
├── PathCompareTests.cs       # new (item 4)
├── TempDir.cs                # new (item 22), beside TestFileSystem.cs
├── TestWait.cs               # new (items 22/24): shared WaitForAsync + named timeouts
└── RecordingDialogService.cs # moved out of MainViewModelTests.cs (item 22)
```

### Pattern: guard raised before the first await (items 1-2)
`RowActionsCoordinator.BackupAsync` already does this and states why: `row.IsBusy = true;` "is raised immediately, before EnsureBackupRootAsync's await" and released in a `finally` (RowActionsCoordinator.cs:66-70, :100-107). The bulk preflight guard follows the same shape: raise, `try { await EnsureBackupRootAsync(); ... } finally { release }`.

### Pattern: CommunityToolkit hook instead of attribute lists (item 14)
```csharp
// proposed (new code); hook name pattern verified at MainViewModel.cs:492
partial void OnIsBackupRunningChanged(bool value) => RequeryOperationGates();
private void RequeryOperationGates()
{
    RefreshCommand.NotifyCanExecuteChanged();
    ChangeBackupRootCommand.NotifyCanExecuteChanged();
    BackupAllChangedCommand.NotifyCanExecuteChanged();
    BackupSelectedCommand.NotifyCanExecuteChanged();
    RestoreSelectedCommand.NotifyCanExecuteChanged();
    BackupCommand.NotifyCanExecuteChanged();
    RestoreCommand.NotifyCanExecuteChanged();
}
```

### Anti-Patterns to Avoid
- **Pruning by "inside the backup root" when the walk root is below it.** That is item 0 itself.
- **Reordering restore's steps** to "fix" the archive-folder layout. Refuse before step one instead.
- **Making a new Services helper `public`.** `ServiceContractTests` will fail its surface fact.
- **Raising timeouts to cure flakiness** (D-05 forbids it) or adding `Thread.Sleep` to tests.
- **Reusing `BulkBackupTotal` as the preflight guard.** It shows the progress panel.
- **Adding resource keys for new messages.** Item 10 (messages through `Strings`) is dropped; existing backup warnings are inline English.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Skipped/left-out list display | A third list formatter | `DirectoryWalk.FormatSkippedList` | Already shared by the backup warning and the scan row; one display shape. |
| Path "inside" test | Another `StartsWith(root + sep)` | The new `PathCompare.IsSameOrInside` | Three divergent copies are the item 4 bug. |
| Fault predicates | Per-file exception lists | One `IsFileSystemFault` | .NET already makes the subclasses `IOException`s. |
| Command requery | More `[NotifyCanExecuteChangedFor]` lists | One `RequeryOperationGates()` | The lists already drifted. |
| Async test waits | `Thread.Sleep` loops or per-class copies | The shared `WaitForAsync` | The test host has no `SynchronizationContext`. |
| Drive probing in tests | Real `DriveInfo` calls | A `Func<string?>` seam | Real drives are the flake. |

**Key insight:** every item either removes a divergent copy of one rule or makes one rule explicit; none needs a new abstraction beyond a small internal static helper or a delegate seam.

## Common Pitfalls

### Pitfall 1: Fixing item 0 so the pinned layouts regress
**What goes wrong:** dropping "inside the backup root" pruning wholesale re-archives other games' archive folders when the backup root equals a shared save folder (pinned by `A_backup_root_equal_to_a_shared_save_folder_packs_no_other_games_archive_dir`).
**Why:** in that layout real subfolders and other games' archive folders are indistinguishable by position.
**How to avoid:** apply the "inside" direction only to excluded dirs that are inside or equal to the walk root.
**Warning signs:** the `""` row of `A_backup_folder_inside_or_equal_to_the_save_folder_is_honoured_and_never_archived` or the shared-folder fact fails.

### Pitfall 2: Item 5 failing a legitimately locked-only backup
**What goes wrong:** "zero entries means fail" breaks `Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path`.
**How to avoid:** gate on zero entries and zero skipped paths.

### Pitfall 3: Item 1 changing the contract of every startup-prompt test
**What goes wrong:** arming now evaluates immediately against the real sweep, consuming the single shot before the test forces its synthetic verdicts.
**How to avoid:** reorder each fact (set verdicts, then arm) and add one arm-after-settle fact.

### Pitfall 4: Forgetting `ResourceKeyTests` in item 21
**What goes wrong:** `Both_severity_colours_have_a_brush_to_go_with_them` still asserts `PaletteRedBrush` and fails after the rename.
**How to avoid:** update `ResourceKeyTests.cs:75` in the same commit.

### Pitfall 5: A public service helper
**What goes wrong:** `ServiceContractTests` fails its reflection surface fact for a new public Services type.
**How to avoid:** `internal static` for `PathCompare`, `IsFileSystemFault`, and any item 16 helper placed in Services.

### Pitfall 6: Item 7 test migration changes asserted messages
**What goes wrong:** in the disk-hash mode, the content check runs first, so a grown file reports a hash mismatch, not byte counts.
**How to avoid:** use the recorded-hash mode for the entry-count and byte-count tests.

### Pitfall 7: Class split changing parallelism (item 23)
**What goes wrong:** more test classes run concurrently, increasing load-sensitivity of 3 s waits.
**How to avoid:** one shared collection or a stress-loop measurement.

### Pitfall 8: Silent screenshot drift in XAML items
**What goes wrong:** a `ContentTemplate` changes access-key behavior or glyph colour tracking without any test noticing (no UI test project exists).
**How to avoid:** the D-07 screenshot pairs plus a manual Alt-key pass; build with `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` (XAML compile errors surface there).

## Code Examples

### Restore guard (item 0 / D-02), proposed
```csharp
// RestoreService.Restore, replacing the check at RestoreService.cs:68 (message key unchanged)
// today: if (BackupService.IsSameOrInside(backupRoot, saveDir))
if (PathCompare.IsSameOrInside(Path.Combine(backupRoot, game.Id), saveDir))
    return OperationResult.Fail(Strings.Format("str_restore_backupFolderInsideSaveFolder", game.DisplayName));
```

### Start-folder fix (item 3), proposed
```csharp
// DriveHints.SuggestBackupStartFolder, replacing DriveHints.cs:71
var type = drive.DriveType;
var ready = type is DriveType.Removable or DriveType.Fixed && drive.IsReady;
volumes.Add((drive.RootDirectory.FullName, type, ready));
```

### Prompt arming (item 1), proposed
```csharp
// MainViewModel; flag set in StartStatusScan's isFullSweep branch just before OnUi(PromptUnsecuredGamesOnce)
private bool _firstFullSweepSettled;
public void RequestStartupUnsecuredPrompt()
{
    _startupPromptPending = true;
    if (_firstFullSweepSettled)
        PromptUnsecuredGamesOnce();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Attribute lists of `NotifyCanExecuteChangedFor` per flag | `partial void OnXChanged` hook calling one requery method | CommunityToolkit.Mvvm 8.x partial-property style already in use here | One list to maintain |
| Exception lists naming `IOException` subclasses | `ex is IOException or UnauthorizedAccessException` | n/a (subclassing is long-standing) | Same behavior, one place |

**Deprecated/outdated in this codebase:** `.planning/codebase/` map (stale by 83 structural changes per the planning-run decision); all file/line facts above come from live code.

## Runtime State Inventory

Not a rename/refactor-of-identifiers phase; the behavioral changes do not rename or migrate stored data. Checked categories:
- **Stored data:** None. Existing archives are unaffected; item 0 only changes what *new* archives contain. Older archives that lack subfolders remain valid (and restore of them replaces the save folder with what they hold; unchanged semantics).
- **Live service config:** None. `games.json` schema is unchanged.
- **OS-registered state:** None.
- **Secrets/env vars:** None.
- **Build artifacts:** None stale. `bin/`/`obj/` are untracked build output.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | A `ContentTemplate` containing `AccessText` on `ToolbarButton` keeps Alt-mnemonics working on all eight keyed toolbar buttons | Item 19 | Keyboard access keys silently stop working; caught only by the manual pass in D-07 |
| A2 | `ToolbarGlyphPath`'s `Fill` binding (nearest `Control` ancestor) tracks hover/pressed/disabled identically when the content moves into a `DataTemplate` | Item 19 | Icons keep the wrong colour on hover; caught by screenshots |
| A3 | The restore picker's delete confirmation (content dialog hosted in the main window) is answerable while the modal picker is open | Item 11 | If not, deleting a backup from the picker deadlocks; a separate defect outside the listed items |
| A4 | Candidate one-sided margin values for item 21 (`16,0`, `0,4,0,0`, `8,0,0,0`, ...) express the intended layout | Item 21 | Wrong spacing; screenshots decide |
| A5 | The 3 s flake is caused by real-drive probing (not other load) | Item 3 | The seam would not cure it; the stress loop in Validation Architecture measures it |
| A6 | `RestoreService` refusing when `<backupRoot>\<id>` is inside or equal to the save folder is acceptable under CLAUDE.md's "no path refused" rule (restore refusing to replace the folder that holds the archives is the existing precedent at `RestoreService.cs:68`) | Item 0 / D-02 | Owner might see a new refusal as scope creep; D-02 explicitly anticipates the case |
| A7 | Recommended wording of the D-01 line and item 5 failure message | Items 0, 5 | Cosmetic (discretion item) |

## Open Questions

1. **Save folder inside `<backupRoot>\<id>` versus D-01's literal wording.**
   - Known: D-01 says folders that "lie inside a per-game archive folder" are excluded and reported. Probe: with save folder `B\x\saves`, today's backup silently drops `sub\b.dat` and restore leaves it gone. Archives live as files directly in `B\x`, outside `B\x\saves`, so excluding subfolders there protects nothing.
   - Unclear: whether the owner intends D-01 to cover that layout or only archive folders found under the walk root.
   - Recommendation: apply the "inside excluded" direction only when the excluded dir is inside or equal to the walk root (so that layout is archived, not excluded). This refines D-01's list to "a folder that is or contains the backup root, or an archive folder inside the save folder". Confirm with the owner in the plan's first task or record the refinement in SPEC.md.
2. **Save folder equal to `<backupRoot>\<id>` (backup side).**
   - Known: restore now refuses it (D-02 fix). Backup still walks the root's own files unfiltered, so a second backup packs earlier `.zip` files and trips over its own `.zip.tmp`.
   - Unclear: whether to also skip root-level `*.zip`/`*.zip.tmp` in that layout or leave it as a documented degenerate case.
   - Recommendation: leave backup behavior as is, document the layout in SPEC.md as supported for backup and refused for restore, and add a test that the refused restore leaves the archives intact.
3. **Item 5 message wording.** Recommendation: a failure that names the save folder and says it holds only the backup folder; do not reuse "Save folder is empty" (two tests assert it for truly empty folders).
4. **Item 20 footers versus D-07.** One shared minimum that fits "Update folder" widens the other dialogs' buttons (80/90 to roughly 110). Recommendation: accept as the item's only intended visible change and show it in the screenshot pairs; confirm the owner agrees "no visible difference" applies to the row-action style only.
5. **Item 19 access keys.** Two of the ten buttons have no `_` (ThemeToggle, BackupAllChanged); the ROADMAP says all do. Recommendation: the manual pass covers the eight keyed ones and verifies the two unkeyed ones show plain text.
6. **Item 11 picker confirmation.** Needs a human run (A3). Recommendation: one `checkpoint:human-verify` task; if it fails, record it as a new finding rather than expanding scope.
7. **Item 3 / D-04: rework versus delete.** `A_cancelled_first_backup_folder_prompt_writes_nothing` covers the row path, not the bulk path. Recommendation: rework into a reachable `BackupSelectedCommand` fact (and reuse it for item 2), which also satisfies D-04's first sentence; deleting is only valid if the owner accepts the row test as equivalent.
8. **Item 24 count.** ROADMAP says 12 call sites with 15000; live is 19. Recommendation: replace all 19 and say so in the plan.
9. **Item 0 noise.** In the shared-folder layout every top-level subfolder is named on every backup. Recommendation: truncate via `FormatSkippedList` (first five plus "and n more").
10. **Item 1 empty catalog.** Setting the settled flag for a zero-row full sweep closes a pre-existing mid-session-prompt quirk; recommend including it (one line) and testing it.
11. **Item 23 parallelism.** Recommendation: keep the split classes in one xUnit collection unless a stress run shows no regression.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, test | yes | 10.0.404 (global.json 10.0.400, `latestFeature`) | none needed |
| Windows desktop session | `dotnet test` (WPF host), D-07 screenshots | yes (Windows 11 Pro, this machine) | 10.0.26300 | Linux can only compile (`EnableWindowsTargeting`); not enough for tests |
| git | commits, `--color-moved` review | yes | n/a | none needed |
| `gh` CLI | looked up WPF-UI 4.3.0 sources | yes | n/a | WebFetch of raw GitHub files |
| Interactive screen capture | D-07 before/after pairs | not exercised in this session | n/a | `System.Drawing` `CopyFromScreen` from PowerShell, or Snipping Tool by the owner |
| Network drives | none required (the seam removes them from tests) | `G:`/`M:` unreachable on this machine | n/a | n/a |

**Missing dependencies with no fallback:** none. **Missing with fallback:** screenshot tooling needs an interactive desktop; if the executor runs headless, the owner takes the pairs.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (runner 4.0.1, Test SDK 18.12.0), `net10.0-windows` |
| Config file | none (no `xunit.runner.json`; default parallelism = one collection per test class) |
| Baseline (D-08) | **695 tests**; two full runs this session: first 693 passed / 2 failed (the two named flaky tests), second 695 passed; Release build 0 warnings, 0 errors |
| Quick run command | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~<Class>"` |
| Full suite command | `dotnet test SavegameBunker.sln -c Release -tl:off` |
| Build gate | `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` (must stay 0/0) |

### Phase Requirements to Test Map (ROADMAP items; no REQ IDs registered)
| Item | Behavior | Test Type | Automated Command | File Exists? |
|------|----------|-----------|-------------------|-------------|
| 0 | Save subfolders archived when save folder is inside backup root; exclusions reported as an info line, not as skipped paths | unit (real temp dirs) | `--filter "FullyQualifiedName~BackupServiceTests"` | extend existing |
| 0 / D-02 | Restore never deletes excluded content or the archives: cases (backup root inside save folder; save folder equal to `<B>\<id>`; save folder inside `<B>\<id>`; item-0 layout round-trip) | unit | `--filter "FullyQualifiedName~RestoreServiceTests"` | extend existing (new facts) |
| 1 | Arming after the first full sweep settled evaluates immediately; once per session | unit | `--filter "FullyQualifiedName~StartupUnsecuredPromptTests"` | extend; reorder existing facts |
| 2 | Row Backup/Restore, Refresh, ChangeBackupRoot, window close blocked while the bulk probe/picker is open; released after cancel | unit (picker hook, no timing) | `--filter "FullyQualifiedName~MainViewModelTests"` | extend; add `OnPickFolder` hook |
| 3 | Start folder never reads `IsReady` for non-fixed/removable drives (pure rule already tested); picker tests drive-independent via seam; bulk cancel fact reachable | unit | `--filter "FullyQualifiedName~DriveHintsTests|FullyQualifiedName~MainViewModelTests|FullyQualifiedName~StartupUnsecuredPromptTests"` | extend |
| 4 | `PathCompare` at drive roots and prefix lookalikes; `ToTemplate("E:\\")` | unit | `--filter "FullyQualifiedName~PathCompareTests|FullyQualifiedName~PathResolverTests"` | new `PathCompareTests.cs` (Wave 0) |
| 5 | Only-excluded-content save folder fails; locked-only still succeeds | unit | `--filter "FullyQualifiedName~BackupServiceTests"` | extend |
| 6, 9 | Behavior-preserving; rename ladder pins, skip pins | existing suite | full suite | existing |
| 7 | Verification modes (recorded-hash vs disk-hash) | unit | `--filter "FullyQualifiedName~BackupServiceTests"` | migrate 4 facts |
| 11 | Delegate passed from the view model; Confirm UI-thread branch removed/asserted | build + manual (WPF-bound `DialogService`) | build gate + manual picker run | no unit test possible |
| 14 | `BackupCommand`/`RestoreCommand` requery on `IsBusy`; gates requery on each op flag | unit | `--filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~MainViewModelGuardPinTests"` | extend |
| 15 | Coordinator reports progress through `Action<int,string>` via `_onUi`, in order | unit | `--filter "FullyQualifiedName~BulkBackupCoordinatorTests|FullyQualifiedName~MainViewModelGuardPinTests"` | migrate recorder |
| 16 | Shared `Describe` (found/not found/count one/many/unresolvable); `Browse` sync | unit | `--filter "FullyQualifiedName~AddGameDialogViewModelTests|FullyQualifiedName~ChangeSaveFolderDialogViewModelTests"` | existing + one new for the helper |
| 19, 20, 21 | Visual identity; access keys; red only where intended | manual (D-07 screenshots) + `ResourceKeyTests` + XAML build | `--filter "FullyQualifiedName~ResourceKeyTests"` + build gate | update `ResourceKeyTests.cs:75` |
| 22, 23, 24 | Pure test refactor; suite count unchanged (+ new defect tests) | existing suite | full suite; `git diff --color-moved` | n/a |

### Sampling Rate
- **Per task commit:** the filtered command for the touched class(es) plus the Release build gate.
- **Per plan/wave merge:** `dotnet test SavegameBunker.sln -c Release -tl:off`; record the count and keep it green (D-08; the count rises by the number of new facts).
- **Flake check for items 3, 22-24:** run the full suite repeatedly (for example five times in a loop: `for i in 1 2 3 4 5; do dotnet test SavegameBunker.sln -c Release -tl:off --no-build || break; done`) and require all green; the two named tests failed on this machine's first full run, so one clean run proves little.
- **Phase gate:** full suite green on Windows, CI green on the PR to `main`, screenshot pairs delivered for items 19-21.

### Wave 0 Gaps
- [ ] `tests/SavegameBunker.Tests/PathCompareTests.cs` — item 4
- [ ] `RecordingDialogService.OnPickFolder` hook (small edit in `MainViewModelTests.cs`, moved by item 22) — item 2
- [ ] Failing-first facts for items 0, 5 and the D-02 cases (the probe results above give the exact expected failures)
- [ ] Framework install: none; xUnit infrastructure exists

## Security Domain

`security_enforcement` is not set in `.planning/config.json`, so it is treated as enabled. This is a local desktop app with no auth, sessions or network surface; the relevant risk is filesystem integrity.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication / V3 Session Management | no | n/a (local app) |
| V4 Access Control | no | n/a |
| V5 Input Validation | yes | Path templates from a hand-edited `games.json` go through `PathResolver.TryResolve`; containment checks via the single `PathCompare.IsSameOrInside` (item 4) |
| V6 Cryptography | no change | SHA-256 verification already in `BackupService`; not touched |
| V12 Files and Resources | yes | Restore's replace-wholesale guard and archive-must-live-in-`<backupRoot>\<id>` check; reparse points never followed (unchanged) |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Restore deleting the folder that holds the backups (destructive layout) | Tampering / Denial of Service | Refuse when `<backupRoot>\<id>` is inside or equal to the save folder (D-02 fix); safety-backup-first ordering unchanged |
| Path prefix confusion (`E:\x` vs `E:\xy`, drive roots) | Tampering | One normalized separator-aware comparison (item 4) with prefix-lookalike tests |
| Silent data omission in a backup | Repudiation (user cannot tell what is missing) | The D-01 info line and the item 5 failure |
| Unobserved task exceptions in dialog code | Denial of Service | Unchanged: handlers must not throw; `DialogService.Error` keeps `ShowQueuedDiscardingFaults` |

## Sources

### Primary (HIGH confidence)
- Live repository at `C:\Dev\SavegameBunker`, HEAD `57811c1`: every file named in the per-item sections was read this session (services, view models, XAML, tests, SPEC.md, CHANGELOG.md, docs/DECISIONS.md, CONTEXT.md, ROADMAP.md, STATE.md, REQUIREMENTS.md).
- Scratch probe project built against the live `SavegameBunker.csproj` (outside the repo, in the session scratchpad) reproducing items 0, 4, 5 and the D-02 restore gap; two full `dotnet test` runs; `dotnet build ... --no-incremental` (0/0).
- WPF-UI 4.3.0 sources at tag `4.3.0` via GitHub (`gh api`): `src/Wpf.Ui/Resources/Palette.xaml`, `Controls/Button/Button.xaml`, `Controls/Snackbar/Snackbar.xaml`, `Controls/Badge/Badge.xaml`, `Controls/TitleBar/TitleBar.xaml`.

### Secondary (MEDIUM confidence)
- https://xunit.net/docs/running-tests-in-parallel — default parallelism is per test class/collection.
- Runtime reflection (`BaseType` of `PathTooLongException`, `DirectoryNotFoundException`, `FileNotFoundException`, `UnauthorizedAccessException`) printed `IOException`, `IOException`, `IOException`, `SystemException`.

### Tertiary (LOW confidence)
- None beyond the `[ASSUMED]` rows in the Assumptions Log (UI behavior that could not be run here).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — nothing new; versions read from the csproj files.
- Architecture / per-item findings: HIGH for Services and view-model items (code read, three defects reproduced); MEDIUM for XAML items (source reading only).
- Pitfalls: HIGH where tied to pinned tests (read), MEDIUM for UI behavior (A1-A3).

**Research date:** 2026-11-18
**Valid until:** 2026-12-18 (stable codebase; re-check line numbers if `dev/v1.1` moves before execution)
