---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
<!-- refreshed: 2026-11-18 -->
# Architecture

**Analysis Date:** 2026-11-18

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────┐
│                 WPF presentation (Views + code-behind)                │
│  `src/SavegameBunker/MainWindow.xaml`  `src/SavegameBunker/Views/*`    │
├──────────────────────────────────────────────────────────────────────┤
│                 ViewModels (CommunityToolkit.Mvvm)                    │
│  `ViewModels/MainViewModel.cs` (root, composition of collaborators)   │
│   ├─ CatalogSession   ├─ GameActions    ├─ BulkBackupCoordinator      │
│   ├─ RowActionsCoordinator             ├─ StatusSweepCoordinator     │
│   └─ Dialog VMs (AddGame, ChangeSaveFolder, RestorePicker, ScanWindow)│
├──────────────────────────────────────────────────────────────────────┤
│      IDialogService  (implemented by `Services/DialogService.cs`)     │
├──────────────────────────────────────────────────────────────────────┤
│                 Services (stateless, return OperationResult)          │
│  `GameCatalog` `BackupService` `RestoreService` `PathResolver`        │
│  `SaveStatusScanner` `BackupComparer` `KnownGamesScanner` ...         │
├──────────────────────────────────────────────────────────────────────┤
│  Models (records/classes)   │  Filesystem: games.json, save dirs,     │
│  `Models/*.cs`              │  backup root (zip archives)             │
└──────────────────────────────────────────────────────────────────────┘
```

Entry point and composition root: `src/SavegameBunker/App.xaml.cs` (`App.OnStartup`).

## Pattern Overview

**Overall:** Hand-rolled MVVM over a service layer, with no DI container and no framework beyond WPF-UI and CommunityToolkit.Mvvm. The project ships exactly two NuGet packages (`src/SavegameBunker/SavegameBunker.csproj`: WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2).

**Key Characteristics:**
- One composition root: `App.OnStartup` constructs every object by hand and wires them as plain locals or fields. There is no service locator and no DI container.
- One dependency direction: Views -> ViewModels -> Services -> Models. Services never reference ViewModels or Views.
- Services are stateless and never throw across their boundary. They return `OperationResult` (`src/SavegameBunker/Models/OperationResult.cs`) or a dedicated result record (`CatalogLoad`, `ScanResult`, `BackupComparison`).
- All UI interaction goes through `IDialogService` (`src/SavegameBunker/Services/IDialogService.cs`). View models never call `MessageBox`, folder pickers or WPF dialog types directly.
- Filesystem work runs off the UI thread via `Task.Run` (wrapped in `AsyncRelayCommand` or in coordinators). Progress is reported through `IProgress<T>` constructed on the UI thread.
- Only one destructive operation exists (restore). Its ordering is load-bearing (see Data Flow).
- The catalog file `games.json` is the entire configuration. There is no settings file.

## Layers

**Startup / Composition Root:**
- Purpose: Resolve the catalog path, load it, build the WPF window and services, and show the UI. Startup has no fatal path: a missing catalog is seeded, a malformed one is reported in the running window.
- Location: `src/SavegameBunker/App.xaml.cs`, `src/SavegameBunker/App.xaml`
- Contains: Theme selection (Light/Dark/High Contrast via WPF-UI), `DispatcherUnhandledException` and `UnobservedTaskException` handlers, debug-only self-test switches (`--selftest-*`).
- Depends on: `GameCatalog`, `MainViewModel`, `MainWindow`, `DialogService`, WPF-UI `ContentDialogService` and `SnackbarService`.
- Used by: The OS (process entry).

**Presentation (Views):**
- Purpose: XAML layout and thin code-behind. Views bind to view models and do not carry business logic.
- Location: `src/SavegameBunker/MainWindow.xaml(.cs)`, `src/SavegameBunker/Views/` (AddGameDialog, ChangeSaveFolderDialog, RestorePickerWindow, ScanWindow)
- Contains: XAML, `DialogResultBehavior` and `CornerClip` in `src/SavegameBunker/Behaviors/`, value converters in `src/SavegameBunker/Converters/`.
- Depends on: ViewModels, WPF-UI controls.
- Used by: `App.OnStartup` (creates `MainWindow`), `DialogService` (creates dialog windows).

**ViewModels:**
- Purpose: Own bound state and commands. Delegate to coordinators for multi-step flows.
- Location: `src/SavegameBunker/ViewModels/`
- Contains:
  - `MainViewModel.cs` (root, 1500+ lines): bound collections, toolbar commands, aggregate guards (`HasBackupTargets`, `IsAnyRowChecking`, `IsAnyRowBusy`), status line, header.
  - `CatalogSession.cs` (internal): the only holder of the loaded catalog record and the only caller of `GameCatalog.SaveAsync`.
  - `GameActions.cs` (internal): per-game backup/restore orchestration and the three pre-flight refusals.
  - `BulkBackupCoordinator.cs` (internal): "Back up all changed", "Back up selected", "Restore selected" loops.
  - `RowActionsCoordinator.cs` (internal): per-row Back up / Restore / Open save folder flows through `IRowActionHost`.
  - `StatusSweepCoordinator.cs` (internal): background save-status sweep and free-space advisory I/O.
  - `BulkRunRules.cs` (internal static): pure rules for bulk runs.
  - `GameRowViewModel.cs`, `ScanHitRowViewModel.cs`, `RestorePickerViewModel.cs` (`RestorePickerRowViewModel`), `AddGameDialogViewModel.cs`, `ChangeSaveFolderDialogViewModel.cs`, `ScanWindowViewModel.cs`.
- Depends on: Services, Models, `IDialogService`.
- Used by: Views via data binding.

**Services:**
- Purpose: All filesystem, catalog, archive and path logic. Stateless, no WPF types in the core services.
- Location: `src/SavegameBunker/Services/`
- Contains: `GameCatalog` (load/validate/save `games.json`), `BackupService` (zip write, verify, list, delete, free space), `RestoreService` (safety backup, extract, swap), `PathResolver` (template expansion), `SaveStatusScanner`, `BackupComparer`, `KnownGamesCatalog` / `KnownGamesScanner` (find games), `DropFolder`, `HelpPath`, `WikiUrl`, `ShellOpen`, `Strings`, `CrashReport`, plus small helpers (`IdSlug`, `SizeFormat`, `ElapsedFormat`, `DirectoryWalk`, `ReparseFile`, `KnownFolders`, `DriveHints`).
- Exception to the no-WPF rule: `src/SavegameBunker/Services/DialogService.cs` is the only WPF-aware type in the folder. It implements `IDialogService`, owns the `Window` and WPF-UI services, and marshals every call onto the UI thread with `Dispatcher.CheckAccess()` / `InvokeAsync`.
- Depends on: Models only (and `Strings` for authored text).
- Used by: ViewModels.

**Models:**
- Purpose: Data carriers and result types.
- Location: `src/SavegameBunker/Models/`
- Contains: `Catalog` (class, `BackupRoot`, `Games`), `Game` (class, `Id`, `DisplayName`, `SaveDirTemplate`), `KnownGame`, `OperationResult` (record: `Success`, `Message`, `Cancelled`), `BackupArchive`, `BackupComparison`, `SaveStatus`, `ScanResult`/`ScanHit`, `RestorePrompt`, `AddGameResult`, `RowSeverity`.
- Depends on: nothing in the app.
- Used by: everything.

## Data Flow

### Primary Request Path: Startup

1. `App.OnStartup` sets the theme and calls `Strings.Preload()` on the UI thread (`src/SavegameBunker/App.xaml.cs`).
2. `GameCatalog.ResolvePath(args)` picks the catalog path, falling back to `games.json` beside the exe (`src/SavegameBunker/Services/GameCatalog.cs`).
3. `GameCatalog.LoadAsync(path)` returns a `CatalogLoad` (path, valid games, per-entry problems, `Seeded`, `FatalError`). It seeds a commented template when the file is absent and reports, but never overwrites, a malformed file.
4. `MainWindow` is constructed, then `ContentDialogService` and `SnackbarService` are attached to its named hosts (`RootDialogHost`, `RootSnackbarPresenter`).
5. `new DialogService(window, ...)` and `new MainViewModel(load, dialogService)` are created; `window.DataContext = vm`; `window.Show()`.
6. `vm.ReportLoad()` surfaces problems; `vm.RequestStartupUnsecuredPrompt()` arms a once-per-session prompt that the first full status sweep decides.

### Primary Request Path: Backup (single row)

1. Row button -> `RowActionsCoordinator` (`src/SavegameBunker/ViewModels/RowActionsCoordinator.cs`).
2. Resolve the backup root from `CatalogSession.TryGetConfiguredBackupRoot`; if unset, `IDialogService.PickFolder` opens the first-backup picker.
3. `GameActions.BackupAsync` -> `Task.Run(() => BackupService.Backup(game, root, progress))`.
4. `BackupService` expands `SaveDirTemplate` through `PathResolver.TryResolve`, writes a zip, verifies it, and returns `OperationResult`.
5. The result is shown via `MainViewModel.Report` (status line plus dialog or snackbar) and the row's comparison is refreshed.

### Primary Request Path: Restore (the only destructive flow)

Owned by `RestoreService.Restore` (`src/SavegameBunker/Services/RestoreService.cs`), reached through `GameActions`. The order is load-bearing and must not be reordered or short-circuited:

1. **Safety backup**: `_backup.Backup(game, backupRoot, "-pre-restore", out safetyPath)`. Aborts the restore if it fails. Skipped only when the save directory is empty or absent.
2. **Confirm**: `confirm(new RestorePrompt(saveDir, safetyPath))`. If declined, the safety archive is deleted.
3. **Extract** the chosen archive to a temp sibling directory (`ZipFile.ExtractToDirectory`), with archive-size limits (`MaxArchiveEntries`, `MaxArchiveUncompressedBytes`).
4. **Move aside** the current save directory (`Directory.Move(saveDir, oldAsideDir)`).
5. **Rename** the temp directory into place (`Directory.Move(tempDir, saveDir)`). On failure, the old directory is moved back.
6. **Cleanup** of the old aside directory is best-effort.

### Secondary Flow: Save status sweep

1. `MainViewModel` constructor calls `StartStatusScan()`, which delegates to `StatusSweepCoordinator`.
2. Each row's save directory is compared with its newest backup through `SaveStatusScanner` and `BackupComparer`, on worker threads, reporting into the row through `OnUi` (the UI-thread dispatcher).
3. A per-row token captured in a closure prevents stale results from overwriting newer ones.

### Secondary Flow: Catalog write

1. A command in `MainViewModel` (Add Game, Change Save Folder, Remove, Refresh) calls `CatalogSession`.
2. `CatalogSession.SaveAsync` calls `GameCatalog.SaveAsync(path, catalog, lastWriteTimeUtc)`. The stamp check refuses the write if the file changed on disk since it was loaded, which protects hand-edits.
3. On success the session refreshes its stamp and `MainViewModel` rebuilds the affected `GameRowViewModel`s.

**State Management:**
- The authoritative state is `games.json` plus the backup root on disk. In memory, `CatalogSession` holds the last loaded record (replaced by `with` copies).
- UI state lives on `MainViewModel` and `GameRowViewModel` (`ObservableObject`). Row selection is a `HashSet<string>` keyed by `Game.Id`, not by row reference, because rows are rebuilt on refresh.
- No state is persisted besides the catalog. Theme choice and the startup prompt are session-only.

## Key Abstractions

**`OperationResult`:**
- Purpose: The universal service return type; carries success, a user-facing message and a cancel flag.
- Examples: `src/SavegameBunker/Models/OperationResult.cs`
- Pattern: Services return it instead of throwing. `MainViewModel.Report()` turns it into a status line and a dialog.

**`CatalogLoad`:**
- Purpose: The full result of loading the catalog, including problems and seed state.
- Examples: `src/SavegameBunker/Services/GameCatalog.cs` (record at line 23)
- Pattern: Load never throws; fatal conditions go into `FatalError`.

**`IDialogService`:**
- Purpose: Every user-facing dialog, folder picker and external open.
- Examples: `src/SavegameBunker/Services/IDialogService.cs`, implemented by `src/SavegameBunker/Services/DialogService.cs`
- Pattern: Interface in the same folder as its only WPF implementation. Calls are marshalled to the UI thread internally, so callers may be on any thread.

**`IRowActionHost`:**
- Purpose: Lets `RowActionsCoordinator` read and update the root view model without a reference to it.
- Examples: `src/SavegameBunker/ViewModels/RowActionsCoordinator.cs`, implemented by `MainViewModel`.
- Pattern: Narrow host interface for a collaborator.

**Coordinators (`internal sealed`):**
- Purpose: Each owns one multi-step flow and reports back through callbacks (`Action<Action> onUi`) and `IProgress<T>` instances built by the view model.
- Examples: `BulkBackupCoordinator`, `RowActionsCoordinator`, `StatusSweepCoordinator`, `GameActions`
- Pattern: Held as `private readonly` fields of `MainViewModel`, never exposed as public properties, so XAML cannot bind to them.

**`PathResolver` templates:**
- Purpose: Turn catalog strings such as `%SAVEDGAMES%\Game\Saves` into absolute paths.
- Examples: `src/SavegameBunker/Services/PathResolver.cs`
- Pattern: `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%` are environment variables. `%DOCUMENTS%` and `%SAVEDGAMES%` are substituted from `SpecialFolder.MyDocuments` and the Known Folder API first, so OneDrive-relocated folders resolve correctly.

## Entry Points

**Application entry:**
- Location: `src/SavegameBunker/App.xaml.cs` (`OnStartup`).
- Triggers: Launch of `SavegameBunker.exe`.
- Responsibilities: Composition root (see Data Flow above).

**Catalog path argument:**
- Location: `GameCatalog.ResolvePath` (`src/SavegameBunker/Services/GameCatalog.cs`)
- Triggers: `dotnet run --project src/SavegameBunker -- D:\retro.json` or `SavegameBunker.exe D:\retro.json`.
- Responsibilities: Choose the catalog file. Falls back to `games.json` beside the exe.

**Test entry:**
- Location: `tests/SavegameBunker.Tests/` (xUnit, `SavegameBunker.Tests.csproj`). Tests reach `internal` types through `InternalsVisibleTo` in `SavegameBunker.csproj`.
- Triggers: `dotnet test SavegameBunker.sln` and CI (`.github/workflows/test.yml`).

## Architectural Constraints

- **Threading:** WPF UI thread for all bound state. Filesystem work runs on thread-pool threads via `Task.Run`. `DialogService` and `MainViewModel.OnUi` are the marshalling points. `Games` is synchronized with `EnableCollectionSynchronization` so tests without a dispatcher stay safe.
- **Global state:** `App.CurrentTheme` and `App.IsHighContrastActive` are static properties in `src/SavegameBunker/App.xaml.cs`. `Strings` holds a static authored dictionary loaded by `Strings.Preload()` (`src/SavegameBunker/Services/Strings.cs`). `GameCatalog.DefaultPath` depends on `AppContext.BaseDirectory`.
- **Circular imports:** Not applicable (C#). No project-reference cycles: `SavegameBunker.Tests` references the app project only.
- **Single catalog writer:** Only `CatalogSession` calls `GameCatalog.SaveAsync`. This is the structural "no second writer" rule.
- **No backup-location policy:** `BackupService` and `RestoreService` accept any folder the user chose. There is no code path that refuses, redirects or relocates a backup based on its volume.
- **Trimming disabled:** WPF-UI resolves resources reflectively; the project must not enable trimming (`SavegameBunker.csproj` comments).
- **Forbidden registry access:** Nothing reads `InstallLocation` or `UninstallString`. Game launch and installation detection are out of scope.

## Anti-Patterns

### Restoring without the safety backup first

**What happens:** Any new code path that replaces a save directory without first calling `BackupService.Backup(..., "-pre-restore", ...)`.
**Why it's wrong:** Deleting the current save set is only defensible because the safety step has already put that set on the backup volume.
**Do this instead:** Extend `RestoreService.Restore` (`src/SavegameBunker/Services/RestoreService.cs`) and keep the order: safety backup -> extract to temp sibling -> move aside -> rename in.

### Writing the catalog from anywhere but `CatalogSession`

**What happens:** Calling `GameCatalog.SaveAsync` from a view model method directly.
**Why it's wrong:** It bypasses the changed-on-disk stamp check in `CatalogSession.SaveAsync` and the stamp refresh, so a hand-edited `games.json` can be overwritten.
**Do this instead:** Add a method to `CatalogSession` (`src/SavegameBunker/ViewModels/CatalogSession.cs`) and call it from `MainViewModel`.

### Showing dialogs or touching WPF from a service or view model

**What happens:** A service calls `MessageBox.Show` or a view model opens a folder picker.
**Why it's wrong:** It breaks testability (xUnit has no UI) and the thread marshalling that `DialogService` guarantees.
**Do this instead:** Add a member to `IDialogService` and implement it in `src/SavegameBunker/Services/DialogService.cs`.

### Throwing from a service

**What happens:** A service method lets an exception escape to the caller.
**Why it's wrong:** `AsyncRelayCommand` handlers that throw surface on the UI thread and break the "report, do not crash" contract.
**Do this instead:** Catch inside the service and return `OperationResult.Fail(...)` or the matching result record.

## Error Handling

**Strategy:** Results, not exceptions, across service boundaries. Startup and background faults are caught at the application level and reported through `IDialogService.Error`.

**Patterns:**
- Services return `OperationResult` (`Success`, `Message`, `Cancelled`).
- `GameCatalog.LoadAsync` returns problems and a fatal error instead of throwing. A malformed catalog is reported and the file is left untouched.
- `App.OnDispatcherUnhandledException` marks the exception handled and reports it through `CrashReport.Describe` (`src/SavegameBunker/Services/CrashReport.cs`). `OnUnobservedTaskException` calls `SetObserved()` first.
- A last-resort `try/catch` in `App.OnStartup` shows a native `MessageBox` for failures before any window exists.

## Cross-Cutting Concerns

**Logging:** No logging framework. Problems are surfaced to the user through the status line and dialogs.
**Validation:** Catalog entries are validated in `GameCatalog` (`src/SavegameBunker/Services/GameCatalog.cs`), with per-entry problems returned to the UI. Template expansion validates through `PathResolver.TryResolve`.
**Authentication:** Not applicable. The app is local-only and has no network service.
**Localization:** Authored strings are loaded from `src/SavegameBunker/Themes/Strings.en.xaml` (embedded as a resource) through `Strings.Get(key)`. A test (`tests/SavegameBunker.Tests/ResourceKeyTests.cs`) checks key usage.

---

*Architecture analysis: 2026-11-18*
