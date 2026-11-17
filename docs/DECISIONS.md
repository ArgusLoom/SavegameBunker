# Design decisions

The choices that shape SavegameBunker, and why. [`SPEC.md`](../SPEC.md) carries the full
behaviour; this page is the short version for anyone wondering "why doesn't it just…?".

## Scope

| Decision | Why |
|----------|-----|
| No uninstall, no launch, no registry access | Windows and Steam already do both, and they were the only reasons to read the registry. With them gone, nothing needs `InstallLocation` or `UninstallString`. |
| Every action works whether or not the game is installed | Save data outlives an uninstall; backing up the saves of a just-removed game is a primary use case. |
| In-app "Add game" dialog | Hand-editing JSON was too high a barrier for the app's one job. |
| A save-vs-backup comparison on every row | It turns backup and restore from gambles into decisions. |

## Catalog and configuration

| Decision | Why |
|----------|-----|
| The catalog file (`games.json`) is the entire configuration — no `settings.json` | `backupRoot` belongs to the catalog, not to the machine: pointing the app at another catalog brings its backup folder with it. No genuinely machine-level setting exists yet. |
| The catalog path can be passed on the command line; a relative `backupRoot` resolves next to it | One exe can drive any number of catalogs, and a portable setup survives a changing drive letter. |
| An absent catalog is seeded with a template; a malformed one is reported and never touched | Absent and broken are different situations — overwriting a broken catalog would destroy something the user wrote by hand. |
| JSON, with forward slashes accepted in every path | Doubled backslashes are JSON's one real hand-editing hazard; accepting `/` removes it. |

## Backup and restore

| Decision | Why |
|----------|-----|
| The backup folder is whatever the user chose, including one on the saves' own disk — no warning | Same-disk backups still cover deletion, corruption and bad patches, which is how saves are actually lost. A warning about a deliberate choice is noise. |
| Volume type is never a rule (`DriveType.Removable` included) | External USB SSDs and HDDs report as `Fixed`. Volume type only picks a starting folder for the picker. |
| The free-space check is advisory, never a gate | Same reason as above: the destination is the user's call. |
| Every restore first takes an automatic safety backup, as an ordinary backup | Restore becomes recoverable through the existing backup path instead of a second mechanism. |
| Restore order: safety backup → extract to a temp sibling → move the old folder aside → rename the new one in | Deleting the current saves is only defensible because the step before it put them in the backup folder. The order must not change. |
| Per-row backup/restore is blocked while a bulk operation runs | A row action could otherwise run concurrently with the bulk run on the same files. |

## Build and dependencies

| Decision | Why |
|----------|-----|
| Exactly two NuGet packages: WPF-UI and CommunityToolkit.Mvvm | No DI container, logging, serialization or auto-update framework. The rule protects control-flow readability; a theme dictionary and an MVVM source generator do not threaten it. |
| Self-contained single-file exe, trimming off | "Another Windows 11 user runs it with no setup" is the success metric. WPF-UI resolves resources reflectively, so a trimmed build would fail silently at runtime. |
| An xUnit suite, although the spec never asked for one | Filesystem flows with hand-rolled path and archive handling cannot be re-checked by hand on every change. It runs on `windows-latest` for every push and pull request, and a release is only built after it passes. |
| Keyboard shortcuts route through the same commands as the buttons | One implementation for keyboard and mouse, so the two cannot drift apart. |

## Open

| Decision | Status |
|----------|--------|
| Follow the Windows High Contrast palette | Works for dark contrast schemes. A light contrast scheme still renders the window dark, because WPF-UI applies one fixed dark high-contrast dictionary. |
