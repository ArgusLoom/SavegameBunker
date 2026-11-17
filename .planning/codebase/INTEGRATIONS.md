---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
# External Integrations

**Analysis Date:** 2026-11-17

## APIs & External Services

**Network APIs:**
- None called by the application. There is no `HttpClient`, `WebRequest`, or socket code under `src/SavegameBunker/`. The app is offline by design.
- PCGamingWiki - referenced only as a link target. `src/SavegameBunker/Services/WikiUrl.cs` (`HostPrefix = "https://www.pcgamingwiki.com/"`) accepts only `https://www.pcgamingwiki.com/` URLs, which are opened in the user's default browser via `src/SavegameBunker/Services/DialogService.cs` (`OpenHttps`). The data in `src/SavegameBunker/Data/known-games.json` was hand-written from documentation and not fetched at runtime.
- Project home / user guide - opened in the browser through `DialogService.OpenHttps` and `OpenHelpGuide` (`src/SavegameBunker/Services/HelpPath.cs`, `src/SavegameBunker/Services/IDialogService.cs`). The repository is referenced as `https://github.com/ArgusLoom/SavegameBunker` in `docs/userguide/en/zensical.toml`.

**Shell integration (local, not network):**
- `Process.Start` with `UseShellExecute = true` opens folders, the catalog JSON, the offline help `index.html`, and HTTPS URLs - `src/SavegameBunker/Services/DialogService.cs`
- `explorer.exe /select,"<path>"` reveals a file - `src/SavegameBunker/Services/DialogService.cs`
- Opening anything that is not a directory or `.json` file is refused by `src/SavegameBunker/Services/ShellOpen.cs`

## Data Storage

**Databases:**
- None. No ORM, no SQL client, no connection strings.

**File Storage:**
- Local filesystem only. The application reads and writes:
  - The catalog file (`games.json` or the path given on the command line) - `src/SavegameBunker/Services/GameCatalog.cs`. Written with optimistic concurrency via last-write time, guarded by a process-wide `SemaphoreSlim`.
  - Backup archives (`.zip`) under the user-chosen `backupRoot` - written by `src/SavegameBunker/Services/BackupService.cs` using `System.IO.Compression.ZipFile`.
  - Save directories under the game's resolved `saveDirTemplate` - read for backup, replaced during restore by `src/SavegameBunker/Services/RestoreService.cs`.
  - Crash reports - formatted by `src/SavegameBunker/Services/CrashReport.cs`; the tests and app do not transmit them anywhere.
- Backup destinations are any folder the user selects, including removable or network volumes. Volume type only picks a starting folder (`src/SavegameBunker/Services/DriveHints.cs`); no rule depends on it.

**Caching:**
- None

## Authentication & Identity

**Auth Provider:**
- Not applicable. No user accounts, no tokens, no login flow.
- Windows file-system permissions are the only access control. The manifest requests `asInvoker` (`src/SavegameBunker/app.manifest`).

## Monitoring & Observability

**Error Tracking:**
- None. No Sentry, AppInsights, or similar SDK is referenced.

**Logs:**
- No logging framework. Errors are reported to the user through `OperationResult` and dialogs (`src/SavegameBunker/Models/OperationResult.cs`, `src/SavegameBunker/Services/DialogService.cs`).
- Unhandled exceptions are caught in `src/SavegameBunker/App.xaml.cs` and surfaced via `CrashReport.Describe`.

## Windows Platform Integrations

**Known folders:**
- `Environment.SpecialFolder.MyDocuments`, `ApplicationData`, `LocalApplicationData`, `UserProfile`, and `SHGetKnownFolderPath(FOLDERID_SavedGames)` (`src/SavegameBunker/Services/KnownFolders.cs`) - used by `src/SavegameBunker/Services/PathResolver.cs` to expand `%DOCUMENTS%`, `%SAVEDGAMES%`, and friends

**Drive information:**
- `System.IO.DriveInfo` and `Path.GetPathRoot` for volume hints - `src/SavegameBunker/Services/DriveHints.cs`

**Single-window interop:**
- `user32.dll` `SendMessage` and `RedrawWindow` in `src/SavegameBunker/App.xaml.cs`

## CI/CD & Deployment

**Hosting:**
- GitHub Releases for the application binary: `SavegameBunker-v<version>-win-x64.zip` plus a SHA-256 file. Created by `gh release create` in `.github/workflows/release.yml`.
- GitHub Pages for the online user guide: `.github/workflows/pages.yml` (environment `github-pages`, `actions/deploy-pages`).

**CI Pipeline:**
- GitHub Actions
  - `.github/workflows/test.yml` - `windows-latest`, `actions/setup-dotnet` with `global.json`, `dotnet build` and `dotnet test`. Runs on pull requests to `main` and via `workflow_call` / `workflow_dispatch`.
  - `.github/workflows/release.yml` - triggered by `v*` tags. Calls `test.yml`, then runs on `ubuntu-latest`: version pre-checks against `src/SavegameBunker/SavegameBunker.csproj` and `CHANGELOG.md`, `dotnet publish` single-file, help pack build with Zensical and `scripts/verify_help_pack_hrefs.py`, zip and SHA-256, `gh release create`, then calls `pages.yml`.
  - `.github/workflows/pages.yml` - builds `en` and `de` user guides with Zensical 0.1.1 (`pip install`), uploads via `actions/upload-pages-artifact`, deploys via `actions/deploy-pages`.
- Dependency updates: Dependabot (`.github/dependabot.yml`) for `nuget` and `github-actions`, monthly, grouped.
- Action pins: all `uses:` references are pinned to full commit SHAs.
- Permissions: workflows default to `contents: read`; only the release job has `contents: write`.

## Environment Configuration

**Required env vars:**
- None. The application runs with no environment variables set.
- CI uses the built-in `github.token` (`.github/workflows/release.yml`) for `gh release create`. No secrets are declared in tracked files.

**Secrets location:**
- Not applicable. No application secrets exist. No `.env` file is tracked (`.gitignore` covers local planning and build output).

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

---

*Integration audit: 2026-11-17*
