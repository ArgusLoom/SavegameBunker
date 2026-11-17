---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
# Technology Stack

**Analysis Date:** 2026-11-17

## Languages

**Primary:**
- C# (language version implied by .NET 10 SDK, C# 14 default) - all application code in `src/SavegameBunker/` and tests in `tests/SavegameBunker.Tests/`
- XAML - all views and theme/resource dictionaries: `src/SavegameBunker/MainWindow.xaml`, `src/SavegameBunker/App.xaml`, `src/SavegameBunker/Themes/Styles.xaml`, `src/SavegameBunker/Themes/Strings.en.xaml`, `src/SavegameBunker/Icons/SavegameBunkerIcons.xaml`, `src/SavegameBunker/Views/*.xaml`

**Secondary:**
- JSON - the user-editable catalog `src/SavegameBunker/games.json` (comments and trailing commas allowed, read with `System.Text.Json`) and the embedded table `src/SavegameBunker/Data/known-games.json`
- Markdown + TOML - user guide sources in `docs/userguide/{en,de}/` built by Zensical (`docs/userguide/{en,de}/zensical.toml`)
- Python - two CI helper scripts only: `scripts/verify_help_pack_hrefs.py` (run in `.github/workflows/release.yml`) and the `zensical` pip install step in `.github/workflows/pages.yml` and `release.yml`
- PowerShell / Git Bash commands are used for local development only (see CLAUDE.md); no shell scripts are tracked in the repo

## Runtime

**Environment:**
- .NET 10 (LTS) on Windows only - target framework `net10.0-windows` in `src/SavegameBunker/SavegameBunker.csproj` and `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`
- WPF desktop application (`<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`)
- Windows 10/11 declared as the supported OS in `src/SavegameBunker/app.manifest` (`supportedOS` GUID for Windows 10/11); `requestedExecutionLevel` is `asInvoker`
- SDK pinned in `global.json`: `10.0.400`, `rollForward: latestFeature`

**Package Manager:**
- NuGet via the .NET SDK (`dotnet restore` implicit)
- Lockfile: Not detected (no `packages.lock.json` tracked; `RestorePackagesWithLockFile` is not set)
- Dependabot configured for NuGet and GitHub Actions, monthly, grouped: `.github/dependabot.yml`

## Frameworks

**Core:**
- WPF (`Microsoft.NET.Sdk` with `UseWPF`) - UI shell, data binding, resource dictionaries
- WPF-UI 4.3.0 - Fluent-styled controls, `ui:TitleBar`, content-dialog and snackbar services, theme dictionaries (`src/SavegameBunker/App.xaml`, `src/SavegameBunker/Services/DialogService.cs`)
- CommunityToolkit.Mvvm 8.4.2 - `ObservableObject`, `RelayCommand`, `AsyncRelayCommand` for the hand-rolled MVVM layer in `src/SavegameBunker/ViewModels/`
- Hand-rolled MVVM and service layering - no DI container (see CLAUDE.md hard constraints)

**Testing:**
- xUnit 2.9.3 - test framework, `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`
- xunit.runner.visualstudio 4.0.1 - VS/`dotnet test` adapter
- Microsoft.NET.Test.Sdk 18.12.0 - test host
- Test project references the app project directly and has `InternalsVisibleTo` access (`src/SavegameBunker/SavegameBunker.csproj`)

**Build/Dev:**
- `dotnet` SDK 10.0.400 - build, test, run, publish
- `Directory.Build.props` (repo root) - sets `EnableWindowsTargeting=true` so `dotnet build` also succeeds on Linux/macOS (compile-only; run and test need Windows)
- Single-file publish configuration conditioned on `PublishSingleFile=true` in `src/SavegameBunker/SavegameBunker.csproj`: self-contained, `RuntimeIdentifier=win-x64`, `IncludeNativeLibrariesForSelfExtract=true`; trimming intentionally off
- Zensical 0.1.1 (pip) - static site generator for the user guide, used by `.github/workflows/pages.yml` and `.github/workflows/release.yml`
- `global.json` - SDK pin consumed by `actions/setup-dotnet` in CI

## Key Dependencies

**Critical (the only two shipping NuGet packages):**
- `WPF-UI` 4.3.0 - entire Fluent UI surface, title bar, dialogs, snackbars; `Wpf.Ui.dll` is copied to output
- `CommunityToolkit.Mvvm` 8.4.2 - observable properties and async relay commands used across `src/SavegameBunker/ViewModels/`

**Test-only:**
- `xunit` 2.9.3, `xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk` 18.12.0

**Platform / BCL APIs relied on (no package):**
- `System.IO.Compression` (`ZipFile.OpenRead`, `ZipFile.Open`) - backup archives in `src/SavegameBunker/Services/BackupService.cs` and `src/SavegameBunker/Services/RestoreService.cs`
- `System.Text.Json` - catalog and known-games parsing in `src/SavegameBunker/Services/GameCatalog.cs` and `src/SavegameBunker/Services/KnownGamesCatalog.cs`
- `System.Diagnostics.Process` with `UseShellExecute = true` - opening folders, the catalog file, the help pack and HTTPS URLs in `src/SavegameBunker/Services/DialogService.cs` (guarded by `src/SavegameBunker/Services/ShellOpen.cs` and `src/SavegameBunker/Services/WikiUrl.cs`)
- `System.Windows.XamlReader` - loads `Themes/Strings.en.xaml` in tests (`src/SavegameBunker/Services/Strings.cs`)

**Native interop (P/Invoke, no package):**
- `shell32.dll` `SHGetKnownFolderPath` and `ole32.dll` `CoTaskMemFree` - resolves the Saved Games known folder in `src/SavegameBunker/Services/KnownFolders.cs` (classic `[DllImport]`, deliberately not `[LibraryImport]`)
- `user32.dll` `SendMessage` and `RedrawWindow` - repaint workaround in `src/SavegameBunker/App.xaml.cs`
- Windows Shell and `explorer.exe /select` launched via `Process.Start` in `src/SavegameBunker/Services/DialogService.cs`

**Infrastructure:**
- `actions/checkout`, `actions/setup-dotnet` (v6.1.0), `actions/upload-pages-artifact`, `actions/deploy-pages`, `gh` CLI - SHA-pinned GitHub Actions in `.github/workflows/*.yml`

## Configuration

**Environment:**
- No environment variables are read by the application at configuration time; the app's only input is the catalog file
- Catalog path: first command-line argument, else `games.json` beside the executable (`src/SavegameBunker/Services/GameCatalog.cs`, `ResolvePath` and `DefaultPath`)
- `backupRoot` (optional, catalog-level) is set inside the catalog; a relative value resolves next to the catalog file
- Path placeholders expanded at runtime by `src/SavegameBunker/Services/PathResolver.cs`: `%SAVEDGAMES%`, `%DOCUMENTS%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`
- No `settings.json`, no `appsettings.json`, no user-secrets; the catalog file is the whole configuration
- Environment variable reads that do occur are standard Windows folder lookups via `Environment.GetFolderPath` / `Environment.SpecialFolder` in `src/SavegameBunker/Services/PathResolver.cs`, `src/SavegameBunker/Services/DriveHints.cs`, `src/SavegameBunker/Services/DialogService.cs`
- Dev-only crash self-test switches exist under `#if DEBUG` in `src/SavegameBunker/App.xaml.cs`

**Build:**
- `src/SavegameBunker/SavegameBunker.csproj` - app project; `Nullable` and `ImplicitUsings` enabled; `games.json` copied with `PreserveNewest`; `Version` 1.0.0 (must match the release tag per `.github/workflows/release.yml`)
- `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj` - test project
- `SavegameBunker.sln` - solution at repo root
- `Directory.Build.props` - repo-wide Windows targeting switch
- `global.json` - SDK pin
- `src/SavegameBunker/app.manifest` - Windows application manifest (asInvoker, Win10/11 compatibility)
- `src/SavegameBunker/app.ico`, `titlebar-icon.png`, `empty-state-icon.png` - packaged resources
- `docs/userguide/{en,de}/zensical.toml` - user guide build config

## Platform Requirements

**Development:**
- Windows 10 or 11 for `dotnet run`, `dotnet test`, and WPF runtime
- .NET 10 SDK matching `global.json` (10.0.400, latestFeature roll-forward)
- Linux/macOS: `dotnet build` only, thanks to `EnableWindowsTargeting`
- Python with `zensical==0.1.1` only if building the user guide locally

**Production:**
- Distributed as a GitHub Release asset: `SavegameBunker-v<version>-win-x64.zip` containing a single self-contained `SavegameBunker.exe` (no .NET install needed), plus a `.sha256` file. Built by `.github/workflows/release.yml` on `ubuntu-latest` with `dotnet publish -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`
- Offline help pack bundled inside the zip under `help/{en,de}/`
- Online user guide published to GitHub Pages via `.github/workflows/pages.yml`

---

*Stack analysis: 2026-11-17*
