---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
<!-- refreshed: 2026-11-17 -->
# Architecture

**Analysis Date:** 2026-11-17

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────┐
│                 Startup / composition root                           │
│   `src/SavegameBunker/App.xaml.cs` (OnStartup)                       │
└───────────────┬──────────────────────────────┬───────────────────────┘
                │ constructs                   │ constructs
                ▼                              ▼
┌───────────────────────────────┐   ┌──────────────────────────────────┐
│ Views (WPF / WPF-UI)          │   │ DialogService                    │
│ `src/SavegameBunker/Views/`   │◀──│ `Services/DialogService.cs`      │
│ `MainWindow.xaml(.cs)`        │   │ (only WPF-aware dialog impl.)    │
│ Behaviors/ Converters/        │   └───────────────▲──────────────────┘
└───────────────┬───────────────┘                   │ IDialogService
                │ DataContext / bindings            │
                ▼                                   │
┌──────────────────────────────────────────────────────────────────────┐
│ ViewModels  `src/SavegameBunker/ViewModels/`                         │
│  MainViewModel ─┬─ GameActions (internal)   ── backup/restore flows  │
│                 ├─ CatalogSession (internal) ── sole catalog writer  │
│                 ├─ GameRowViewModel, *DialogViewModel, ScanWindowVM  │
└─────────────────┬────────────────────────────────────────────────────┘
                  │ calls (no reverse dependency)
                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Services  `src/SavegameBunker/Services/` (no WPF references)         │
│  GameCatalog · BackupService · RestoreService · PathResolver         │
│  SaveStatusScanner · KnownGamesScanner/Catalog · BackupComparer      │
│  KnownFolders · DriveHints · DropFolder · HelpPath · Strings …       │
└─────────────────┬────────────────────────────────────────────────────┘
                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Data and OS:  games.json (catalog) · backup root folder (zip files)  │
│  save folders (templates) · Known Folders / environment variables    │
└──────────────────────────────────────────────────────────────────────┘
```

## Pattern Overview

**Overall:** Hand-rolled MVVM over a service layer, single WPF process, no DI container, no framework beyond WPF-UI and CommunityToolkit.Mvvm.

**Key Characteristics:**
- One composition root: `App.OnStartup` in `src/SavegameBunker/App.xaml.cs` constructs every object by hand (`new MainWindow()`, `new DialogService(...)`, `new MainViewModel(load, dialogService)`). Per the comment there, no DI container is used.
- One-directional dependencies: Views → ViewModels → Services → filesystem. Services never reference WPF, never reference ViewModels.
- Services are stateless and return values instead of throwing across their boundary (`OperationResult` in `src/SavegameBunker/Models/OperationResult.cs`; `CatalogLoad` returned by `GameCatalog.LoadAsync`).
- View models reach UI only through `IDialogService` (`src/SavegameBunker/Services/IDialogService.cs`). They never call `MessageBox` or pickers directly.
- Filesystem work runs off the UI thread via `Task.Run` (inside `GameActions`, `BackupService` callers) and `AsyncRelayCommand` from CommunityToolkit.Mvvm.
- The catalog file is the entire configuration: `games.json` holds `backupRoot` and the game list.

## Layers

**Startup / Composition:**
- Purpose: Resolve the catalog path, load it, build the window and services, apply theme, handle unhandled exceptions.
- Location: `src/SavegameBunker/App.xaml.cs`, `src/SavegameBunker/App.xaml`
- Contains: `OnStartup`, `OnDispatcherUnhandledException`, `OnUnobservedTaskException`, theme application (`ApplyTheme`, `UseSystemColoursForAccent`).
- Depends on: `GameCatalog`, `DialogService`, `MainViewModel`, `MainWindow`, WPF-UI services.
- Used by: The WPF runtime only.

**Views:**
- Purpose: XAML layout and code-behind that only wires things WPF cannot bind declaratively.
- Location: `src/SavegameBunker/MainWindow.xaml(.cs)`, `src/SavegameBunker/Views/*.xaml(.cs)`
- Contains: `MainWindow` (FluentWindow with `RootDialogHost` and `RootSnackbarPresenter`), `AddGameDialog`, `ChangeSaveFolderDialog`, `RestorePickerWindow`, `ScanWindow`.
- Depends on: ViewModels via `DataContext`; `Behaviors/` and `Converters/`.
- Used by: `App.OnStartup`.

**ViewModels:**
- Purpose: Observable state and commands. Orchestrate flows; delegate every file operation to services.
- Location: `src/SavegameBunker/ViewModels/`
- Contains: `MainViewModel` (the hub, about 1,900 lines), `GameRowViewModel`, `GameActions`, `CatalogSession`, `AddGameDialogViewModel`, `ChangeSaveFolderDialogViewModel`, `RestorePickerViewModel`, `ScanWindowViewModel`, `ScanHitRowViewModel`.
- Depends on: Services, Models, `IDialogService`.
- Used by: Views (bindings), `App.OnStartup` (construction).

**Services:**
- Purpose: All filesystem, catalog, path and backup logic. Stateless, testable without WPF.
- Location: `src/SavegameBunker/Services/`
- Contains: `GameCatalog` (static load/validate/save), `BackupService` (zip write, verify, list), `RestoreService` (destructive swap), `PathResolver` (template expansion), `SaveStatusScanner`, `KnownGamesScanner`, `KnownGamesCatalog`, `BackupComparer`, `DropFolder`, `DriveHints`, `HelpPath`, `WikiUrl`, `Strings`, `DialogService`.
- Depends on: Models only (and the BCL / Windows APIs).
- Used by: ViewModels.

**Models:**
- Purpose: Plain data carriers and small enums.
- Location: `src/SavegameBunker/Models/`
- Contains: `Catalog`, `Game` (init-only), `KnownGame`, `OperationResult`, `BackupArchive`, `BackupComparison`, `SaveStatus`, `ScanResult`, `RestorePrompt`, `RowSeverity`, `AddGameResult`.
- Depends on: Nothing.
- Used by: Everything above.

## Data Flow

### Primary Request Path (startup and catalog load)

1. `App.OnStartup` resolves the catalog path from command-line args, falling back to `games.json` beside the executable (`src/SavegameBunker/App.xaml.cs`, `GameCatalog.ResolvePath` in `src/SavegameBunker/Services/GameCatalog.cs`).
2. `GameCatalog.LoadAsync(path)` returns a `CatalogLoad`. An absent file is seeded with a commented template and an empty list; a malformed file yields `FatalError` and is left untouched.
3. `MainWindow` is constructed, its dialog and snackbar hosts are wired into `DialogService`, and `MainViewModel(load, dialogService)` is set as `DataContext`.
4. `vm.ReportLoad()` (`src/SavegameBunker/ViewModels/MainViewModel.cs`, around line 710) reports problems and seeding to the status line and dialog.
5. The status sweep (`SaveStatusScanner`, `BackupService.ListBackups`) runs on background threads and updates `GameRowViewModel` rows.

### Backup Flow

1. User action on a row or the toolbar triggers an `AsyncRelayCommand` on `MainViewModel`.
2. `MainViewModel` delegates to `GameActions.BackupAsync` (`src/SavegameBunker/ViewModels/GameActions.cs`), which runs `BackupService.Backup` inside `Task.Run`.
3. `BackupService` writes a zip beside the backup root using its own directory walk, not `ZipFile.CreateFromDirectory`, so directory junctions are not followed (`src/SavegameBunker/Services/BackupService.cs`, around line 108). It then verifies the archive.
4. Result is an `OperationResult`; `MainViewModel.Report()` pushes it to `StatusMessage` and to a dialog through `IDialogService`.

### Restore Flow (destructive; order is load-bearing)

1. `GameActions.CheckRestorePreconditionsAsync` and `PickRestoreAsync` run the pre-flight refusals and the restore picker.
2. `RestoreService` (`src/SavegameBunker/Services/RestoreService.cs`) executes, in this exact order:
   - a. Safety backup via `BackupService.Backup(game, backupRoot, "-pre-restore")` (around line 90)
   - b. Confirm with user through a callback (around line 95); declining deletes the safety archive
   - c. Extract the chosen archive to a temp sibling directory (`ZipFile.ExtractToDirectory`, around line 114)
   - d. Move the live save directory aside (`Directory.Move`, around line 151)
   - e. Rename the temp directory into place (`Directory.Move`, around line 167); on failure, move the old directory back (around line 175)
3. The restore never short-circuits this sequence.

### Catalog Write Flow

1. Any catalog mutation (add game, change backup folder, change save folder, remove game) goes through `CatalogSession` (`src/SavegameBunker/ViewModels/CatalogSession.cs`).
2. `CatalogSession` calls `GameCatalog.SaveAsync(path, catalog, lastWriteTimeUtc)`, the only catalog writer in the tree. The last-write stamp detects a file edited externally.
3. On failure, the in-memory change is rolled back at the same index.

**State Management:**
- Observable UI state lives on `MainViewModel` and `GameRowViewModel` (CommunityToolkit `[ObservableProperty]` and `ObservableObject`).
- Catalog state lives in `CatalogSession`, which is internal and held as a private field.
- Selection is tracked by id in `MainViewModel._selectedIds`; collection access is guarded by `_gamesSyncLock`.
- Nothing is persisted besides `games.json` and backup archives. Theme is re-derived from Windows on every launch.

## Key Abstractions

**`OperationResult`:**
- Purpose: Success/failure with a user-facing message, returned by every mutating service method.
- Examples: `src/SavegameBunker/Models/OperationResult.cs`
- Pattern: `OperationResult.Fail(message)` / `Ok`; services never throw across their boundary.

**`CatalogLoad`:**
- Purpose: Everything the app learns from loading `games.json`: path, valid entries, per-entry problems, seeded flag, fatal error.
- Examples: returned by `GameCatalog.LoadAsync` in `src/SavegameBunker/Services/GameCatalog.cs`.
- Pattern: Immutable record, reassigned via `with` in `CatalogSession`.

**`IDialogService`:**
- Purpose: The single seam between view models and any dialog, message, picker or shell-open action.
- Examples: `src/SavegameBunker/Services/IDialogService.cs` (interface), `src/SavegameBunker/Services/DialogService.cs` (WPF implementation, marshals to UI thread).
- Pattern: Interface plus one WPF-aware implementation; view models stay thread-agnostic.

**`GameActions` and `CatalogSession`:**
- Purpose: Extracted internal collaborators that `MainViewModel` owns privately to keep it from growing further. `GameActions` owns per-game backup and restore orchestration; `CatalogSession` owns the catalog record and the only path to `GameCatalog.SaveAsync`.
- Examples: `src/SavegameBunker/ViewModels/GameActions.cs`, `src/SavegameBunker/ViewModels/CatalogSession.cs`
- Pattern: `internal sealed`, `private readonly` field only. Never exposed as a public property, so XAML cannot bind to them.

**`PathResolver`:**
- Purpose: Expands save-folder templates: `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`, plus the pseudo-variables `%DOCUMENTS%` and `%SAVEDGAMES%` resolved from `SpecialFolder.MyDocuments` and `SHGetKnownFolderPath(FOLDERID_SavedGames)`.
- Examples: `src/SavegameBunker/Services/PathResolver.cs`, `src/SavegameBunker/Services/KnownFolders.cs`
- Pattern: Static pure function with `TryResolve`. Pseudo-variables are used instead of hardcoded `%USERPROFILE%\Documents` because of OneDrive Known Folder Move.

**`Game` and `Catalog`:**
- Purpose: Catalog entries (`Id`, `DisplayName`, `SaveDirTemplate`) and the file root (`BackupRoot`, `Games`).
- Examples: `src/SavegameBunker/Models/Game.cs`, `src/SavegameBunker/Models/Catalog.cs`
- Pattern: `Game` is init-only; edits construct a new instance. The `Id` names the backup folder and must never change on edit.

## Entry Points

**`App.OnStartup`:**
- Location: `src/SavegameBunker/App.xaml.cs`
- Triggers: Process start of `SavegameBunker.exe`.
- Responsibilities: Theme and high-contrast setup, path resolution, catalog load, window and service composition, `ReportLoad`, unsecured-games prompt arming. Startup has no fatal path; the outer try/catch is a last resort.

**`GameCatalog.ResolvePath`:**
- Location: `src/SavegameBunker/Services/GameCatalog.cs`
- Triggers: `OnStartup`.
- Responsibilities: First command-line argument, or `games.json` beside the exe. In DEBUG builds the `--selftest-` switches are filtered out first in `App.xaml.cs`.

**`MainWindow`:**
- Location: `src/SavegameBunker/MainWindow.xaml.cs`
- Triggers: Constructed in `OnStartup`.
- Responsibilities: Hosts the main view and the root dialog and snackbar presenters.

**Test host:**
- Location: `tests/SavegameBunker.Tests/`
- Triggers: `dotnet test SavegameBunker.sln`
- Responsibilities: Exercises services and view models directly. There is no `SynchronizationContext`, so assert settled state with `WaitForAsync`.

## Architectural Constraints

- **Threading:** WPF UI thread for view models and dialogs. Filesystem work runs on thread pool via `Task.Run`. `DialogService` marshals every call onto the UI thread because services can call back from background threads. `Progress<T>` callbacks may arrive late on the test host.
- **Global state:** `App.Current` resources (accent colour, `Strings` loaded with `Strings.Preload()` on the UI thread before worker threads read it). `Strings` is a static lookup in `src/SavegameBunker/Services/Strings.cs`.
- **No DI container:** Construction is manual. Adding a dependency means adding a constructor parameter and passing it in `App.OnStartup` or the owning view model.
- **Single catalog writer:** Only `CatalogSession` calls `GameCatalog.SaveAsync`. Adding a second writer breaks the stamp check and the rollback logic.
- **Circular imports:** Not applicable to .NET project references; no project cycles observed. `GameActions` constructs its own `RestoreService` from the `BackupService` it receives.
- **Dependency limit:** Exactly two shipping NuGet packages (WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2), per `src/SavegameBunker/SavegameBunker.csproj` and CLAUDE.md.
- **Trimming:** Off. Reflection-based WPF-UI resource loading depends on it.
- **No registry access:** Nothing reads `InstallLocation` or `UninstallString`; do not add it.
- **No volume policy:** The backup destination is honoured as chosen. No code may refuse, redirect or warn based on the volume type.

## Anti-Patterns

### Dialogs or message boxes from a view model

**What happens:** Some code in a view model or service calls `MessageBox` or a picker directly.
**Why it's wrong:** Breaks testability and thread-agnosticism; the test host has no WPF dispatcher.
**Do this instead:** Add a method to `IDialogService` (`src/SavegameBunker/Services/IDialogService.cs`) and implement it in `DialogService`.

### Growing `MainViewModel` further

**What happens:** `src/SavegameBunker/ViewModels/MainViewModel.cs` is about 1,900 lines and already hosts many commands, status sweep and advisory logic.
**Why it's wrong:** Every new flow adds to the largest file and to the hardest-to-test surface.
**Do this instead:** Follow the `GameActions` / `CatalogSession` pattern: a new `internal sealed` collaborator under `src/SavegameBunker/ViewModels/`, held as a private readonly field, with no WPF types.

### Services throwing across their boundary

**What happens:** A service method throws for an expected failure, such as a missing folder or locked file.
**Why it's wrong:** Command handlers passed to `AsyncRelayCommand` must not throw; an escaping exception surfaces on the UI thread.
**Do this instead:** Catch at the service boundary and return `OperationResult.Fail(...)`. The one permitted bare catch is in `BackupService` read-only listing (around line 253).

### Writing the catalog without `CatalogSession`

**What happens:** Code calls `GameCatalog.SaveAsync` directly.
**Why it's wrong:** Bypasses the last-write stamp check and the rollback-on-failure path.
**Do this instead:** Add a method to `CatalogSession` and call it from `MainViewModel`.

### Reordering restore steps

**What happens:** Deleting or moving the live save before the safety backup is written.
**Why it's wrong:** The deletion is only defensible because the safety backup already exists.
**Do this instead:** Keep the sequence in `RestoreService` exactly: safety backup, extract to temp, move old aside, move new in.

## Error Handling

**Strategy:** Return values, not exceptions. Surface every failure to the user.

**Patterns:**
- Services return `OperationResult`; `MainViewModel.Report()` writes to `StatusMessage` and shows a dialog.
- Load problems are carried in `CatalogLoad.Problems` (per-entry) and `CatalogLoad.FatalError` (whole file).
- Unhandled exceptions: `App.OnDispatcherUnhandledException` and `OnUnobservedTaskException` report through `DialogService` when available and fall back to a plain message box otherwise. `CrashReport` (`src/SavegameBunker/Services/CrashReport.cs`) formats the report.
- Best-effort cleanup (deleting an unwanted safety archive, removing a temp directory) is wrapped in `try { } catch { /* best effort */ }` and never changes the reported result.

## Cross-Cutting Concerns

**Logging:** No logging library (constraint). Failures are reported to the user via `OperationResult`, the status line, and dialogs.

**Validation:** Catalog validation in `GameCatalog.LoadAsync` (per-entry problems, id checks via `IdSlug` in `src/SavegameBunker/Services/IdSlug.cs`). Path templates validated by `PathResolver`. Add-game flow in `AddGameDialogViewModel` and `CatalogSession.PartitionNewGames`.

**Authentication:** Not applicable. The app is local-only.

**Localisation:** All UI strings come from `src/SavegameBunker/Themes/Strings.en.xaml` through `src/SavegameBunker/Services/Strings.cs`. Everything written to disk is English.

---

*Architecture analysis: 2026-11-17*
