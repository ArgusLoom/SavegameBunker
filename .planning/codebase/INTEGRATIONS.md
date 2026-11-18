---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# External Integrations

**Analysis Date:** 2026-11-18

## Summary

SavegameBunker is an offline desktop application. It has no backend, makes no HTTP calls from application code, and holds no credentials. Its external touchpoints are the local filesystem (game save folders and backup archives), the Windows shell (folder picker, file and URL open), and two web links opened in the user's browser.

## APIs & External Services

**Web APIs:**
- None. No `HttpClient`, `WebRequest` or socket code exists under `src/SavegameBunker/`.

**Browser links (opened, not fetched):**
- PCGamingWiki search - `https://www.pcgamingwiki.com/w/index.php?search=...`
  - Built by `WikiUrl.Search` in `src/SavegameBunker/Services/WikiUrl.cs`
  - Allowlisted by `WikiUrl.IsAllowed`: https only, host `www.pcgamingwiki.com`; anything else is refused before `Process.Start`
  - Opened via `DialogService` in `src/SavegameBunker/Services/DialogService.cs` (`OpenHttps`, around line 275) with `UseShellExecute = true`
- Project website/guide links from the online help - the Pages site is hosted under GitHub Pages (see Hosting below)

**Local help pack:**
- `help/en/index.html` and `help/de/index.html` beside the executable, opened with the default browser through `HelpPath` (`src/SavegameBunker/Services/HelpPath.cs`) and `DialogService.OpenHelpGuide`

## Data Storage

**Databases:**
- None. No database client, ORM or connection string exists.

**File Storage:**
- Local filesystem only.
  - Game save folders: resolved from catalog `saveDirTemplate` values through `src/SavegameBunker/Services/PathResolver.cs`. Supported tokens: `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`, and pseudo-variables `%DOCUMENTS%` (from `Environment.SpecialFolder.MyDocuments`) and `%SAVEDGAMES%` (from `SHGetKnownFolderPath(FOLDERID_SavedGames)` via `src/SavegameBunker/Services/KnownFolders.cs`)
  - Backup archives: `.zip` files written into the user-chosen `backupRoot` (catalog-level; relative values resolve next to `games.json`). Written and read with `System.IO.Compression.ZipFile` in `src/SavegameBunker/Services/BackupService.cs` and `src/SavegameBunker/Services/RestoreService.cs`
  - Backup naming uses local time: `DateTime.Now.ToString("yyyy-MM-dd_HHmmss")` (`BackupService.cs`, around line 145)
  - Write probe: `File.WriteAllBytes(probePath, [])` in `BackupService.cs` (around line 409) to verify the destination is writable
  - Catalog file: `src/SavegameBunker/games.json`, copied to the output directory and used as the default catalog; read and atomically rewritten through a temp path with `File.WriteAllTextAsync` in `src/SavegameBunker/Services/GameCatalog.cs` (around lines 88, 201-208)
- Crash handling: unhandled exceptions are shown in a dialog through `src/SavegameBunker/Services/CrashReport.cs` and `src/SavegameBunker/App.xaml.cs`. No crash-report upload exists.

**Caching:**
- None.

**Embedded data:**
- `src/SavegameBunker/Data/known-games.json` - known-game table compiled into the assembly as an embedded resource, read by `src/SavegameBunker/Services/KnownGamesCatalog.cs` via `Assembly.GetManifestResourceStream`. Used by the Scan feature (`KnownGamesScanner.cs`, `SaveStatusScanner.cs`).

## Windows Platform Integrations

**Shell and OS:**
- Folder picker: `Microsoft.Win32.OpenFolderDialog` in `src/SavegameBunker/Services/DialogService.cs` (around line 159)
- Open folder or file in Explorer: `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` and `Process.Start("explorer.exe", "/select,...")` in `DialogService.cs` (around lines 222-230). Paths are classified first by `src/SavegameBunker/Services/ShellOpen.cs`, which allows only JSON files and directories
- Open a file in the default editor: `DialogService.OpenInEditor` (around line 259) with `UseShellExecute = true`
- Known folders: `SHGetKnownFolderPath` via `[DllImport("shell32.dll")]` and `CoTaskMemFree` via `[DllImport("ole32.dll")]` in `src/SavegameBunker/Services/KnownFolders.cs`
- Window messaging: `SendMessage` and `RedrawWindow` via `[DllImport("user32.dll")]` in `src/SavegameBunker/App.xaml.cs` (around lines 380-384) for window repaint workarounds
- Drive type hint: `src/SavegameBunker/Services/DriveHints.cs` chooses a starting folder for the picker by volume type; it does not restrict or warn about any destination

**Process execution:**
- No child processes other than the two shell launches above. No registry access (`Microsoft.Win32.Registry` is not used; the app deliberately never reads `InstallLocation` or `UninstallString`, per `CLAUDE.md`).

## Authentication & Identity

- None. The application has no accounts, tokens or login. It runs as `asInvoker` (`src/SavegameBunker/app.manifest`).

## Monitoring & Observability

**Error Tracking:**
- None (no Sentry, AppInsights or similar).

**Logs:**
- No logging library. Errors are reported through `OperationResult` into the status bar and a dialog (`MainViewModel.Report`). No log files are written.

## CI/CD & Deployment

**Source hosting:**
- GitHub repository `ArgusLoom/SavegameBunker` (from `repo_url` in `docs/userguide/en/zensical.toml`)

**CI Pipeline:** GitHub Actions, workflows in `.github/workflows/`:
- `test.yml` - `windows-latest`, .NET SDK from `global.json`, build and xUnit test run; on PRs to `main`
- `release.yml` - on `v*` tags; calls `test.yml`, then publishes a single-file `win-x64` exe, builds the help pack, zips it with a SHA-256 file, creates the GitHub release via `gh` with `GITHUB_TOKEN`, and attests build provenance with `actions/attest-build-provenance`
- `pages.yml` - builds the Zensical user guide (en, de) and deploys to GitHub Pages; called by `release.yml` after the release exists, or run on demand

**Third-party actions used (all SHA-pinned):**
- `actions/checkout`, `actions/setup-dotnet`, `actions/cache`, `actions/upload-artifact`, `actions/download-artifact`, `actions/attest-build-provenance`, `actions/upload-pages-artifact`, `actions/deploy-pages`

**Dependency updates:**
- `.github/dependabot.yml` - monthly grouped updates for the `nuget` and `github-actions` ecosystems

**Hosting:**
- Binary distribution: GitHub Releases (`SavegameBunker-v{VERSION}-win-x64.zip` plus `.sha256`)
- User guide: GitHub Pages under the `/SavegameBunker/` project path; links are relative (`use_directory_urls = false`) so the same site works there and from the offline help pack

**Package sources:**
- NuGet.org for `WPF-UI`, `CommunityToolkit.Mvvm` and the test packages
- PyPI for `zensical==0.1.1` (CI only)

## Environment Configuration

**Required environment variables:**
- None for running the application.
- CI uses `GITHUB_REF_NAME`, `GITHUB_TOKEN` (`github.token`), `GITHUB_STEP_SUMMARY`, `GITHUB_OUTPUT` and `GITHUB_ENV`, all provided by GitHub Actions.

**Secrets location:**
- No application secrets. The release job authenticates with the built-in `GITHUB_TOKEN` (`contents: write`); the attest job uses OIDC (`id-token: write`). No repository secrets are referenced in the workflows.
- No `.env` or credential files are tracked. If one appears locally, it is not read or quoted in this document.

## Webhooks & Callbacks

**Incoming:**
- None.

**Outgoing:**
- None.

---

*Integration audit: 2026-11-18*
