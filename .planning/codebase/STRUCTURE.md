---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
# Codebase Structure

**Analysis Date:** 2026-11-17

## Directory Layout

```
SavegameBunker/                      # repository root
├── .github/                         # CI: test.yml, release.yml, pages.yml, dependabot.yml
├── docs/                            # DECISIONS.md, screenshot, userguide (MkDocs-style sites)
│   └── userguide/
│       ├── en/                      # English user guide source (zensical.toml, docs/)
│       ├── de/                      # German user guide source
│       ├── overrides/partials/      # Site template override
│       └── index.html               # Built landing page
├── scripts/                         # verify_help_pack_hrefs.py (help-link checker)
├── src/
│   └── SavegameBunker/              # The WPF application (single project)
│       ├── Behaviors/               # Attached behaviours (CornerClip, DialogResultBehavior)
│       ├── Converters/              # IValueConverter implementations
│       ├── Data/                    # known-games.json (embedded resource)
│       ├── Icons/                   # SavegameBunkerIcons.xaml (geometry resources)
│       ├── Models/                  # Plain data types
│       ├── Services/                # Stateless logic, no WPF except DialogService
│       ├── Themes/                  # Styles.xaml, Strings.en.xaml
│       ├── ViewModels/              # MVVM view models and internal collaborators
│       ├── Views/                   # Dialog and secondary window XAML + code-behind
│       ├── App.xaml(.cs)            # Composition root
│       ├── MainWindow.xaml(.cs)     # Primary window
│       ├── games.json               # Default catalog (copied to output)
│       ├── SavegameBunker.csproj
│       └── *.png, app.ico, app.manifest
├── tests/
│   └── SavegameBunker.Tests/        # xUnit suite, one file per unit under test
├── .planning/                       # Local-only GSD workspace (git-ignored)
├── CLAUDE.md, SPEC.md, README.md, CHANGELOG.md, SECURITY.md, LICENSE
├── Directory.Build.props            # Shared MSBuild props (EnableWindowsTargeting)
├── global.json                      # Pins the .NET 10 SDK
├── SavegameBunker.sln
└── .gitattributes, .gitignore
```

## Directory Purposes

**`src/SavegameBunker/Services/`:**
- Purpose: All filesystem, catalog, path, backup and restore logic.
- Contains: `GameCatalog.cs`, `BackupService.cs`, `RestoreService.cs`, `PathResolver.cs`, `SaveStatusScanner.cs`, `KnownGamesScanner.cs`, `KnownGamesCatalog.cs`, `BackupComparer.cs`, `DropFolder.cs`, `DriveHints.cs`, `HelpPath.cs`, `WikiUrl.cs`, `ShellOpen.cs`, `ReparseFile.cs`, `KnownFolders.cs`, `CrashReport.cs`, `IdSlug.cs`, `SizeFormat.cs`, `ElapsedFormat.cs`, `Strings.cs`, plus `IDialogService.cs` and `DialogService.cs`.
- Key files: `src/SavegameBunker/Services/GameCatalog.cs`, `src/SavegameBunker/Services/BackupService.cs`, `src/SavegameBunker/Services/RestoreService.cs`.

**`src/SavegameBunker/ViewModels/`:**
- Purpose: Observable state and commands.
- Contains: `MainViewModel.cs` (hub), `GameRowViewModel.cs`, `GameActions.cs` and `CatalogSession.cs` (internal collaborators), dialog and window view models (`AddGameDialogViewModel.cs`, `ChangeSaveFolderDialogViewModel.cs`, `RestorePickerViewModel.cs`, `ScanWindowViewModel.cs`, `ScanHitRowViewModel.cs`).
- Naming: Dialog and window view models end in `ViewModel` and match their view name (`AddGameDialog.xaml` ↔ `AddGameDialogViewModel.cs`).

**`src/SavegameBunker/Views/`:**
- Purpose: Secondary dialogs and windows.
- Contains: `AddGameDialog`, `ChangeSaveFolderDialog`, `RestorePickerWindow`, `ScanWindow` (each `.xaml` plus `.xaml.cs`).
- Primary window is not here: `src/SavegameBunker/MainWindow.xaml(.cs)` sits at the project root.

**`src/SavegameBunker/Models/`:**
- Purpose: Data carriers and enums, no behaviour beyond small helpers.
- Contains: `Catalog.cs`, `Game.cs`, `KnownGame.cs`, `OperationResult.cs`, `BackupArchive.cs`, `BackupComparison.cs`, `SaveStatus.cs`, `ScanResult.cs`, `RestorePrompt.cs`, `RowSeverity.cs`, `AddGameResult.cs`.

**`src/SavegameBunker/Behaviors/`:**
- Purpose: Attached behaviours for XAML that cannot be expressed in styles alone.
- Contains: `CornerClip.cs`, `DialogResultBehavior.cs`.

**`src/SavegameBunker/Converters/`:**
- Purpose: Value converters used in XAML bindings.
- Contains: `InverseBooleanConverter.cs`, `RelativeTimeConverter.cs`.

**`src/SavegameBunker/Themes/`:**
- Purpose: WPF resource dictionaries.
- Contains: `Styles.xaml` (control styles, theme-aware checkmark tint), `Strings.en.xaml` (all UI text, keyed by `str_...`).

**`src/SavegameBunker/Icons/`:**
- Purpose: Vector geometry for toolbar and row icons, merged from `App.xaml`.
- Contains: `SavegameBunkerIcons.xaml`. Included as `Resource`, not `Page`, in the csproj.

**`src/SavegameBunker/Data/`:**
- Purpose: Static data compiled into the exe.
- Contains: `known-games.json`, embedded via `EmbeddedResource` in the csproj.

**`tests/SavegameBunker.Tests/`:**
- Purpose: xUnit tests.
- Contains: One `*Tests.cs` per unit (for example `BackupServiceTests.cs`, `RestoreServiceTests.cs`, `MainViewModelTests.cs`, `CatalogSessionTests.cs`), plus `TestFileSystem.cs` shared helper and `SavegameBunker.Tests.csproj`.

**`docs/userguide/`:**
- Purpose: Source for the public user guide, published by `.github/workflows/pages.yml`.
- Contains: `en/` and `de/` each with `docs/` markdown and `zensical.toml`.

## Key File Locations

**Entry Points:**
- `src/SavegameBunker/App.xaml.cs`: Composition root and `OnStartup`.
- `src/SavegameBunker/MainWindow.xaml.cs`: Primary window code-behind.

**Configuration:**
- `src/SavegameBunker/games.json`: Default catalog; copied to output on build.
- `src/SavegameBunker/SavegameBunker.csproj`: Targets, packages, embedded resources.
- `Directory.Build.props`: Shared build settings.
- `global.json`: SDK pin.
- `.github/workflows/test.yml`: CI test run on `windows-latest`.

**Core Logic:**
- `src/SavegameBunker/Services/GameCatalog.cs`: Catalog load, validate, save.
- `src/SavegameBunker/Services/BackupService.cs`: Zip backup, verify, list.
- `src/SavegameBunker/Services/RestoreService.cs`: Destructive restore swap.
- `src/SavegameBunker/Services/PathResolver.cs`: Template expansion.
- `src/SavegameBunker/ViewModels/MainViewModel.cs`: Main view model.
- `src/SavegameBunker/ViewModels/GameActions.cs`: Per-game backup and restore flows.
- `src/SavegameBunker/ViewModels/CatalogSession.cs`: Catalog record and writes.

**Testing:**
- `tests/SavegameBunker.Tests/`: All tests.
- `tests/SavegameBunker.Tests/TestFileSystem.cs`: Temporary folder helper.

**Documentation:**
- `SPEC.md`: Public specification.
- `docs/DECISIONS.md`: Design decisions.
- `README.md`, `CHANGELOG.md`, `SECURITY.md`: Public-facing docs at root.

## Naming Conventions

**Files:**
- C# types: PascalCase, one primary type per file, filename equals type name (`BackupService.cs` ↔ `BackupService`).
- Tests: `<UnitUnderTest>Tests.cs` in the flat `tests/SavegameBunker.Tests/` folder.
- XAML views: `PascalCase.xaml` with code-behind `PascalCase.xaml.cs`.
- Data and JSON: kebab-case (`known-games.json`), except the catalog `games.json`.
- Image assets: kebab-case (`empty-state-icon.png`, `titlebar-icon.png`).

**Directories:**
- PascalCase by layer: `Services`, `ViewModels`, `Models`, `Views`, `Themes`, `Converters`, `Behaviors`, `Icons`, `Data`.
- Tests mirror the project name with `.Tests` suffix.

**Types and members:**
- View models: `*ViewModel`. Dialog services: `I*Service` interface with one implementation.
- Resource keys: `str_<area>_<name>` in `Themes/Strings.en.xaml`. `ResourceKeyTests.cs` checks them.
- Sealed by default: `sealed class` unless there is a reason not to.

## Where to Add New Code

**New Feature (touching backup or restore):**
- Logic: `src/SavegameBunker/Services/` for new filesystem behaviour, returning `OperationResult`.
- Orchestration: `src/SavegameBunker/ViewModels/GameActions.cs` for per-game flows, or a new `internal sealed` collaborator alongside it.
- Tests: `tests/SavegameBunker.Tests/<Name>Tests.cs`.

**New Dialog or Window:**
- XAML and code-behind: `src/SavegameBunker/Views/<Name>.xaml(.cs)`.
- View model: `src/SavegameBunker/ViewModels/<Name>ViewModel.cs`.
- Method on the dialog contract: add to `src/SavegameBunker/Services/IDialogService.cs` and implement in `src/SavegameBunker/Services/DialogService.cs`.
- Tests: `tests/SavegameBunker.Tests/<Name>ViewModelTests.cs` (view model logic only; the dialog itself is not tested).

**New Catalog Write:**
- Add a method on `src/SavegameBunker/ViewModels/CatalogSession.cs`. Do not call `GameCatalog.SaveAsync` elsewhere.

**New UI String:**
- Key and English text in `src/SavegameBunker/Themes/Strings.en.xaml`; read via `Strings.Get` or `Strings.Format` in `src/SavegameBunker/Services/Strings.cs`.

**New Model:**
- `src/SavegameBunker/Models/<Name>.cs`, `sealed` where possible, init-only properties for catalog types.

**New Utility or Formatter:**
- Shared helper: `src/SavegameBunker/Services/<Name>.cs` as `public static class`.

**New Converter or Behaviour:**
- `src/SavegameBunker/Converters/` or `src/SavegameBunker/Behaviors/`.

**New Known Game Entry:**
- `src/SavegameBunker/Data/known-games.json` (embedded). Keep the shipped copy in sync with the second `EmbeddedResource` / `Resource` entry described in the csproj comment.

**New Icon:**
- Geometry in `src/SavegameBunker/Icons/SavegameBunkerIcons.xaml`; raster assets at project root, added as `Resource` in the csproj.

## Special Directories

**`.planning/`:**
- Purpose: GSD planning state (ROADMAP, STATE, WISHLIST, codebase maps).
- Generated: Partly (by GSD commands).
- Committed: No. Git-ignored. Nothing in build, tests or docs may depend on it.

**`src/SavegameBunker/bin/`, `obj/`, and `tests/*/bin/`, `obj/`:**
- Purpose: Build outputs.
- Generated: Yes.
- Committed: No.

**`docs/userguide/*/site/` (build output, if present):**
- Generated: Yes.
- Committed: No.

**`.github/`:**
- Purpose: CI and release workflows. `release.yml` calls `test.yml` first, so a tag cannot ship while tests are red.
- Committed: Yes.

---

*Structure analysis: 2026-11-17*
