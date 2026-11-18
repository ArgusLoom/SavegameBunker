# Phase 2: Audit follow-ups - Pattern Map

**Mapped:** 2026-11-18
**Files analyzed:** 27 (new or modified)
**Analogs found:** 25 / 27

All analog paths are git-tracked source (checked against `git ls-files src tests`). Line numbers are live at HEAD 57811c1 on `dev/v1.1`. No `.claude/skills/` exists. This is a refactor and defect phase, so most "analogs" are the file being modified or its direct sibling. Where RESEARCH.md already gives the exact edit shape, this map points to it instead of repeating it.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/SavegameBunker/Services/PathCompare.cs` (new, internal) | utility | transform | `Services/DriveHints.cs` (internal static, pure rule) plus the `IsSameOrInside` body being moved (`BackupService.cs:792-807`) | exact |
| `Services/DirectoryWalk.cs` (mod: prune rule, `onExcluded`) | service | file-I/O | itself, `onSkipped` channel (`:34-44`) | exact |
| `Services/BackupService.cs` (mod: items 0, 5, 6, 7, 9) | service | file-I/O | itself (`:105-215`) | exact |
| `Services/RestoreService.cs` (mod: guard `:68`, `:76`) | service | file-I/O | itself (`:59-78`) | exact |
| `Services/PathResolver.cs` (mod: `ToTemplate` `:150-165`) | utility | transform | itself | exact |
| `Services/DriveHints.cs` (mod: IsReady only for Fixed/Removable) | utility | request-response | itself (`:66-75`) | exact |
| `Services/ReparseFile.cs` (mod: item 9 predicate) | utility | file-I/O | `DirectoryWalk.cs:53,68,82,95` catch filters | role-match |
| `Services/DialogService.cs` (mod: item 11) | service (WPF) | request-response | itself (`:135-138`, `:190-192`) | exact |
| `Services/IDialogService.cs` (mod if delegate passed) | interface | request-response | itself | exact |
| `ViewModels/MainViewModel.cs` (items 1, 2, 3, 14, 15) | view model | event-driven | `RowActionsCoordinator.cs:60-107` guard-before-await | exact |
| `ViewModels/BulkBackupCoordinator.cs` (item 15) | coordinator | batch | itself (`:99-130`) | exact |
| `ViewModels/GameActions.cs` (item 11 delegate) | view model helper | request-response | itself (`:115`) | exact |
| `ViewModels/AddGameDialogViewModel.cs`, `ChangeSaveFolderDialogViewModel.cs` (item 16) | view model | request-response | each other (`AddGame...:394-407` vs `ChangeSave...:90-102`) | exact |
| Save-folder `Describe` helper (new, internal static) | utility | transform | `Services/SizeFormat.cs` / `ElapsedFormat.cs` (pure static formatters) | role-match |
| `MainWindow.xaml` (items 19, 20, 21, comment fix `:286-287`) | view | n/a | `Themes/Styles.xaml` `ToolbarButton` `:298-335` | exact |
| `Themes/Styles.xaml` (items 19, 20, 21) | view | n/a | itself | exact |
| `App.xaml` (D-06 rename `:39-40`) | view | n/a | itself (`SavegameBunkerCautionBrush` sibling) | exact |
| `Views/AddGameDialog.xaml`, `ChangeSaveFolderDialog.xaml`, `ScanWindow.xaml`, `RestorePickerWindow.xaml` (footer style, `:97`) | view | n/a | `ChangeSaveFolderDialog.xaml:127-130` | exact |
| `tests/.../PathCompareTests.cs` (new) | test | transform | `DriveHintsTests.cs` (synthetic inputs, pure rule) | role-match |
| `tests/.../TempDir.cs` (new) | test util | file-I/O | `TestFileSystem.cs` plus the 20 duplicated `_dir`/`Dispose` blocks | role-match |
| `tests/.../TestWait.cs` (new) | test util | transform | `WaitForAsync` copy, `MainViewModelGuardPinTests.cs:42-48` | exact |
| `tests/.../RecordingDialogService.cs` (new, moved) | test fake | request-response | `MainViewModelTests.cs:10-50` | exact (move) |
| `BackupServiceTests.cs` / `RestoreServiceTests.cs` (new facts) | test | file-I/O | `BackupServiceTests.cs:240-259`, `:747-770` | exact |
| `StartupUnsecuredPromptTests.cs` (reorder, rework `:274`) | test | event-driven | `MainViewModelTests.cs:400-412` | exact |
| `MainViewModelGuardPinTests.cs` (item 2 guard fact) | test | event-driven | `MainViewModelTests.cs:400-412` plus `RecordingDialogService.OnReport` | exact |
| `ResourceKeyTests.cs` (`:75`, doc comment `:11`) | test | n/a | itself | exact |
| `MainViewModelTests.cs` split (item 23) | test | n/a | itself | exact |
| `SPEC.md`, `CHANGELOG.md`, `docs/DECISIONS.md` | doc | n/a | existing sections (SPEC.md:218-222, CHANGELOG `[Unreleased]` `### Fixed`) | exact |

## Pattern Assignments

### `Services/DirectoryWalk.cs` (service, file-I/O): item 0 prune rule and `onExcluded`

**Analog:** itself. Copy the `onSkipped` channel shape (`:40-44`):
```csharp
public static void ForEachFile(
    string rootDir, Action<FileInfo> onFile, IReadOnlyCollection<string>? excludedDirs = null,
    Action<string>? onSkipped = null)
{
    void Skip(string path) => onSkipped?.Invoke(Path.GetRelativePath(rootDir, path));
```
Add `Action<string>? onExcluded = null` with a matching local `Excluded(path)`. The doc must say deliberate exclusions go to `onExcluded`, never `onSkipped`.

**Prune site to replace** (`:106-111`):
```csharp
if (excludedDirs is not null
    && excludedDirs.Any(excluded =>
        BackupService.IsSameOrInside(sub, excluded) || BackupService.IsSameOrInside(excluded, sub)))
{
    continue;
}
```
Replace with the D-10 rule (RESEARCH.md:117-119), calling `PathCompare.IsSameOrInside` (drops the dependency on `BackupService`). Call `Excluded(sub)` before `continue`. The `FormatSkippedList` helper (`:141-147`) is reused for the "Left out" line, so do not add a second formatter.

**Catch-filter style** (item 9): the existing `catch (Exception ex) when (ex is UnauthorizedAccessException or ... or IOException)` at `:53,68,82,95` becomes `when (PathCompare.IsFileSystemFault(ex))`, or whatever the helper is named.

---

### `Services/BackupService.cs` (service, file-I/O): items 0, 5, 6, 7, 9

**Analog:** itself.

**Exclusion list** (`:112-116`, keep as is):
```csharp
var excludedDirs = new[] { NormalizeDir(backupRoot), NormalizeDir(targetDir) };
```
**Item 0 message append** mirrors the skipped warning (`:195-201`, `:215-216`):
```csharp
var createdMessage = Strings.Format("str_backup_created", game.DisplayName);
if (skippedPaths.Count > 0)
    createdMessage += DescribeSkippedPathsWarning(skippedPaths);
...
private static string DescribeSkippedPathsWarning(IReadOnlyCollection<string> skippedPaths) =>
    $" Warning: {skippedPaths.Count} path(s) could not be read and are NOT in this backup: {DirectoryWalk.FormatSkippedList(skippedPaths)}";
```
Add a sibling `leftOut` list (separate from `skippedPaths`) and a neutral `" Left out: ..."` line built with `DirectoryWalk.FormatSkippedList`. Collect `leftOut` only from the archive pass (`ArchiveSaveDir`, call at `:124`). The verification re-walk (`:135-136`) must NOT pass `onExcluded`. Inline English, no new `Strings` key.

**Item 5 failure path** copies the existing failure shape (`:126-131`, `:137-141`):
```csharp
TryDelete(tempPath);
return OperationResult.Fail($"Backup failed for {game.DisplayName}: ...");
```
Gate: `entryHashes.Count == 0 && skippedPaths.Count == 0`. This is required by the pinned test `Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path` (`BackupServiceTests.cs:240-259`). Leave `"Save folder is empty: ..."` (`:82`) alone.

**Item 6:** extract only the rename ladder `:148-191` into a private static method. Keep the exact text at `:180-181` and `MaxRenameRetries` semantics (bare name plus `-1..-100`). Keep both catch levels.

**Item 4 removal:** delete `NormalizeDir`/`IsSameOrInside` at `:792-807` once `PathCompare` exists. Keep `NormalizeDir` private use at `:116` pointing to `PathCompare.Normalize`.

---

### `Services/PathCompare.cs` (new utility, transform)

**Analog:** the moving code plus the `internal static class` plus XML-doc style of `DriveHints.cs:18-31`.

Source to move (`BackupService.cs:792-807`):
```csharp
private static string NormalizeDir(string path) =>
    Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

internal static bool IsSameOrInside(string candidate, string parent)
{
    var child = NormalizeDir(candidate);
    var root = NormalizeDir(parent);
    return child.Equals(root, StringComparison.OrdinalIgnoreCase)
        || child.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
```
Bug to fix: a drive root keeps its trailing separator (`E:\`), so `root + sep` becomes `E:\\`. Append a separator only when `root` does not already end with one. File-scoped namespace `SavegameBunker.Services`, `internal static` (public types fail `ServiceContractTests.cs:142-146`). If item 9's `IsFileSystemFault` lives here, keep it `internal`.

Callers to update: `DirectoryWalk`, `RestoreService.cs:68` and `:76`, `PathResolver.ToTemplate`.

---

### `Services/PathResolver.cs` (utility, transform): item 4

**Analog:** itself, `:150-165`. Current:
```csharp
var target = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar);
...
.Select(c => (c.Token, Value: Path.GetFullPath(c.Value!).TrimEnd(Path.DirectorySeparatorChar)))
...
var remainder = target.Length == best.Value.Length ? "" : target[(best.Value.Length + 1)..];
```
`TrimEnd('\\')` turns `E:\` into `E:` (current-dir-relative). Replace both with the `PathCompare.Normalize` form that keeps a root intact, and compute `remainder` so the `+ 1` separator skip only applies when the candidate lacks a trailing separator.

---

### `Services/RestoreService.cs` (service, file-I/O): D-02 / D-11

**Analog:** itself, `:64-69`:
```csharp
if (BackupService.IsSameOrInside(backupRoot, saveDir))
    return OperationResult.Fail(Strings.Format("str_restore_backupFolderInsideSaveFolder", game.DisplayName));
```
Change to `PathCompare.IsSameOrInside(Path.Combine(backupRoot, game.Id), saveDir)` (strict superset of the old check). Reuse the same message key. Sits before any write, so restore order (CLAUDE.md) is untouched. `:76` also switches to `PathCompare`.

---

### `Services/DriveHints.cs` (utility): item 3 / D-05

**Analog:** itself, `:66-75`:
```csharp
foreach (var drive in drives)
{
    try
    {
        volumes.Add((drive.RootDirectory.FullName, drive.DriveType, drive.IsReady));
    }
    catch (IOException) { continue; }
    catch (UnauthorizedAccessException) { continue; }
}
```
Read `drive.DriveType` first and evaluate `IsReady` only for `Fixed`/`Removable`, else `false`. The class doc (`:26-30`) already states why network drives are excluded. `ChooseStartFolder` and `DriveHintsTests` stay valid.

---

### `ViewModels/MainViewModel.cs` (view model, event-driven)

**Item 1.** Fields and sites: `RequestStartupUnsecuredPrompt` at `:735`, `PromptUnsecuredGamesOnce` guard at `:764`, sweep closing callback at `:1099-1100`, and the zero-row early return at `:1066-1067`. Follow RESEARCH.md:134-135 (a `_firstFullSweepSettled` bool set before `OnUi(PromptUnsecuredGamesOnce)` and in the zero-row path).

**Item 2 guard pattern to copy** from `RowActionsCoordinator.cs:66-70, 100-107`:
```csharp
// row.IsBusy ... is raised immediately, before EnsureBackupRootAsync's await ...
row.IsBusy = true;
try
{
    var root = await _host.EnsureBackupRootAsync();
    ...
}
finally
{
    row.IsBusy = false;
}
```
Apply in `RunBulkBackupAsync` (`:1330-1334`): raise `IsBulkPreparing` before `await EnsureBackupRootAsync()`, release in a `finally` wrapping the whole body. Add it to `IsAnyOperationRunning` (`:279-280`) and to `CanBackupRow` (`:1132`) and `CanRestoreRow` (`:1147`). Declare it as a private-set `[ObservableProperty]` partial property like `BulkBackupTotal` (`:429-439`, `public partial int BulkBackupTotal { get; private set; }`). Do not reuse `BulkBackupTotal`.

**Item 14.** Replace the attribute list at `:433-439`, plus the lists at `:625-629` and `:669-673`, with hooks, using the existing hook style (`partial void OnSearchTextChanged(string value)` at `:492`):
```csharp
partial void OnBulkBackupTotalChanged(int value) => RequeryOperationGates();
private void RequeryOperationGates() { /* Refresh, ChangeBackupRoot, BackupAllChanged, BackupSelected, RestoreSelected, Backup, Restore */ }
```
Call it from the `IsBusy` branch (`:197-201`). Leave `IsScanRunning`'s single `FindGamesCommand` attribute and the narrower subset notifications at `:128-130`, `:191-193`, `:213-214`.

**Item 3 seam.** Line `:1467`: `var start = await Task.Run(DriveHints.SuggestBackupStartFolder);`. Inject a `Func<string?>` through an optional trailing constructor parameter (default `DriveHints.SuggestBackupStartFolder`), the same optional-delegate style as the Phase 1 coordinator seams.

**Item 15.** The late-arrival guard at `:1337-1343` (`Progress<(int Index, string Name)>` with `if (!IsBulkBackupRunning) return;`) goes away; pass `SetBulkProgress` as an `Action<int,string>` (the restore path already does this).

---

### `ViewModels/BulkBackupCoordinator.cs` (coordinator, batch): item 15

**Analog:** itself plus the restore path's `Action<int,string> onItem` (called directly). Replace `IProgress<(int Index, string Name)>` and `progress.Report((i + 1, row.DisplayName))` (`:99-104`) with `var index = i + 1; _onUi(() => onItem(index, row.DisplayName));`, matching how `_onUi` already wraps every row write (`:109, :125, :130`). Keep `if (targets.Count == 0) return null;`.

---

### `Services/DialogService.cs` and `ViewModels/GameActions.cs` (item 11)

**Analog:** `DialogService.cs:190-192`:
```csharp
var vm = new RestorePickerViewModel(gameDisplayName, backups, this, path => new BackupService().DeleteArchive(path));
```
Delegate comes from `GameActions.cs:115` (the sole caller; it already holds `_backup`). Test fakes that implement `IDialogService` if the signature changes: `RecordingDialogService` (`MainViewModelTests.cs:15`), `ThrowingDialogService` (`AddGameDialogViewModelTests.cs:12`), nested `ThrowingConfirmDialogService` (`StartupUnsecuredPromptTests.cs:214`). The `Confirm` UI-thread branch at `:135-138` becomes a `Debug.Assert(!_owner.Dispatcher.CheckAccess(), ...)` with the single `InvokeAsync(...).Task.Unwrap()` path. Keep the doc comment about not running inline.

---

### `AddGameDialogViewModel.cs` and `ChangeSaveFolderDialogViewModel.cs` (item 16)

**Analogs:** each other. `ResolveFolder` bodies are identical (Add `:394-407`, Change `:90-102`) apart from the missing-folder string (Add caches `FolderMissingStatus` at `:30`; Change calls `Strings.Get("str_common_folderNotFound")` inline). Move into one `internal static string Describe(string folder)`. Leave the token-guarded `StartResolve`/`ReportResolveAsync` pair. `BrowseAsync` (ends `return Task.CompletedTask;`, `:59-66` / `:186-195`) becomes a sync `[RelayCommand] private void Browse()`. `BrowseCommand` binds at `AddGameDialog.xaml:90-91` and `ChangeSaveFolderDialog.xaml:141-142`.

---

### Views: `MainWindow.xaml`, `Themes/Styles.xaml`, `App.xaml`, dialogs (items 19-21)

**Item 19 toolbar template.** Analog: `ToolbarButton` style, `Styles.xaml:298-335`. Add a `Setter Property="ContentTemplate"` there. Current per-button block (`MainWindow.xaml:36-48`):
```xml
<ui:Button x:Name="ThemeToggleButton" ... Style="{StaticResource ToolbarButton}" ...>
    <StackPanel Orientation="Vertical">
        <Viewbox Style="{StaticResource ToolbarGlyph}">
            <Path Style="{StaticResource ToolbarGlyphPath}" Data="{StaticResource Icon.Theme}"/>
        </Viewbox>
        <AccessText Text="{DynamicResource str_toolbar_toggleTheme}" Style="{StaticResource ToolbarButtonLabel}"/>
    </StackPanel>
</ui:Button>
```
Reduce to `Content=` (text) plus `Tag=` (geometry). Keep `DynamicResource` for text and `StaticResource` for geometry, as the overflow menu comment at `:545-550` requires. Two labels are bound (`BackupSelectedLabel`, `RestoreSelectedLabel`); two buttons (theme toggle, "Back up all changed") carry no `_` key, so the template must render both kinds identically. Verify access keys manually per D-07.

**Item 20 row action.** Duplicate blocks at `MainWindow.xaml:447-455` and `:521-529`:
```xml
<ui:Button.Style>
    <Style TargetType="ui:Button" BasedOn="{StaticResource IconActionButton}">
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsBusy}" Value="True">
                <Setter Property="Visibility" Value="Collapsed"/>
```
Extract a keyed `RowActionButton` style `BasedOn="{StaticResource IconActionButton}"`. Leave the `ui:DropDownButton` at `:534`. Respect the hazard comment at `:531-533`. Dialog footers: `MinWidth` AddGame 80/80 (`AddGameDialog.xaml:21,23`), ChangeSaveFolder 110/80 (`:127,:130`), Scan 90/90 (`ScanWindow.xaml:21,23`), RestorePicker 90/90 (`RestorePickerWindow.xaml:26,28`). Per D-12 one shared minimum near 110 is the item's only intended visible change. Do not unify top margins.

**Item 21 palette rename.** `Styles.xaml:148` and `:162` (`{DynamicResource PaletteRedBrush}`), `AddGameDialog.xaml:97`, `App.xaml:39-40` definitions: rename to `SavegameBunkerDangerColor`/`SavegameBunkerDangerBrush`, value `#FFDC6C64` unchanged. Only the title-bar close hover reverts to the library's `#F44336`. Update `ResourceKeyTests.cs:75` in the same commit (`Assert.Contains(@"x:Key=""PaletteRedBrush""", appXaml);`) and its doc comment at `:11`. Delete unused tokens `lg`, `xl`, `radiusControlValue`, `radiusControl` (`Styles.xaml:60-61,72,74`) and update the stale `lg` comment at `RestorePickerWindow.xaml:22-24`. Correct the stale comment at `MainWindow.xaml:286-287`.

---

### Test files

**Failing-first service facts** (D-09 current helpers): copy `BackupServiceTests.cs:240-259` (locked-file fact; uses `GameWithSaves()`, `_service.Backup(game, backupRoot)`, `BackupDirFor`, asserts no `*.zip.tmp`) and the theory at `:747-770`:
```csharp
[Theory]
[InlineData("Backups")]
[InlineData("")]
public void A_backup_folder_inside_or_equal_to_the_save_folder_is_honoured_and_never_archived(string relativeRoot)
{
    var game = GameWithSaves();
    var saveDir = Path.Combine(_dir, "saves");
    var backupRoot = Path.Combine(saveDir, relativeRoot);
    var first = _service.Backup(game, backupRoot);
    ...
    var second = _service.Backup(game, backupRoot, "-second", out var secondPath);
    using var zip = ZipFile.OpenRead(secondPath!);
    var entry = Assert.Single(zip.Entries);
```
Item 0 facts mirror this with the save folder inside the backup root and inspect `zip.Entries`. D-02/D-11 facts go in `RestoreServiceTests.cs`; assert refusal and that every archive and the save folder survive.

**Bulk picker-cancel fact (D-13) and item 2 guard fact.** Analog `MainViewModelTests.cs:400-412`:
```csharp
var (vm, dialog, catalogPath) = CreateViewModel(backupRoot: "");
dialog.FolderToReturn = null;
vm.BackupCommand.Execute(vm.Games[0]);
await WaitForAsync(() => dialog.PickFolderCalls == 1);
Assert.Empty(Directory.GetFiles(_dir, "*.zip", SearchOption.AllDirectories));
Assert.False(File.Exists(catalogPath));
Assert.Empty(dialog.ErrorMessages);
```
Rework `StartupUnsecuredPromptTests.cs:274-293` to select the row and use `BackupSelectedCommand`. For the item 2 observation point, add `Action? OnPickFolder` to `RecordingDialogService` mirroring `OnReport` (`MainViewModelTests.cs:~25-40`, invoked at the start of the method before recording).

**Startup prompt facts (item 1).** Existing flow in `StartupUnsecuredPromptTests.cs:30-60`: `CreateViewModelAsync` waits for the construction sweep, then each fact does `vm.RequestStartupUnsecuredPrompt()`, then `ApplyBackupComparison(...)`, then `PromptUnsecuredGamesOnce()`. Reorder each fact so synthetic verdicts apply before arming, and add one arm-after-settle fact.

**Item 22/24 helpers.** `WaitForAsync` template (`MainViewModelGuardPinTests.cs:42-48`):
```csharp
private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
{
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (!condition() && DateTime.UtcNow < deadline)
        await Task.Delay(5);
    Assert.True(condition(), "Timed out waiting for the expected view model state.");
}
```
Move into `TestWait.cs` with named constants. Temp dir pattern: `Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"))` plus `IDisposable` (deny-rule removal, junction cleanup, `Directory.Delete`, swallow `IOException`/`UnauthorizedAccessException`); keep a `_dir` alias so diffs stay mechanical.

## Shared Patterns

### Services never throw (error contract)
**Source:** `BackupService.cs:126-131`, `RestoreService.cs:59-69`.
**Apply to:** every Services change. Return `OperationResult.Fail(...)`, call `TryDelete(tempPath)` before returning on failure. New Services helpers are `internal` (`ServiceContractTests.cs:142-146` reflects over public types).

### Paths: one comparator
**Source:** new `PathCompare` (from `BackupService.cs:792-807`).
**Apply to:** `DirectoryWalk`, `RestoreService:68,:76`, `PathResolver.ToTemplate`, `BackupService`.

### Filesystem fault filter
**Source:** `DirectoryWalk.cs:53` style `catch (Exception ex) when (...)`.
**Apply to:** item 9 sites (`BackupService.cs:293, 384-387, 681`; `DirectoryWalk.cs:53,68,82,95`; `DriveHints.cs:73-74`; `ReparseFile.cs:42-43,53-54,64-67`). `DirectoryNotFoundException`, `FileNotFoundException`, `PathTooLongException` all derive from `IOException`, so `ex is IOException or UnauthorizedAccessException` is behavior-identical. Leave `DeleteArchive` catches (`BackupService.cs:332,339`) and the two stale-temp cleanups alone.

### Guard raised before first await, released in finally
**Source:** `RowActionsCoordinator.cs:66-70, 100-107`.
**Apply to:** item 2 (`IsBulkPreparing`).

### UI boundary
**Source:** CLAUDE.md. View models only use `IDialogService`; `DialogService` is the only WPF-aware class. Keep that when adding the item 11 delegate and the item 3 seam.

### Test async waiting
**Source:** `MainViewModelGuardPinTests.cs:42-48`.
**Apply to:** all view-model tests. Never snapshot after a command; never raise timeouts to cure flakiness (D-05).

### Docs (D-03)
**Source:** `SPEC.md:218-222` (backupRoot inside save directory bullet, under "### 3. Backup"), `CHANGELOG.md` `## [Unreleased]` existing `### Fixed` block (bold lead sentence style), `docs/DECISIONS.md` "Backup and restore" section (add a row only for the new restore refusal). English only. Record the D-10 refined rule and the D-11 layout (supported for backup, refused for restore) in SPEC.md.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `ContentTemplate`-based toolbar `DataTemplate` (item 19) | view | n/a | No existing `ContentTemplate`-on-style pattern in the repo. Use RESEARCH.md:223 and verify by screenshot. WPF-UI's `Button.xaml` ContentPresenter honours `ContentTemplate` and `RecognizesAccessKey`. |
| `IsBulkPreparing` observable flag | view model | event-driven | No gate flag exists for the pre-claim await. Closest shape is `IsBackupRunning`/`BulkBackupTotal` declaration plus the `RowActionsCoordinator` guard. |

## Metadata

**Analog search scope:** `src/SavegameBunker/{Services,ViewModels,Themes,Views}`, `src/SavegameBunker/MainWindow.xaml`, `tests/SavegameBunker.Tests`.
**Files read for excerpts:** DirectoryWalk, DriveHints, BackupService (`:105-215`, `:785-810`), RestoreService (`:55-85`), PathResolver (`:140-172`), MainViewModel (targeted), RowActionsCoordinator (`:60-110`), several test files (targeted), Styles.xaml and MainWindow.xaml (targeted). Other line numbers are taken from RESEARCH.md (verified there against HEAD 57811c1).
**Pattern extraction date:** 2026-11-18
