---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# Coding Conventions

**Analysis Date:** 2026-11-18

## Naming Patterns

**Files:**
- One public type per file, file name equals type name: `src/SavegameBunker/Services/BackupService.cs`, `src/SavegameBunker/ViewModels/GameRowViewModel.cs`
- Views are paired XAML and code-behind: `src/SavegameBunker/Views/ScanWindow.xaml` + `ScanWindow.xaml.cs`
- Suffixes encode role: `*ViewModel` (view models), `*Coordinator` (extracted MainViewModel collaborators, e.g. `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs`), `*Service` (stateless services), `*Scanner`, `*Catalog`, `*Dialog`, `*Window`, `*Tests`
- Interfaces take the `I` prefix: `IDialogService` (`src/SavegameBunker/Services/IDialogService.cs`), `IRowActionHost`
- Models are records or plain classes in `src/SavegameBunker/Models/`: `OperationResult`, `CatalogLoad`, `RowSeverity`, `BackupComparisonKind`

**Functions and methods:**
- PascalCase for all methods, including async ones. Async methods end in `Async` when they return `Task`: `LoadAsync`, `SaveAsync`, `BackupAsync`
- Synchronous filesystem work returns `OperationResult` rather than throwing: `BackupService.Backup(...)` in `src/SavegameBunker/Services/BackupService.cs`
- Command handlers are plain methods carrying `[RelayCommand]` (CommunityToolkit.Mvvm) and the generator produces the `*Command` property. Write `public void Backup()`, bind `BackupCommand`. Example: `src/SavegameBunker/ViewModels/MainViewModel.cs` line 326 onward
- Gating predicates are named `Can*` and wired via `[RelayCommand(CanExecute = nameof(CanBackupRow))]` (`MainViewModel.cs` around line 1122)

**Variables and fields:**
- Private instance fields: `_camelCase` with leading underscore. Examples in `src/SavegameBunker/ViewModels/MainViewModel.cs`: `_session`, `_dialog`, `_gameActions`, `_gamesSyncLock`
- Private static readonly fields: PascalCase, no underscore: `SaveGate` in `src/SavegameBunker/Services/GameCatalog.cs`, `SnackbarTimeout` in `src/SavegameBunker/Services/DialogService.cs`
- Locals and parameters: `camelCase`. Constants are PascalCase: `DefaultFileName`, `MaxRenameRetries`
- Observable properties use the partial-property form, not field attributes, so setters can stay private:

```csharp
// src/SavegameBunker/ViewModels/GameRowViewModel.cs
[ObservableProperty] public partial string StatusText { get; private set; }
[ObservableProperty] public partial bool IsBusy { get; set; }   // public setter only where a View binding writes it
```

**Types:**
- Classes are `sealed` by default. Static classes and enums are not marked sealed (e.g. `GameCatalog`, `Strings`, `RowSeverity`). Unsealed concrete classes need a reason
- View models derive from `ObservableObject` through the alias `CommunityToolkitObservableObject` when the namespace would collide with the `System.Windows` type (`src/SavegameBunker/ViewModels/GameRowViewModel.cs`)
- Nullable reference types are enabled project-wide (`<Nullable>enable</Nullable>` in `src/SavegameBunker/SavegameBunker.csproj`); use `?` honestly, not as a default

**String resource keys:**
- Every authored UI string lives in `src/SavegameBunker/Themes/Strings.en.xaml` with keys shaped `str_<area>_<name>`, for example `str_common_ready`, `str_toolbar_backUpSelected_access`. Around 200 keys follow this pattern.
- Look them up through `Strings.Get("str_...")` in code, or `{DynamicResource ...}` in XAML. A missing key throws; never hard-code UI text in C#.

## Code Style

**Formatting:**
- No `.editorconfig` and no Prettier/dotnet-format configuration is committed. Follow the existing code.
- Allman braces (opening brace on its own line) for types, methods, and control blocks, as in every file under `src/SavegameBunker/`
- Four-space indentation, `using` directives and namespaces: file-scoped namespaces everywhere (`namespace SavegameBunker.Services;`)
- Collection expressions for empty or literal collections: `[]`, `[ ]`, e.g. `new CatalogLoad(path, new Catalog(), [], Seeded: true, ...)` in `GameCatalog.cs`; `public List<string> InfoMessages { get; } = [];` in tests
- Target-typed `new()` is used for fields and locals where the type is obvious: `private static readonly SemaphoreSlim SaveGate = new(1, 1);`
- Pattern matching and `is not null` are preferred over `== null`

**Linting and analysis:**
- No analyzer ruleset file is committed. Compiler warnings are the only gate.
- XML doc comments are compiled (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`). `CS1591` (missing doc) and `CS1573` are suppressed in `SavegameBunker.csproj`, so a broken `<see cref>` is a build warning but a missing summary is not
- Language version is C# 14 (the partial-property `[ObservableProperty]` form requires it)
- `global.json` pins the .NET 10 SDK (`10.0.400`, `rollForward: latestFeature`)

**Dependencies:**
- The shipping app carries exactly two NuGet packages: WPF-UI 4.3.0 and CommunityToolkit.Mvvm 8.4.2. Do not add a third package (CLAUDE.md, Hard constraints). Use the BCL (`System.Text.Json`, `System.IO.Compression`, `System.Windows.Markup.XamlReader`)

## Import Organization

**Order:**
1. `System.*` namespaces, alphabetical (`using System.IO;`, `using System.Text.Json;`)
2. Third-party (`using CommunityToolkit.Mvvm.ComponentModel;`, `using Wpf.Ui...`)
3. Project namespaces (`using SavegameBunker.Models;`, `using SavegameBunker.Services;`)

**Implicit usings are on** (`<ImplicitUsings>enable</ImplicitUsings>`) for both the app and the test project. Explicit `using System;` / `System.Linq` / `System.Threading.Tasks` are redundant and were removed in commit `6df0955` ("drop usings that implicit usings already cover"). Do not re-add them. Keep explicit usings only for namespaces the implicit set does not cover (`System.IO.Compression`, `System.Text.Json`, `System.Windows.Markup`).

**Path aliases:** None. Some files disambiguate with an alias when a type name collides with a WPF type: `using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;` in `src/SavegameBunker/ViewModels/GameRowViewModel.cs`. Fully qualify `System.IO.Path` when a local variable or parameter named `path` shadows the type, as `GameCatalog.cs` does.

## Error Handling

**Service boundary contract:** services never throw across their boundary. They return `OperationResult` (`src/SavegameBunker/Models/OperationResult.cs`) built with `Ok`, `Fail`, or `Cancel`. Catch broad `Exception` inside the service and convert it to a message that carries context:

```csharp
// src/SavegameBunker/Services/BackupService.cs (pattern, around line 203)
catch (Exception ex)
{
    return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
}
```

**Cleanup:** failed cleanup must never replace the original error. Wrap deletes of temp files in a `TryDelete` helper and swallow only that (`BackupService.cs` around line 126).

**Narrow catches where the outcome is known:** catch a specific type with a `when` filter when one exception means something different from another. Example: `catch (IOException) when (attempt < MaxRenameRetries && File.Exists(candidate))` in `BackupService.cs`.

**Loading never throws:** `GameCatalog.LoadAsync` returns a `CatalogLoad` with `FatalError` set instead (`src/SavegameBunker/Services/GameCatalog.cs`). Absent files are seeded; malformed files are reported and left byte-for-byte untouched. Never overwrite a catalog the user may have edited.

**View-model reporting:** view models call `MainViewModel.Report()`, which writes to `StatusMessage` and the dialog. View models never touch `MessageBox`, folder pickers, or other dialogs directly. They go through `IDialogService` (`src/SavegameBunker/Services/IDialogService.cs`).

**Async command handlers must not throw:** an exception escaping an `AsyncRelayCommand` task surfaces on the UI thread. Report failures as `OperationResult` values instead.

**Argument validation:** guard public entry points with `ArgumentNullException.ThrowIfNull` or equivalent. Tests assert the exception type (`CrashReportTests.cs`: `Assert.Throws<ArgumentNullException>`).

## Logging

**Framework:** none. No logging library is permitted by the hard constraints. The app reports through the status line and dialogs.

**Crash reporting:** `src/SavegameBunker/Services/CrashReport.cs` formats the last-resort report written by the startup try/catch in `src/SavegameBunker/App.xaml.cs`. Do not add a general-purpose logger.

**Patterns:**
- No `Console.*`, `Debug.*`, or `Trace.*` calls exist under `src/SavegameBunker/`.
- User-visible messages come from `Strings` and include the game's display name, for example `"Backup failed for {game.DisplayName}: {ex.Message}"`.

## Comments

**When to comment:**
- Comments explain *why*, not *what*. Most comments in the code cite a design reason or a SPEC behaviour. Example: the note above `SaveGate` in `GameCatalog.cs` explains that it guards in-process saves only.
- Mention SPEC or DECISIONS when a rule is load-bearing: "per SPEC" appears in `Models/OperationResult.cs`.
- Explain non-obvious platform traps in place: `Strings.cs` documents why the dictionary is materialised once (deferred BAML deserialisation is not thread-safe), and `SavegameBunker.csproj` documents why `WithCulture=false` is needed.
- Do not comment obvious code.
- Commit history from the repo uses English conventional prefixes (`docs(planning):`, `style:`). All written files are in English.

**XML doc comments:**
- Required on public types and on public methods whose behaviour is not obvious from the signature. Use `<summary>`, `<remarks>`, and `<param>` in that order. `<param>` entries are used when a parameter has a rule (see `CatalogLoad` in `GameCatalog.cs`).
- Use `<see cref="..."/>` to link to related members. A stale `cref` fails the build.
- Not every member needs a doc comment (CS1591 is suppressed).

## Function Design

**Size:** coordinators and view models are large and split by responsibility rather than kept small. `MainViewModel.cs` delegates to `BulkBackupCoordinator`, `StatusSweepCoordinator`, `RowActionsCoordinator`, `GameActions`, and `CatalogSession`. Put new per-feature orchestration in a coordinator of the same shape rather than growing `MainViewModel`.

**Parameters:**
- Constructor injection only. No DI container. `MainViewModel(CatalogLoad load, IDialogService dialog)` is the composition seam, built in `App.OnStartup`.
- Pass collaborators as parameters and keep them as `private readonly` fields.
- Optional parameters are used for seams and defaults (`SaveAsync(string path, Catalog catalog, DateTime? expectedLastWriteTimeUtc)`).

**Return values:**
- Commands and service operations return `OperationResult` or `Task<OperationResult>`.
- Loads return a record (`CatalogLoad`).
- Use `Task.Run` as the wrapper when the work is synchronous filesystem I/O with no async overload (`GameCatalog.LoadAsync` at line 76). Do not fake `async` signatures.

## Module Design

**Exports:**
- Services are `public static class` or `public sealed class` with no WPF references. They are instantiated directly as fields on `MainViewModel` (`BackupService _backup = new()` pattern).
- Internal helpers are `internal static class` (for example `TestFileSystem` in tests, `KnownFolders` in the app).
- Only expose `internal` to the test assembly through `[InternalsVisibleTo("SavegameBunker.Tests")]` in `SavegameBunker.csproj`.

**Layer direction:** Views -> ViewModels -> Services -> Models. Services never reference WPF. `DialogService` (`src/SavegameBunker/Services/DialogService.cs`) is the only WPF-aware dialog implementation and marshals calls onto the UI thread itself.

**Barrel files:** None. Every type is imported from its own namespace.

## Threading

- Filesystem work runs off the UI thread through `Task.Run` or `AsyncRelayCommand`.
- `AsyncRelayCommand` tracks `IsRunning` and raises `CanExecuteChanged` itself. Bind busy guards to it.
- Shared collections touched from background threads use `BindingOperations.EnableCollectionSynchronization` with a lock (`MainViewModel.cs` around line 109).
- Serialise file writes with a process-wide `SemaphoreSlim(1, 1)` (`GameCatalog.SaveGate`).
- Destructive restore order must not change: safety backup, extract to temp sibling, remove old directory, rename new in (CLAUDE.md, Architecture).

## Destructive Operations and Paths

- Write files with `File.Replace` from a temp sibling in the same directory, never from the system temp folder (`GameCatalog.SaveAsync`, `BackupService.cs`).
- Use forward slashes in catalog paths. Resolve `%DOCUMENTS%` and `%SAVEDGAMES%` through `PathResolver`, never a hard-coded `%USERPROFILE%\Documents`.

---

*Convention analysis: 2026-11-18*
