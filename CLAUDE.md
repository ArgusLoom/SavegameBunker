# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Working guidelines

Derived from [Andrej Karpathy's observations](https://x.com/karpathy/status/2015883857489522876)
on LLM coding pitfalls (MIT, via `andrej-karpathy-skills`). These bias toward caution over speed —
for trivial tasks, use judgment.

### 1. Think before coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity first

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical changes

**Touch only what you must. Clean up only your own mess.**

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.
- Remove imports/variables/functions that YOUR changes made unused; leave pre-existing dead code alone.

The test: every changed line should trace directly to the user's request.

### 4. Goal-driven execution

**Define success criteria. Loop until verified.**

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan as `[step] → verify: [check]`. Strong success criteria let
you loop independently; weak criteria ("make it work") require constant clarification.

## Commands

```powershell
dotnet build SavegameBunker.sln                          # build
dotnet test SavegameBunker.sln                           # run the xUnit suite
dotnet run --project src/SavegameBunker                  # run the WPF app
dotnet run --project src/SavegameBunker -- D:\retro.json  # run it against another catalog
```

The app and its xUnit suite run on Windows only (`net10.0-windows` + WPF). `dotnet build` also
works on Linux/macOS, because `Directory.Build.props` sets `EnableWindowsTargeting` — that checks
the compile, including XAML, but `dotnet test` and `dotnet run` still need Windows. CI runs the suite
on `windows-latest` (`.github/workflows/test.yml`) for every push and pull request to `main`, and
`release.yml` calls it first, so a tag never ships while the suite is red. The test host has no
`SynchronizationContext`, so `Progress<T>` reports land on the thread pool and can arrive late —
assert settled state with `WaitForAsync`, not a snapshot right after a command finishes. `global.json` pins the
.NET 10 LTS SDK so a build does not silently pick up whichever other SDK (8/9/11) is also installed.

## Hard constraints

- **Exactly two NuGet dependencies in the shipping app.** `src/SavegameBunker/SavegameBunker.csproj` carries
  exactly two shipping `PackageReference`s — WPF-UI 4.3.0 and CommunityToolkit.Mvvm 8.4.2 — and no
  third. No DI container, no logging library, no serialization library, no auto-update framework.
  Trimming stays off. Test-only packages (xUnit) remain the separate exception. The rule protects
  control-flow readability, which a theme dictionary and a source generator do not threaten.
- **`SPEC.md` is the public specification.** Scope changes revise it, carrying the reasoning
  inline in the relevant section, and `docs/DECISIONS.md` is updated alongside when a design
  decision changes. During an active milestone a local `.planning/REQUIREMENTS.md` holds that
  milestone's contract and wins where the two disagree; it is never committed.
- **The app does not touch the registry.** Launching, uninstalling and installation detection are
  deliberately out of scope; nothing reads `InstallLocation` or `UninstallString`. Do not add them back
  — every action is deliberately independent of whether a game is installed.
- **One catalog file:** `src/SavegameBunker/games.json`, copied to the build output
  (`CopyToOutputDirectory=PreserveNewest`). It is the file the app reads when no path is given on
  the command line.
- **The catalog file is the entire configuration.** It holds `backupRoot` next to the games, and the
  .exe takes a catalog path on the command line (falling back to `games.json` beside itself). Do not
  add a `settings.json` — `backupRoot` is catalog-level, not machine-level, and v1 has no value that
  belongs in a machine-level file. Relative `backupRoot` resolves next to the catalog file.
- **The backup destination is whatever folder the user chose, including one on the save data's own
  volume.** The chosen folder is honoured without comment — no warning fires, by design; see SPEC.md's *Backup destination* section for the reasoning. There must be
  no code path that refuses, redirects or silently relocates a backup based on where it points.
  `DriveType.Removable` is not a rule anywhere — external SSDs report as `Fixed`; volume type only
  picks a starting folder for the picker.
- **Forward slashes are valid separators in every path in the catalog** and are the form the seeded
  template shows, because doubled backslashes are JSON's one real hand-editing hazard.
- **Absent and malformed `games.json` are different.** Absent → write a commented template and start
  empty. Malformed → report the error and leave the file untouched. Never overwrite a file the user
  may have hand-edited.

## Architecture

Hand-rolled MVVM, no framework. Three layers, one direction of dependency:

**Startup** — `App.OnStartup` is the only composition root. It calls `GameCatalog.ResolvePath(e.Args)`,
awaits `LoadAsync`, constructs `MainWindow`, injects `MainViewModel(load, dialogService)` (a `DialogService` built from the window and WPF-UI's
content-dialog and snackbar services),
shows it, then calls `vm.ReportLoad()`. **Startup has no fatal path**: an absent catalog is seeded, a
malformed one is reported into the running window. The try/catch around the block is a last resort
for genuinely unexpected failures, not the normal error route.

**Services/** — stateless, no WPF references, instantiated directly as fields on `MainViewModel`.
`GameCatalog` loads, validates and writes the catalog. `BackupService` writes, verifies and lists
archives; `RestoreService` restores them. `PathResolver.TryResolve` expands
`%APPDATA%`/`%LOCALAPPDATA%`/`%USERPROFILE%` and the pseudo-variables `%DOCUMENTS%` and
`%SAVEDGAMES%`, which are not Windows environment variables — they are substituted from
`SpecialFolder.MyDocuments` and `SHGetKnownFolderPath(FOLDERID_SavedGames)` first, then the
rest is expanded normally. Both exist because OneDrive Known Folder Move relocates
Documents outside the user profile and Saved Games is relocatable too; a hardcoded
`%USERPROFILE%\Documents` is wrong on those machines, so prefer the pseudo-variables in every
template.

**Error contract:** services never throw across their boundary. They return `OperationResult`
(`Ok`/`Fail` + message) and `MainViewModel.Report()` pushes the message to both `StatusMessage` and a
dialog. `GameCatalog.LoadAsync` returns a `CatalogLoad` carrying the path, the valid entries, per-entry
problems, whether it seeded, and a fatal error — it does not throw either.

**UI boundary:** view models never touch `MessageBox`, folder pickers or any other dialog directly —
everything goes through `IDialogService` (`Info`/`Error`/`Confirm`, `PickFolder`, the Add Game /
Scan (`ShowFindGamesDialog`) / Change Save Folder / Restore Picker dialogs,
`OpenInEditor`/`OpenHttps`/`OpenHelpGuide`). `DialogService` is the only WPF-aware
implementation and marshals every call onto the UI thread itself, because services now call back into
it from background threads. Keep both properties so view models stay testable and thread-agnostic.

**Threading:** work that touches the filesystem runs off the UI thread. CommunityToolkit.Mvvm's
`AsyncRelayCommand` is the command type for it — it tracks its own in-flight state (`IsRunning`) and
raises `CanExecuteChanged` itself, so a busy guard bound to it actually disables the button.
`ZipFile.CreateFromDirectory` has no async overload, so `Task.Run` is the honest wrapper rather than a
fake `async` signature. An exception escaping a command's task surfaces on the UI thread, so handlers
passed to it must not throw — report through `OperationResult` instead.

**Destructive operations:** restore is the only one. Its order is load-bearing and must not be
reordered or short-circuited: **safety backup → extract to a temp sibling directory → remove the old
directory → rename the new one in.** Deleting the current save set is only defensible because the
step before it put that set on the backup volume.

## Conventions that differ from defaults

- **Everything written to disk is English** — code, comments, documentation, commit messages, UI
  strings, and the contents of `.planning/`. This holds regardless of the language the conversation
  is being held in; a German chat does not produce German files.
- Classes are `sealed` unless there is a reason not to be; file-scoped namespaces everywhere.
- XML doc comments on public types/methods, explaining *why*, referencing SPEC behaviour.

## Planning workflow

Planning is driven by GSD in a **local-only** `.planning/` directory. It is git-ignored and is not
part of the repository, so a fresh clone has none of it — nothing in the build, the tests or the
docs may depend on it. When it exists, it holds:

- `.planning/ROADMAP.md` — milestone history and the active milestone, if any
- `.planning/STATE.md` — current position, carried-in concerns and triaged defects (Blockers)
- `.planning/WISHLIST.md` — unordered pool of deferred ideas and accepted limitations worth
  revisiting; explicitly not a roadmap and not a commitment

Anything a reader of the public repository needs belongs in tracked files instead: behaviour in
`SPEC.md`, design reasoning in `docs/DECISIONS.md`, shipped changes in `CHANGELOG.md`, user-facing
limitations in `README.md`.
