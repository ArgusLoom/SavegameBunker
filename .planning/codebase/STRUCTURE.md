---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# Codebase Structure

**Analysis Date:** 2026-11-18

## Directory Layout

```
SavegameBunker/                       # repository root
├── .github/workflows/                # test.yml (CI), release.yml, pages.yml
├── docs/                             # DECISIONS.md, screenshot.png, userguide/ (en + de site sources)
├── scripts/                          # verify_help_pack_hrefs.py (help-pack link check)
├── src/SavegameBunker/               # the WPF application (single project)
│   ├── Behaviors/                    # attached WPF behaviors
│   ├── Converters/                   # IValueConverter implementations
│   ├── Data/                         # known-games.json (embedded resource)
│   ├── Icons/                        # SavegameBunkerIcons.xaml (merged geometries)
│   ├── Models/                       # records and data classes
│   ├── Services/                     # filesystem, catalog, archive, path logic
│   ├── Themes/                       # Styles.xaml, Strings.en.xaml
│   ├── ViewModels/                   # MainViewModel, coordinators, dialog VMs
│   └── Views/                        # secondary windows and dialogs (XAML + code-behind)
├── tests/SavegameBunker.Tests/       # xUnit suite (one *Tests.cs per production type)
├── CHANGELOG.md, README.md, SPEC.md  # public documentation
├── CLAUDE.md                         # agent guidance
├── Directory.Build.props             # EnableWindowsTargeting for cross-platform compile
├── global.json                       # pins the .NET 10 SDK
└── SavegameBunker.sln                # solution: app + tests
```

Not tracked, but part of the working tree: `.planning/` (GSD planning state, tracked per CLAUDE.md, with `.planning/codebase/` holding these analysis documents).

## Directory Purposes

**`src/SavegameBunker/`:**
- Purpose: The one shipping project. Output is `SavegameBunker.exe` (WinExe, `net10.0-windows`, WPF).
- Contains: `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `SavegameBunker.csproj`, `games.json`, `app.manifest`, `app.ico`, PNG resources.
- Key files: `src/SavegameBunker/App.xaml.cs`, `src/SavegameBunker/MainWindow.xaml.cs`, `src/SavegameBunker/games.json`, `src/SavegameBunker/SavegameBunker.csproj`

**`src/SavegameBunker/Models/`:**
- Purpose: Plain data types with no behavior beyond validation of their own fields.
- Contains: `Catalog.cs`, `Game.cs`, `KnownGame.cs`, `OperationResult.cs`, `BackupArchive.cs`, `BackupComparison.cs`, `SaveStatus.cs`, `ScanResult.cs`, `RestorePrompt.cs`, `AddGameResult.cs`, `RowSeverity.cs`
- Key files: `src/SavegameBunker/Models/Catalog.cs`, `src/SavegameBunker/Models/OperationResult.cs`

**`src/SavegameBunker/Services/`:**
- Purpose: All side-effecting and pure logic. Stateless static classes or `sealed` classes with no state.
- Contains: Catalog I/O (`GameCatalog.cs`), backup and restore (`BackupService.cs`, `RestoreService.cs`), path templates (`PathResolver.cs`), save status (`SaveStatusScanner.cs`, `BackupComparer.cs`), known-games lookup (`KnownGamesCatalog.cs`, `KnownGamesScanner.cs`), dialogs (`IDialogService.cs`, `DialogService.cs`), shell and help links (`ShellOpen.cs`, `HelpPath.cs`, `WikiUrl.cs`), and helpers.
- Key files: `src/SavegameBunker/Services/GameCatalog.cs`, `src/SavegameBunker/Services/BackupService.cs`, `src/SavegameBunker/Services/RestoreService.cs`, `src/SavegameBunker/Services/PathResolver.cs`, `src/SavegameBunker/Services/DialogService.cs`
- Note: `DialogService.cs` is the only WPF-aware file in this folder.

**`src/SavegameBunker/ViewModels/`:**
- Purpose: Bound state, commands and orchestration for each window or flow.
- Contains: Root VM and coordinators (see Key File Locations), row VMs, and dialog VMs.
- Key files: `src/SavegameBunker/ViewModels/MainViewModel.cs`, `src/SavegameBunker/ViewModels/CatalogSession.cs`, `src/SavegameBunker/ViewModels/GameActions.cs`

**`src/SavegameBunker/Views/`:**
- Purpose: Secondary windows and dialogs. Each has a XAML file and a code-behind file.
- Contains: `AddGameDialog`, `ChangeSaveFolderDialog`, `RestorePickerWindow`, `ScanWindow`.
- Key files: `src/SavegameBunker/Views/RestorePickerWindow.xaml`, `src/SavegameBunker/Views/ScanWindow.xaml`

**`src/SavegameBunker/Behaviors/`:**
- Purpose: Attached behaviors used from XAML.
- Contains: `CornerClip.cs`, `DialogResultBehavior.cs`

**`src/SavegameBunker/Converters/`:**
- Purpose: Value converters for XAML bindings.
- Contains: `InverseBooleanConverter.cs`, `RelativeTimeConverter.cs`

**`src/SavegameBunker/Themes/`:**
- Purpose: Application-level resource dictionaries.
- Contains: `Styles.xaml` (control styles), `Strings.en.xaml` (all authored UI text, keyed; also embedded with `WithCulture=false` for tests)

**`src/SavegameBunker/Icons/`:**
- Purpose: Geometry resources for toolbar and row icons, merged from `App.xaml`.
- Contains: `SavegameBunkerIcons.xaml`

**`src/SavegameBunker/Data/`:**
- Purpose: Embedded data shipped inside the executable.
- Contains: `known-games.json` (embedded resource, read by `KnownGamesCatalog`)

**`tests/SavegameBunker.Tests/`:**
- Purpose: xUnit tests. Names mirror production types (for example `BackupServiceTests.cs` tests `Services/BackupService.cs`, `MainViewModelTests.cs` tests `ViewModels/MainViewModel.cs`).
- Contains: About 37 test classes plus `TestFileSystem.cs` (shared helper) and `SavegameBunker.Tests.csproj`.
- Key files: `tests/SavegameBunker.Tests/TestFileSystem.cs`, `tests/SavegameBunker.Tests/ServiceContractTests.cs`, `tests/SavegameBunker.Tests/ResourceKeyTests.cs`

**`docs/`:**
- Purpose: Design and user documentation.
- Contains: `DECISIONS.md` (design decision record), `userguide/` (Zensical site sources in `en/` and `de/`, with `docs/*.md` pages), `screenshot.png`.

**`.github/workflows/`:**
- Purpose: CI and release automation.
- Contains: `test.yml` (runs the suite on `windows-latest` for PRs to `main`), `release.yml` (calls the test workflow before publishing), `pages.yml` (user guide site).

## Key File Locations

**Entry Points:**
- `src/SavegameBunker/App.xaml.cs`: composition root and `OnStartup`
- `src/SavegameBunker/MainWindow.xaml.cs`: main window code-behind

**Configuration:**
- `src/SavegameBunker/games.json`: the catalog (copied to output with `PreserveNewest`), the only configuration file
- `src/SavegameBunker/SavegameBunker.csproj`: target framework, the two package references, embedded resources
- `Directory.Build.props`: `EnableWindowsTargeting`
- `global.json`: .NET SDK pin

**Core Logic:**
- `src/SavegameBunker/Services/GameCatalog.cs`: catalog load, seed, validate, save
- `src/SavegameBunker/Services/BackupService.cs`: archive write, verify, list, delete
- `src/SavegameBunker/Services/RestoreService.cs`: the destructive restore sequence
- `src/SavegameBunker/Services/PathResolver.cs`: template expansion
- `src/SavegameBunker/ViewModels/MainViewModel.cs`: root view model
- `src/SavegameBunker/ViewModels/CatalogSession.cs`: sole catalog writer

**Testing:**
- `tests/SavegameBunker.Tests/`: all tests, flat (no subfolders)
- `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`: test project

**Specs and Decisions:**
- `SPEC.md`: public behavior specification
- `docs/DECISIONS.md`: design decisions
- `CHANGELOG.md`: shipped changes

## Naming Conventions

**Files:**
- Source files are PascalCase and named after their primary type: `BackupService.cs` contains `BackupService`.
- Interfaces carry an `I` prefix: `IDialogService.cs`.
- Dialog view models end in `DialogViewModel`: `AddGameDialogViewModel.cs`, `ChangeSaveFolderDialogViewModel.cs`.
- Dialog and window views pair `Name.xaml` with `Name.xaml.cs`: `ScanWindow.xaml` and `ScanWindow.xaml.cs`.
- Coordinators end in `Coordinator` (`BulkBackupCoordinator`, `StatusSweepCoordinator`, `RowActionsCoordinator`) or are named for their role (`CatalogSession`, `GameActions`).
- Tests end in `Tests.cs` and mirror the type name: `RestoreServiceTests.cs`.
- Data and config files are kebab-case: `known-games.json`. The catalog file is lowercase `games.json`.
- Shipped resource images are kebab-case: `titlebar-icon.png`, `empty-state-icon.png`.

**Directories:**
- PascalCase folders by role under the project: `Models`, `Services`, `ViewModels`, `Views`, `Converters`, `Behaviors`, `Themes`, `Icons`, `Data`.
- Lowercase for documentation sites under `docs/`.

**Types and members:**
- `sealed` classes by default; `internal` for types held only by `MainViewModel` (for example `CatalogSession`, `GameActions`); `public` for services that tests and the UI reach.
- File-scoped namespaces everywhere (`namespace SavegameBunker.Services;`).
- Namespaces mirror folders: `SavegameBunker.Services`, `SavegameBunker.ViewModels`, `SavegameBunker.Models`.
- Authored strings are keyed `str_<area>_<name>` (for example `str_common_ready`, `str_crash_source_action`) in `Themes/Strings.en.xaml`.

## Where to Add New Code

**New user-facing feature (command and state):**
- Bound state and command: `src/SavegameBunker/ViewModels/MainViewModel.cs`. If the flow has more than a few steps, add a coordinator under `src/SavegameBunker/ViewModels/` following `BulkBackupCoordinator.cs`, and hold it as a `private readonly` field.
- Filesystem or catalog logic: `src/SavegameBunker/Services/`, returning `OperationResult`.
- New dialog: add a method to `src/SavegameBunker/Services/IDialogService.cs`, implement it in `src/SavegameBunker/Services/DialogService.cs`, and place the XAML and code-behind in `src/SavegameBunker/Views/`.
- Tests: `tests/SavegameBunker.Tests/<TypeName>Tests.cs`.

**New service:**
- Implementation: `src/SavegameBunker/Services/<Name>.cs` (`public static class` for pure helpers, `public sealed class` for stateful-per-call services that take collaborators in their constructor).
- Tests: `tests/SavegameBunker.Tests/<Name>Tests.cs`. Use `tests/SavegameBunker.Tests/TestFileSystem.cs` for temporary directories.

**New model:**
- Implementation: `src/SavegameBunker/Models/<Name>.cs`, as `sealed record` where it is a value.

**New view model for a dialog or window:**
- Implementation: `src/SavegameBunker/ViewModels/<Name>DialogViewModel.cs` (or `<Name>ViewModel.cs`).
- View: `src/SavegameBunker/Views/<Name>.xaml` and `<Name>.xaml.cs`.

**New value converter or behavior:**
- Converter: `src/SavegameBunker/Converters/<Name>Converter.cs`
- Behavior: `src/SavegameBunker/Behaviors/<Name>.cs`

**New authored text:**
- Add the key to `src/SavegameBunker/Themes/Strings.en.xaml`. `tests/SavegameBunker.Tests/ResourceKeyTests.cs` and `StringsTests.cs` check it.

**New known game:**
- Add to `src/SavegameBunker/Data/known-games.json`.

**Shared helpers:**
- Small pure helpers: `src/SavegameBunker/Services/` (for example `SizeFormat.cs`, `IdSlug.cs`).

## Special Directories

**`bin/`, `obj/`:**
- Purpose: Build output and intermediate files.
- Generated: Yes
- Committed: No (ignored by `.gitignore`)

**`.planning/`:**
- Purpose: GSD planning state: `ROADMAP.md`, `STATE.md`, `WISHLIST.md`, `phases/`, `codebase/`.
- Generated: Partly (by GSD commands)
- Committed: Yes (tracked per CLAUDE.md). Code and shipped docs must not depend on it.

**`docs/userguide/*/.gitignore`:**
- Purpose: Ignores the documentation site build output for each language.
- Generated: Yes (site build)
- Committed: The `.gitignore` files are committed; the build output is not.

---

*Structure analysis: 2026-11-18*
