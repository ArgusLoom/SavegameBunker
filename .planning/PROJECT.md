# SavegameBunker

## What This Is

SavegameBunker is a Windows 11 desktop application (WPF, .NET 10 LTS, WPF-UI Fluent dark shell)
that backs up PC game save data to a folder the user chooses and restores it again. Games and the
backup destination both live in one catalog file, edited from the UI or by hand, with an optional
known-games scan (~280 curated entries) that can find and add several games' save folders in one
pass. It also does bulk work: checkbox selection with "Back up selected" and "Restore selected", a
real-time name search, and keyboard access to per-row actions. It is a single-user, offline,
local-only tool — no accounts, no network, no registry access. Ships as a single self-contained
`.exe` that needs no .NET runtime installed and seeds its own catalog on first run.

## Core Value

A user can put a game's save data somewhere safe and get it back again, from one window, and can
see at a glance whether either action is worth doing right now.

## Current Milestone

**v1.0.1 — Hardening & Refactoring — released.**

Behavior-locked hardening: close the remaining R2 audit defects and split `MainViewModel.cs`
into sealed coordinators. The 629-test suite is the regression contract; the Owner's Windows
run is the verification of record. Scope and execution order: `REQUIREMENTS.md` + `ROADMAP.md`
Phase 1.

## Requirements

### Validated

<!-- Implemented, tested, and confirmed working via UAT. -->

- Catalog loading, seeding and validation.
- Path resolution incl. `%DOCUMENTS%`/`%SAVEDGAMES%`/`%APPDATA%`/`%LOCALAPPDATA%`/`%USERPROFILE%`,
  forward slashes accepted.
- One authoritative catalog reaching the build output.
- Catalog chosen by command-line argument with `backupRoot` relative to it.
- Add Game dialog with known-games type-ahead suggestions, drag-and-drop, PCGamingWiki lookup, and
  template-resolution preview.
- Off-UI-thread save-status scanning, sortable grid, merged relative-time Status cell.
- Crash-safe backup writes (temp-name-then-rename, reopen-and-verify), row-scoped progress ring,
  bulk "back up all changed", free-space advisory.
- Restore: safety-backup → temp-sibling-extract → rename-aside → rename-in, three-step progress
  bar, corrupt/mismatch/locked-folder handling, archive delete.
- Known-games scan: bounded-parallel sweep, shared-template and already-known dedup, collision
  refusal by name, stable per-entry ids.
- Checkbox selection per-row + header select-all; "Back up selected" / "Restore selected"
  (per-game confirmation, strictly sequential); real-time name search.
- Startup reminder for games with unbacked-up changes; startup backup-destination health check.
- Alt-key toolbar access, Ctrl+B/Ctrl+R row actions; dialogs usable at 125–150% text scaling.
- Minimal shippable UI, plain-language crash handling (dispatcher and unobserved-task paths),
  self-contained single-file `.exe`, Windows High Contrast (dark case) followed.

Covered by 555+ xUnit tests in `tests/SavegameBunker.Tests/`.

### Active

Candidates not yet scoped into a milestone, carried in `.planning/WISHLIST.md`:

- Proactive, scheduled backup reminder — today's startup nudge is one-time per session, not a
  recurring schedule.
- Extensible known-games table — the ~280-entry table is embedded in the `.exe` and can only grow
  by rebuilding from source.
- `DialogService.Error` is fire-and-forget; `Confirm` blocks on `.GetResult()` from a worker
  thread — two `Report()` calls racing from different background operations could show two
  overlapping dialogs or silently drop one.

Already-known v2 candidates: per-game backup destination override, backup pruning/retention,
catalog entry edit/remove, cancel-in-flight, `settings.json`, logging.

### Out of Scope

Boundaries, not backlog.

- **Launching games** — Steam, the Start menu and the desktop already do it.
- **Uninstalling games** — Windows' Apps & Features does it.
- **Registry / installation detection** — nothing in the product consumes `InstallLocation` or
  `UninstallString`; save data is install-independent.
- **Automatic detection of unknown games** — partially addressed by the known-games scan; true
  auto-discovery of arbitrary games remains out of scope.
- **Cloud backup / cloud sync** — the destination is a local folder the user picks; no service
  dependency, no accounts.
- **Automatic backup pruning** — listing and deleting a single backup is in scope; retention
  policies are not.
- **Support for other operating systems** — the design leans on Windows volume semantics and WPF.
- **Save data encryption** — backups are plain ZIPs; encryption would block manual recovery.
- **Auto-update** — the deliverable is a hand-copied `.exe`.
- **Localization** — UI text language is irrelevant; code and comments stay English. (The user
  guide itself is bilingual, English/German.)
- **Light Windows High Contrast theme** — the app's single fixed HighContrast dictionary is dark;
  a user on a *light* contrast theme still gets a dark window (accent colors correctly follow the
  OS palette, the window ground does not).

## Context

SavegameBunker started as a brownfield repo with a larger scope — `RegistryScanner`,
`LaunchService`, `UninstallService` alongside `BackupService`, `PathResolver` and `GameCatalog`.
Those three and the installed/not-installed model were deleted early on, since Steam and Windows
already cover launching and uninstalling. The rest of the app — catalog editing, save-status
scanning, the known-games scan, backup, restore, the WPF-UI Fluent shell, resilience (crash
handling, per-row progress, plain-language errors), bulk selection/search, startup health checks,
and keyboard/text-scaling accessibility — shipped together as v1.0.

**Current codebase state:** ~9,750 LOC (C# + XAML), 555+ xUnit tests, `net10.0-windows` WPF, x64,
SDK pinned to .NET 10 LTS. Two shipping NuGet dependencies (WPF-UI 4.3.0, CommunityToolkit.Mvvm
8.4.2) — the original zero-dependency rule was amended for a theme dictionary and MVVM source
generator that don't threaten control-flow readability. Hand-rolled MVVM otherwise;
`IDialogService` is the only UI seam.

**Known carried-forward items** (see `.planning/WISHLIST.md` for full detail):
- Light Windows High Contrast theme stays dark (documented limitation, see Out of Scope above).
- `BackupService`/`RestoreService`'s own success messages diverge slightly from the UI copywriting
  contract — cosmetic.
- v2 candidates — per-game backup destination override, backup pruning, catalog entry edit/remove,
  cancel-in-flight, `settings.json`, logging.
- Backup/restore verification compares entry names and lengths only, never a checksum (.NET has no
  CRC-32 validation on read) — SPEC.md *Known limits*.
- Two writers (a second row action, or bulk backup) can still run concurrently against *different*
  rows with no cross-row lock — same-row and preflight-window collisions are closed, and a per-row
  action against a *running bulk operation* is blocked; two unrelated per-row actions in parallel
  remain possible by design (different save folders, no data-corruption risk).
- `IdSlug` has no length cap — an overlong id fails with `PathTooLong` only at write time, not at
  entry.

**Deliverable definition (met):** a self-contained `SavegameBunker.exe` runs on a clean Windows 11
machine with no .NET runtime install, seeds its own `games.json`, backs up and restores saves, and
survives a removable drive changing letter.

## Constraints

Derived from `SPEC.md`. No ADRs exist, so nothing here is decision-locked — these are
specification constraints and may be revised only by revising the SPEC.

- **Tech stack**: .NET 10 LTS / C#, WPF, WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, `net10.0-windows`, x64
- **Platform**: Windows 11 only — volume semantics and WPF have no cross-platform path
- **Dependencies**: exactly two shipping NuGet packages (WPF-UI, CommunityToolkit.Mvvm); no DI
  container, no logging library, no serialization library, no auto-update framework; test-only
  packages (xUnit) are the separate exception
- **Data**: the catalog file holds `backupRoot` and the games; it comes from a command-line
  argument or `games.json` next to the .exe; absent means seed a template, malformed means report
  and never overwrite
- **Paths**: forward slashes accepted everywhere; a relative `backupRoot` resolves next to the
  catalog file
- **Backup target**: the configured `backupRoot`, any folder at all, including one on the save
  data's own volume — never redirected, never refused, no warning
- **Backup path**: `<backupRoot>\<id>\<yyyy-MM-dd_HHmmss>.zip`, written under a temporary name and
  renamed on success, then verified by reopening
- **Restore order**: safety backup → extract to temp sibling → remove old → rename in. No other
  order is acceptable
- **Error handling**: every filesystem operation is guarded; failures surface via `OperationResult`
  through `MainViewModel.Report()`; no silent failures and the app must never crash — both the
  dispatcher and unobserved-task exception paths are caught and reported
- **Threading**: every filesystem operation runs off the UI thread
- **UI**: WPF-UI Fluent dark shell, fixed green accent, one grid with per-row actions, merged
  Status cell, plus Add game / Scan / Refresh / Open catalog file / Change backup folder; Windows
  High Contrast (dark) followed; no onboarding, no settings screen

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Remove Uninstall and Launch | Both duplicate functionality Windows and Steam already provide; together they were the only consumers of the registry layer | ✓ Good |
| Drop the registry layer entirely with them | With no consumer for `InstallLocation`/`UninstallString`, keeping the scanner would serve nothing | ✓ Good |
| All actions independent of installation status | Save data outlives an uninstall; archiving saves of a just-removed game is a primary use case | ✓ Good |
| Seed `games.json` when absent, never when malformed | Absent and broken are different situations; overwriting a broken catalog would destroy user configuration | ✓ Good — verified end-to-end on a clean-machine walkthrough |
| In-app "Add game" dialog | Hand-editing JSON was too high a barrier for the app's one job | ✓ Good — later extended with known-games suggestions, drag-and-drop and a wiki lookup |
| Automatic pre-restore backup, taken as an ordinary backup | Makes restore recoverable using the existing backup path instead of a second mechanism | ✓ Good |
| Show a save-vs-backup comparison per row | Turns backup and restore from gambles into decisions | ✓ Good |
| Backup destination is a user-configured folder, same-disk allowed, no warning | Same-disk backups still cover deletion, corruption and bad patches, the case saves are actually lost to; a warning on a deliberate configuration is noise | ✓ Good — no complaints raised |
| `DriveType.Removable` is not a rule anywhere | External USB SSDs and HDDs report as `Fixed`; volume type only picks a starting folder for the picker | ✓ Good |
| `backupRoot` lives in the catalog file, not in a `settings.json` | Catalog-level, not machine-level; pointing the .exe at a different catalog brings its destination with it | ✓ Good — no `settings.json` was ever added |
| Catalog path accepted on the command line; relative `backupRoot` resolved next to it | Makes one .exe drive any number of catalogs, and survives a changing drive letter | ✓ Good — verified directly on a removable drive |
| JSON kept over XML or INI, with forward slashes accepted | Doubled backslashes were JSON's one real hand-editing hazard; accepting `/` removes it | ✓ Good |
| Self-contained single-file publish as the exit gate | Success metric is "another Windows 11 user runs it with no setup" | ✓ Good — verified on a clean machine with no .NET runtime |
| Add a test project (xUnit) even though SPEC did not mention testing | Filesystem flows with hand-rolled path and archive handling cannot be re-verified by hand on every change | ✓ Good — grew to 555+ tests, caught real regressions |
| Amend zero-NuGet rule to exactly-two (WPF-UI, CommunityToolkit.Mvvm) | Neither a theme dictionary nor an MVVM source generator threatens control-flow readability, the rule's original target | ✓ Good |
| Windows High Contrast: follow OS palette | Accessibility — the app must not force its fixed dark theme on a user who has opted into a system contrast theme | ⚠️ Revisit — works for dark contrast schemes; a light contrast scheme still renders the window dark |
| Free-space check is advisory only, never a gate | Backup destination choice is the user's; a warning on a deliberate configuration is noise | ✓ Good |
| Per-row keyboard shortcuts (Ctrl+B/Ctrl+R) route through the existing RelayCommands | One command implementation for keyboard and mouse; no parallel code path to drift | ✓ Good |
| Block per-row backup/restore while a bulk operation runs | A per-row action on an unrelated row could otherwise start concurrently with a bulk run | ✓ Good |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-11-17 after the v1.0 initial release*
