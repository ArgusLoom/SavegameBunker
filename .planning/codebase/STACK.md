---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# Technology Stack

**Analysis Date:** 2026-11-18

## Languages

**Primary:**
- C# (latest language version for .NET 10, `Nullable` and `ImplicitUsings` enabled) - all application, service, view-model and test code under `src/SavegameBunker/` and `tests/SavegameBunker.Tests/`
- XAML - WPF views and resource dictionaries, e.g. `src/SavegameBunker/MainWindow.xaml`, `src/SavegameBunker/App.xaml`, `src/SavegameBunker/Themes/Styles.xaml`, `src/SavegameBunker/Icons/SavegameBunkerIcons.xaml`

**Secondary:**
- JSON - the user catalog `src/SavegameBunker/games.json` (comments and trailing commas allowed, read by `System.Text.Json`) and the embedded known-games table `src/SavegameBunker/Data/known-games.json`
- Python 3 - only the documentation toolchain and the help-pack check script `scripts/verify_help_pack_hrefs.py`, run in CI
- PowerShell / Bash - developer commands in `CLAUDE.md`; no custom build scripts in the repo
- Markdown and TOML - user guide sources under `docs/userguide/{en,de}/` configured by `docs/userguide/{en,de}/zensical.toml`

## Runtime

**Environment:**
- .NET 10 (LTS), Windows desktop target `net10.0-windows` (`src/SavegameBunker/SavegameBunker.csproj`, `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`)
- Pinned SDK: `10.0.400` with `rollForward: latestFeature` in `global.json`
- Windows 10/11 only: `app.manifest` declares `supportedOS` for Windows 10/11 and `requestedExecutionLevel level="asInvoker"`
- Application type: `WinExe` (WPF), `UseWPF=true`
- Version string: `<Version>1.0.1</Version>` in `src/SavegameBunker/SavegameBunker.csproj`; the CI release job requires this to match the git tag

**Package Manager:**
- NuGet via `dotnet` CLI (SDK-style projects, no `Directory.Packages.props` central management detected)
- Solution: `SavegameBunker.sln`
- Lockfile: Not detected (no `packages.lock.json` tracked)
- Build-wide setting: `Directory.Build.props` sets `EnableWindowsTargeting=true`, so `dotnet build` also succeeds on Linux/macOS for compile checks; `dotnet test` and `dotnet run` require Windows

## Frameworks

**Core:**
- WPF (`UseWPF`) on .NET 10 - UI shell, views in `src/SavegameBunker/Views/`, window in `src/SavegameBunker/MainWindow.xaml`
- WPF-UI 4.3.0 (`PackageReference`, namespace `ui` = `http://schemas.lepo.co/wpfui/2022/xaml`) - Fluent-styled controls, `ui:TitleBar`, content dialogs and snackbars; theme dictionaries in `src/SavegameBunker/Themes/Styles.xaml`
- Hand-rolled MVVM with CommunityToolkit.Mvvm 8.4.2 (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`) - view models under `src/SavegameBunker/ViewModels/`
- No DI container, no logging framework, no serialization library beyond the BCL (enforced by `CLAUDE.md`)

**Testing:**
- xUnit 2.9.3 - test framework, `tests/SavegameBunker.Tests/`
- `xunit.runner.visualstudio` 4.0.1 - test adapter
- `Microsoft.NET.Test.Sdk` 18.12.0 - test host
- Global `using Xunit;` via `<Using Include="Xunit" />` in the test csproj
- `InternalsVisibleTo` grants `SavegameBunker.Tests` access to internals (`src/SavegameBunker/SavegameBunker.csproj`)

**Build/Dev:**
- .NET SDK 10.0.400 - build, test, publish
- Zensical 0.1.1 (Python package, `pip install zensical==0.1.1`) - static site generator for the user guide, run in `.github/workflows/release.yml` and `.github/workflows/pages.yml`
- GitHub Actions - CI and release, see `.github/workflows/`

## Key Dependencies

**Critical (shipped in the executable):**
- `WPF-UI` 4.3.0 - the only UI component library; styles, dialogs, title bar
- `CommunityToolkit.Mvvm` 8.4.2 - observable properties, relay commands, messenger-free MVVM plumbing

These two are the only shipping `PackageReference`s. `CLAUDE.md` makes this a hard constraint; adding a third shipping package requires revising the constraint first.

**Base Class Library usage worth knowing:**
- `System.Text.Json` - catalog read/write (`src/SavegameBunker/Services/GameCatalog.cs`, `src/SavegameBunker/Services/KnownGamesCatalog.cs`), with `JsonCommentHandling` and trailing-comma tolerance on read
- `System.IO.Compression` (`ZipFile`) - backup archives (`src/SavegameBunker/Services/BackupService.cs`, `ZipFile.OpenRead` at line ~522 and `ZipFile.Open` in create mode at line ~662); the backup walk is custom rather than `ZipFile.CreateFromDirectory`, to avoid following directory junctions
- `System.Diagnostics.Process` - opens folders, files and URLs via shell execute (`src/SavegameBunker/Services/DialogService.cs`)
- `Microsoft.Win32.OpenFolderDialog` - native folder picker (`src/SavegameBunker/Services/DialogService.cs`, line ~159)
- `System.Windows.Markup.XamlReader` - loads the embedded string table `src/SavegameBunker/Themes/Strings.en.xaml` (`src/SavegameBunker/Services/Strings.cs`)

**Platform interop (no NuGet, native DLLs):**
- `shell32.dll` `SHGetKnownFolderPath` for `FOLDERID_SavedGames` and `ole32.dll` `CoTaskMemFree` - `src/SavegameBunker/Services/KnownFolders.cs`, classic `[DllImport]` on purpose
- `user32.dll` `SendMessage` and `RedrawWindow` - `src/SavegameBunker/App.xaml.cs` (around lines 380-384)

**Infrastructure:**
- Embedded resources: `src/SavegameBunker/Data/known-games.json`, `src/SavegameBunker/Themes/Strings.en.xaml` (`WithCulture=false`), `titlebar-icon.png`, `empty-state-icon.png`, `Icons/SavegameBunkerIcons.xaml`
- Application icon `src/SavegameBunker/app.ico` (also embedded as Win32 resource)

## Configuration

**Environment:**
- No environment variables are read by the application. `PathResolver` (`src/SavegameBunker/Services/PathResolver.cs`) expands `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%` from the process environment inside catalog templates, plus the pseudo-variables `%DOCUMENTS%` and `%SAVEDGAMES%`
- No `.env` files are present in the repository (only the forbidden-file policy applies if one appears)
- Catalog path: first command-line argument, otherwise `games.json` beside the executable (`GameCatalog.ResolvePath`, called from `src/SavegameBunker/App.xaml.cs`)
- No `settings.json`: `backupRoot` lives inside the catalog (`src/SavegameBunker/Models/Catalog.cs`); relative values resolve next to the catalog file

**Build:**
- `global.json` - SDK pin (10.0.400, latestFeature roll-forward)
- `Directory.Build.props` - `EnableWindowsTargeting`
- `src/SavegameBunker/SavegameBunker.csproj` - `GenerateDocumentationFile=true` with `NoWarn` for CS1591/CS1573; `PublishSingleFile` branch sets `SelfContained`, `RuntimeIdentifier=win-x64`, `IncludeNativeLibrariesForSelfExtract`; trimming intentionally off
- `src/SavegameBunker/app.manifest` - asInvoker, Windows 10/11 compatibility
- `.gitattributes`, `.gitignore`, `.github/dependabot.yml` (monthly NuGet and GitHub Actions updates, grouped)
- `docs/userguide/{en,de}/zensical.toml` - guide site config (`use_directory_urls = false` for `file://` help pack)

## Platform Requirements

**Development:**
- Windows 10/11 with the .NET 10 SDK (`global.json` pins 10.0.400)
- Full test run requires Windows (`dotnet test` against WPF targets); `dotnet build` also works on Linux/macOS through `EnableWindowsTargeting`
- Optional: Python 3 with `zensical==0.1.1` to build the user guide locally (`docs/userguide/{en,de}`)

**Production:**
- Distributed as a self-contained single-file `win-x64` executable, zipped as `SavegameBunker-v{VERSION}-win-x64.zip` with a `.sha256` file, produced by the `release` job in `.github/workflows/release.yml`
- The zip ships `help/en/` and `help/de/` (generated user guide) beside the executable
- No .NET install needed on the target machine (self-contained publish)
- Runs without elevation (`asInvoker`)

## CI

- `.github/workflows/test.yml` - `windows-latest`, `dotnet build SavegameBunker.sln -c Release`, `dotnet test ... --no-build`; runs on PRs to `main` and via `workflow_call`
- `.github/workflows/release.yml` - triggered by `v*` tags, runs tests first, then `ubuntu-latest` for the Windows publish (uses `-p:EnableWindowsTargeting=true`), the Zensical help build, `scripts/verify_help_pack_hrefs.py`, zip + SHA-256, GitHub release creation, and build provenance attestation
- `.github/workflows/pages.yml` - builds the English and German guide and deploys to GitHub Pages; called from `release.yml`

All third-party actions are pinned to full commit SHAs with version comments.

---

*Stack analysis: 2026-11-18*
