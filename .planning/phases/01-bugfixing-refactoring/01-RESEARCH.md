# Phase 1: Bugfixing & Refactoring - Research

**Researched:** 2026-11-17
**Domain:** C#/.NET 10 WPF desktop app — hand-rolled MVVM view-model decomposition + two targeted
service-layer bugfixes, behavior-locked throughout.
**Confidence:** HIGH

## Summary

This phase has no external unknowns — it is a closed-world refactor of a codebase this research
read directly. `MainViewModel.cs` is 1,886 lines (verified by reading the full file this session,
not the ~1,885/1,933 figures quoted in STATE.md/CONCERNS.md, which are close but not exact — treat
1,886 as current truth). It already has two extracted `internal sealed` collaborators
(`GameActions`, `CatalogSession`) that establish the exact pattern REF-02 must continue: a plain
class, no WPF types, no `[ObservableProperty]` state of its own, held as a `private readonly`
field, constructed in `MainViewModel`'s constructor, never exposed as a `public` property. Neither
existing collaborator owns observable UI state or constructs its own `Progress<T>`/`IProgress<T>`
— both are handed one by the caller or return plain values. This is the load-bearing constraint for
REF-02/REF-03: the three new coordinators (bulk-backup, status-sweep, row-actions) must follow the
same shape, or the threading discipline in REF-03 silently breaks (see Pitfall 1).

The two bugfixes are narrow and already located precisely: REF-04a is a specific `catch` clause in
`BackupService.cs` (confirmed at lines 152-169) whose cap-exceeded path falls through to a generic
exception handler; REF-04b is a specific missing argument (`onSkipped`) in a specific call
(`SaveStatusScanner.cs` line 51) to an already-shared helper (`DirectoryWalk.ForEachFile`) that
already supports what's missing. Neither fix touches a third file. REF-05 requires a new contract
test file — no such file exists in the test suite yet (confirmed by directory listing) — exercising
every public method on the `SavegameBunker.Services` namespace classes with hostile/invalid input
and asserting no exception crosses the boundary.

**Primary recommendation:** Extract strictly in the order the roadmap already specifies (pure
helpers → bulk-backup → status-sweep → row-actions → final composition-root cleanup), keep every
new coordinator's shape identical to `GameActions`/`CatalogSession` (plain `internal sealed` class,
no observable state, callbacks/`IProgress<T>` passed in by the caller), and do the two REF-04
bugfixes as small, independent, test-first commits that do not depend on the split at all — they
can land before, after, or interleaved with the five extraction plans.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REF-01 | Behavior parity — 629-test suite passes unchanged | See "Architecture Patterns" (extraction shape that keeps `MainViewModel`'s public command/property surface — and therefore `MainViewModelTests.cs`, 3,269 lines / 155 `[Fact]`/`[Theory]` attributes — untouched) and "Validation Architecture" |
| REF-02 | `MainViewModel.cs` → composition root ≤~700 lines; 3 new sealed coordinators | See "Architecture Patterns" line-range breakdown of the exact three extraction targets, verified against the full file read this session |
| REF-03 | Uniform `OnUi()`/`IProgress` threading; `IsAnyRowChecking` / reset-to-Checking guard preserved | See Pitfall 1 (Progress<T> construction site) and Pitfall 2 (re-entrancy guard placement) |
| REF-04 | Rename-retry cap message distinguishable from disk error; Scan surfaces skipped paths like Backup | See "Common Pitfalls" / "Code Examples" — both exact call sites quoted verbatim below |
| REF-05 | Never-throw service contract, verified by a contract test pass | See "Don't Hand-Roll" and "Validation Architecture" — full enumerated Services surface below |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Bulk-backup orchestration (BackupAllChanged/BackupSelected/RestoreSelected loops) | ViewModels (new `internal sealed` coordinator) | Services (`BackupService`, `RestoreService`) | Orchestrates UI-bound rows and drives services; owns no filesystem logic itself — matches existing `GameActions` tier |
| Status-sweep engine (`StartStatusScan` + `RecomputeSpaceAdvisoryAsync`) | ViewModels (new `internal sealed` coordinator) | Services (`SaveStatusScanner`, `BackupComparer`, `BackupService.ListBackups`) | Per-row scan scheduling/token bookkeeping is view-model-shaped state; the actual scan logic already lives in Services |
| Per-row actions (single Backup/Restore/OpenSaveFolder/ChangeSaveFolder/RemoveGame) | ViewModels (new `internal sealed` coordinator, or folded into `GameActions`) | Services | Thin per-row delegation, same tier as the capability it replaces inside `MainViewModel` today |
| Rename-retry message distinction (REF-04a) | Services (`BackupService`) | — | Already the exact file/method; no tier change |
| Scan skip-surfacing (REF-04b) | Services (`SaveStatusScanner`) + Models (`SaveStatus` if a carried field is needed) | ViewModels (`GameRowViewModel` display, `MainViewModel` sweep message) | The walk already reports skips via `DirectoryWalk`'s `onSkipped` callback; only the wiring from Services up to a user-visible surface is missing |
| Never-throw contract verification (REF-05) | Test project (new contract test) | Services (every public method) | Enumerates the existing Services surface; adds no new production code |

## Standard Stack

No new libraries. This phase is forbidden from adding any (`CLAUDE.md`'s two-dependency rule;
confirmed still exactly two shipping packages: `WPF-UI` 4.3.0 and `CommunityToolkit.Mvvm` 8.4.2 in
`src/SavegameBunker/SavegameBunker.csproj` — `[VERIFIED: src/SavegameBunker/SavegameBunker.csproj]`,
consistent with `.planning/codebase/STACK.md`). Test tooling is also unchanged: xUnit 2.9.3,
`xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk` 18.12.0, no mocking library, no
assertion library beyond `Assert`.

### Core (existing, reused)
| Library | Version | Purpose | Why Standard (for this phase) |
|---------|---------|---------|--------------------------------|
| CommunityToolkit.Mvvm | 8.4.2 | `ObservableObject`, `RelayCommand`, `AsyncRelayCommand` | `MainViewModel` keeps every `[ObservableProperty]`/command — coordinators do not need it unless a coordinator itself becomes observable, which this research recommends against (see Pitfall 3) |
| xUnit | 2.9.3 | Test framework for REF-05's new contract test and REF-04's new regression tests | Matches every existing test file's convention |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reflection-based Services-surface enumeration for REF-05 | A hand-maintained list of service types/methods | Reflection self-updates when a method is added/removed later and cannot silently go stale; a hand list can skip a new method. Reflection is recommended — see "Don't Hand-Roll" |

**Installation:** None — no packages to add.

**Version verification:** Not applicable; no new package versions to confirm. Existing pins
re-verified against `SavegameBunker.csproj` and `global.json` this session (SDK 10.0.400 pinned,
`10.0.404` installed locally and roll-forward compatible — `[VERIFIED: dotnet --version output
this session]`).

## Package Legitimacy Audit

Not applicable — this phase adds zero new NuGet packages. `CLAUDE.md`'s two-dependency rule is a
hard constraint for the whole milestone (`.planning/REQUIREMENTS.md` Out of Scope: "Any new NuGet
dependency"). No package legitimacy check was run because there is nothing to check.

## Architecture Patterns

### Current State — MainViewModel.cs anatomy (verified by reading all 1,886 lines this session)

```
MainViewModel.cs (1,886 lines total)
├── Fields & ctor                         lines 23–161   (~140 lines) — STAYS (composition root)
├── Games/GamesView/RegisterRow/InsertSorted  163–253    (~90 lines)  — STAYS (row lifecycle)
├── Aggregates (HasBackupTargets, IsAnyRowChecking, SelectedCount, labels…)  255–395  — STAYS
├── RecomputeSpaceAdvisoryAsync + advisory properties   333–453   (~120 lines) — EXTRACT → status-sweep coordinator
├── Bulk-backup progress properties (BulkBackupCurrent/Total/CurrentGameName)  455–504 — STAYS on MainViewModel as [ObservableProperty]; coordinator writes through callbacks, does not own them (see Pitfall 3)
├── Search/filter, window title, header display properties  496–610 — STAYS
├── StatusMessage/StatusLine/Report       612–643, 1860–1884 — STAYS (single report sink)
├── IsBackupRunning/IsRestoreRunning/IsScanRunning + in-flight counters  644–703 — STAYS (command gating state)
├── ReportLoad, startup-prompt arming/consumption  710–832 — STAYS (small, cohesive, not named in REF-02's three targets)
├── AddGameAsync/FindGamesAsync/OpenCatalogAsync/HelpAsync/RefreshAsync/ChangeBackupRootAsync  839–1054 — STAYS (not named in REF-02's three targets; catalog/session gestures already delegate to CatalogSession)
├── StartStatusScan (the sweep engine itself)  1056–1234  (~180 lines) — EXTRACT → status-sweep coordinator
├── BackupAsync (single-row), CanBackupRow            1241–1302 (~60 lines) — EXTRACT → row-actions
├── RestoreAsync (single-row), CanRestoreRow          1304–1386 (~85 lines) — EXTRACT → row-actions
├── OpenSaveFolderAsync, ChangeSaveFolderAsync, RemoveGameAsync   1388–1511 (~125 lines) — EXTRACT → row-actions
├── BackupAllChangedAsync/CanBackupAllChanged/RunBulkBackupAsync  1513–1663 (~150 lines) — EXTRACT → bulk-backup coordinator
├── BackupSelectedAsync/CanBackupSelected             1665–1692 (~30 lines)  — EXTRACT → bulk-backup coordinator
├── RestoreSelectedAsync/CanRestoreSelected           1694–1822 (~130 lines) — EXTRACT → bulk-backup coordinator
└── EnsureBackupRootAsync                             1824–1858 (~35 lines) — STAYS (shared by both per-row and bulk paths; keep on composition root, pass as a delegate/func or call back up, do not duplicate into two coordinators)
```

Rough post-split arithmetic: extracting status-sweep (~300 lines incl. advisory), row-actions
(~270 lines), and bulk-backup (~310 lines) removes roughly 880 lines of logic from 1,886, landing
`MainViewModel.cs` at **~1,000 lines before command-wrapper/property overhead is also trimmed** —
REF-02's ≤~700-line target requires the `[RelayCommand]`-decorated methods themselves to shrink to
thin one-line forwarding calls (matching the existing style:
`private Task AddGameAsync() => AddGameCoreAsync(initialSaveFolder: null);`), not just moving the
bodies. Budget this explicitly in the plan rather than treating "extract the three coordinators" as
sufficient on its own.

### Pattern 1: `internal sealed` collaborator with no observable state (the established pattern)
**What:** A plain class, `internal sealed`, taking its dependencies through the constructor,
exposing `public`/`internal` methods that return plain values, records, or `OperationResult` —
never `[ObservableProperty]`, never deriving from `ObservableObject`, never a WPF type anywhere in
its signature.
**When to use:** Every one of REF-02's three new coordinators.
**Example (verified, read this session):**
```csharp
// Source: src/SavegameBunker/ViewModels/GameActions.cs (lines 30-54)
internal sealed class GameActions
{
    private readonly BackupService _backup;
    private readonly RestoreService _restore;
    private readonly IDialogService _dialog;

    public GameActions(BackupService backup, IDialogService dialog)
    {
        _backup = backup;
        _restore = new RestoreService(backup);
        _dialog = dialog;
    }

    public Task<OperationResult> BackupAsync(GameRowViewModel row, string backupRoot, IProgress<long>? progress = null) =>
        Task.Run(() => _backup.Backup(row.Game, backupRoot, progress));
```
Note the `IProgress<long>? progress` parameter is *accepted*, never constructed, by this
collaborator — the caller (`MainViewModel.BackupAsync`) builds `new Progress<long>(...)` itself,
on the UI thread, and hands it down. This is the pattern the new coordinators must copy exactly.

### Pattern 2: `Progress<T>` constructed on the UI thread, passed down
**What:** Every `Progress<T>`/`Progress<(int,string)>` instance in `MainViewModel` is constructed
inline inside the `[RelayCommand]`-decorated method, before any `await`/`Task.Run`, so its
callback captures the `SynchronizationContext` that was current at construction — WPF's
`DispatcherSynchronizationContext` on the real app, none in the xUnit host.
**When to use:** Any time an extracted coordinator needs to report progress to a row or to
`BulkBackupCurrent`/`BulkBackupCurrentGameName`.
**Example (verified, read this session):**
```csharp
// Source: src/SavegameBunker/ViewModels/MainViewModel.cs, lines 1268-1273 (BackupAsync)
// Constructed here, on the UI thread, so its callbacks marshal back onto the
// dispatcher rather than arriving on the Task.Run thread inside GameActions.BackupAsync
// — the same reason RestoreAsync constructs its own Progress<int> here.
var progress = new Progress<long>(written => { if (row.BackupBytesTotal > 0) row.BackupBytesWritten = written; });

var result = await _gameActions.BackupAsync(row, root, progress);
```

### Pattern 3: `OnUi()` for any write to a bound row/property from a background thread
**What:** A private static helper that runs an action on the UI dispatcher when one exists, or
inline when it doesn't (test host has no `Application.Current`).
**When to use:** Any coordinator extraction MUST either (a) keep every `OnUi()` call site with the
code it marshals when that code moves, or (b) leave `OnUi()` itself on `MainViewModel` and have
the coordinator call back into a passed-in `Action<Action>` / delegate for marshalling. Do not let
a coordinator write to `GameRowViewModel` properties directly from inside its own `Task.Run` without
going through an equivalent of `OnUi()` — this is the exact crash CLAUDE.md's threading section and
`ARCHITECTURE.md`'s Dialog/Threading notes describe.
**Example (verified, read this session):**
```csharp
// Source: src/SavegameBunker/ViewModels/MainViewModel.cs, lines 1086-1094
private static void OnUi(Action action)
{
    var dispatcher = System.Windows.Application.Current?.Dispatcher;
    if (dispatcher is null || dispatcher.CheckAccess())
        action();
    else
        dispatcher.Invoke(action);
}
```

### Pattern 4: Re-entrancy guard is "reset rows to Checking, then the sweep settles them"
**What:** `IsAnyRowChecking` (a pure aggregate: `Games.Any(row => row.ComparisonKind ==
BackupComparisonKind.Checking)`) is the guard every bulk command's `CanExecute` checks. A bulk run
sets its targets' comparison to `Checking` at the start (`row.ApplyBackupComparison(
BackupComparison.Checking())`) — this flips `IsAnyRowChecking` true immediately and disables the
same command until the closing `StartStatusScan(only: targets)` settles everything again.
**When to use:** Whichever coordinator ends up owning `RunBulkBackupAsync`/`RestoreSelectedAsync`
must preserve this exact reset-then-rescan sequence — it IS the re-entrancy guard REF-03 names, not
an incidental detail. Do not let the coordinator skip the upfront reset, and do not let it return
before the view model's final `StartStatusScan(only: targets)` runs.
**Example (verified, read this session):**
```csharp
// Source: src/SavegameBunker/ViewModels/MainViewModel.cs, lines 1552-1556
foreach (var row in targets)
    row.ApplyBackupComparison(BackupComparison.Checking());
```

### Anti-Patterns to Avoid
- **A coordinator that constructs its own `Progress<T>`:** breaks dispatcher-affinity capture
  (Pattern 2) — the callback would arrive on whatever thread the coordinator happened to be on when
  it built the `Progress<T>`, not the UI thread, reproducing the exact crash
  `_gamesSyncLock`/`EnableCollectionSynchronization` was added to prevent for a different call site.
- **A coordinator that becomes `ObservableObject` and owns `BulkBackupCurrent`/`BulkBackupTotal`
  directly:** `MainViewModel`'s XAML bindings (not read this session, but implied by
  `[ObservableProperty]` + `[NotifyPropertyChangedFor]` on these exact properties) bind to
  `MainViewModel` itself. Moving the properties would require either re-pointing every XAML binding
  (out of scope: `SPEC-defined behavior`/UI is explicitly untouched) or forwarding every
  `PropertyChanged` from the coordinator back up — strictly more code and a second place state can
  drift. Keep the bound properties on `MainViewModel`; the coordinator takes/returns plain values.
- **Reordering `RunBulkBackupAsync`'s try/finally:** the `finally` block
  (lines 1629-1643 in the current file) is what prevents an unexpected mid-loop exception from
  leaving every target row `IsBusy = true` and the bulk counters non-zero forever, which would
  permanently disable `CanRefreshOrChangeBackupRoot` and every other command gated on
  `IsBulkBackupRunning`. Preserve the finally's exact contents verbatim when the method moves.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Enumerating every service's public surface for REF-05's contract test | A hand-maintained list of method names | `System.Reflection` over the `SavegameBunker.Services` namespace's public types (`typeof(BackupService).Assembly.GetTypes().Where(t => t.Namespace == "SavegameBunker.Services" && t.IsPublic)`) | A hand list silently stops matching the surface the moment a new public method is added; reflection re-derives the surface every run, matching the Testing-Relevant convention of "nothing hidden behind an interface the test can't reach" already used for `ResourceKeyTests`/`StringsTests` |
| Scan's skip reporting shape | A second skip-collection mechanism parallel to `BackupService`'s | `DirectoryWalk.ForEachFile`'s existing `onSkipped` parameter — already a shared abstraction both `BackupService` and `SaveStatusScanner` call, already solving exactly this (see Code Examples) | `DirectoryWalk.cs`'s own remarks explicitly state it was "extracted so the exception handling, reparse-point rules and symlink skip live in one place instead of two drifting copies" — building a second mechanism here would be the regression that extraction was meant to prevent |
| Rename-retry cap message distinction | A new exception type, or a second try/catch wrapper around the whole `Backup` method | A narrowly-scoped catch on the cap-exceeded condition only, inside the existing retry loop, returning a distinct `OperationResult.Fail` message before the loop's own exit path reaches the generic outer catch | The existing generic `catch (Exception ex)` at line 171 already exists and correctly handles every *other* `IOException` (disk full, vanished dir); only the specific "100 collisions exhausted" case needs a different message, not a different exception-handling architecture |

**Key insight:** Every "don't hand-roll" item in this phase is "don't build a second version of a
mechanism that already exists one call away" — this is a maturation refactor of an already-careful
codebase, not greenfield work. The research task was to locate the existing mechanism precisely
enough that the plan does not duplicate it.

## Runtime State Inventory

This phase is a refactor (`MainViewModel.cs` split) plus two bugfixes, but it is **not** a
rename/rebrand/identifier-migration phase: no catalog key, string-resource key, `Game.Id`, folder
name, or environment-variable name changes. Each category below was checked explicitly against
what this phase actually touches.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no database/datastore keys (`games.json`'s `backupRoot`/game `id`s), and no catalog schema field is renamed or moved by any REF-01..05 requirement | None |
| Live service config | None — this app has no external service with its own UI/database config (no n8n, no cloud service); `.github/dependabot.yml` and workflow files are unaffected by a pure C# class split | None |
| OS-registered state | None — no scheduled tasks, no `pm2`/`launchd`/`systemd` units; `app.manifest` and `global.json` are unaffected | None |
| Secrets/env vars | None — the app reads no secrets; `PathResolver`'s environment-variable *reads* (`%APPDATA%` etc.) are unaffected by moving which C# class calls `PathResolver` | None |
| Build artifacts | None expected — splitting one `.cs` file into several under the same project (`src/SavegameBunker/SavegameBunker.csproj` uses the SDK's default implicit glob, no explicit `<Compile Include>` list was found for `ViewModels/*.cs`) requires no `.csproj` edit; a clean `dotnet build` picks up new files automatically | None — but the plan should still run one full `dotnet build` after each new file is added to catch a scenario where a glob exclusion exists that this research didn't find (not observed, but cheap to confirm) |

**Nothing found in any category** — verified by reading `ARCHITECTURE.md`'s Configuration/Data-Flow
sections, `STACK.md`'s Configuration section, and the phase's own Out-of-Scope list, none of which
name any external, persisted, or OS-registered state this split or these two bugfixes would touch.

## Common Pitfalls

### Pitfall 1: Moving `Progress<T>` construction into the new coordinator breaks dispatcher affinity
**What goes wrong:** If `BulkBackupCoordinator.RunAsync(...)` (or similar) constructs its own
`new Progress<(int,string)>(...)` or `new Progress<long>(...)` instead of receiving one from
`MainViewModel`, the callback captures whatever `SynchronizationContext` is active at the moment
the coordinator's method runs — which may already be inside a `Task.Run`, i.e. no WPF dispatcher
context at all. Reports then arrive on a thread-pool thread and write directly to a bound
`GameRowViewModel`/`MainViewModel` property, which is exactly the "calling thread cannot access
this object because a different thread owns it" failure `MainViewModel`'s own `_gamesSyncLock` doc
comment (lines 42-55) already names as a real, previously-hit failure mode for a different call
site.
**Why it happens:** `Progress<T>`'s whole contract is "callback runs on whatever
`SynchronizationContext.Current` was when the constructor ran" — this is easy to miss when moving
code between classes, because the code still compiles and passes in the xUnit host (no dispatcher
there either way).
**How to avoid:** Every `new Progress<...>(...)` call site identified in the line-range table above
stays in `MainViewModel`, constructed before the call into the coordinator, exactly as `GameActions`
already requires its callers to do.
**Warning signs:** A manual Windows run (the Owner's verification of record) shows a crash or a
silently-stuck progress bar during a bulk backup/restore that the xUnit suite did not catch — the
test host's missing dispatcher means this class of bug is invisible to automated tests and can only
surface on the manual run, which is exactly why this pitfall must be caught by code-reading/plan
discipline, not by the test suite.

### Pitfall 2: Reset-to-Checking must happen before the coordinator call starts, not after
**What goes wrong:** If a coordinator is handed targets and internally defers the
`row.ApplyBackupComparison(BackupComparison.Checking())` reset until partway through its own body,
there is a window after `CanExecute` was last checked but before the reset where a second bulk
command (or a per-row command on the same row) can still execute, because `IsAnyRowChecking` has
not yet flipped true.
**Why it happens:** Natural refactor instinct is to move the whole method body as one unit and
trust that "it still runs in the same order" — but if the reset loop gets reordered relative to
property-read gates during extraction (e.g., to batch it with the `IsBusy = true` loop), the
re-entrancy window reopens.
**How to avoid:** Preserve the literal statement order from `RunBulkBackupAsync`/
`RestoreSelectedAsync`: reset comparison to `Checking` → set `BulkBackupTotal`/`Current` → set
`row.IsBusy = true` for every target → run the loop. REF-03 explicitly calls this out as existing
behavior that "must be preserved and documented where it now lives" — the new coordinator's own XML
doc comment should restate this ordering requirement, not just the code.
**Warning signs:** A new or existing test exercising "second bulk command while first is running"
(none currently exists for this exact race per the test-count audit below — see Validation
Architecture) starts flaking only under load/parallelism, not deterministically.

### Pitfall 3: `MainViewModelTests.cs` (3,269 lines, 155 `[Fact]`/`[Theory]` attributes) is the
behavior-parity oracle — treat its public surface as frozen
**What goes wrong:** REF-01 requires the 629-test suite to pass *unchanged*. `MainViewModelTests.cs`
drives every bulk/per-row command entirely through `MainViewModel`'s public `[RelayCommand]`
properties (`vm.BackupCommand.ExecuteAsync(...)`, etc.) and public/`internal` properties — never
through `GameActions`/`CatalogSession` directly. If any extraction changes a command's name,
signature, `CanExecute` wiring, or the shape/timing of a `PropertyChanged` notification those tests
assert on, those tests fail and REF-01 is violated.
**Why it happens:** It's tempting, when extracting a coordinator, to also rename the command method
for clarity (e.g., `BackupAllChangedAsync` → delegate straight to
`_bulkBackup.BackupAllChangedAsync()` with a different internal name) — as long as the
`[RelayCommand]`-generated public command name and `CanExecute` method name stay the same, this is
safe; renaming the *private* method is fine, renaming or removing the generated public command is
not.
**How to avoid:** Keep every `[RelayCommand(CanExecute = nameof(...))]` attribute, its generated
command property name, and its `Can*` method's boolean logic byte-for-byte identical. The
coordinator extraction should only ever move the *body* behind the thin wrapper, following the
exact shape `AddGameAsync() => AddGameCoreAsync(initialSaveFolder: null);` already demonstrates for
a different flow.
**Warning signs:** Any test in `MainViewModelTests.cs` needs to change for the suite to go green —
per REF-01, that is only acceptable "where a test asserts a removed defect contract that an R2 fix
supersedes, and then only with owner-visible justification." A test failing because a command
signature moved is not that case.

### Pitfall 4: The rename-retry cap fix must not change the *successful* suffix-retry path
**What goes wrong:** `Backup_survives_a_final_rename_collision_by_suffixing_the_archive` (existing
test, `tests/SavegameBunker.Tests/BackupServiceTests.cs` lines 516-540) asserts the *normal* 1-2
collision case still succeeds with a `-1.zip` suffix. A fix aimed at the ≥100-collision case must
only change behavior once `attempt` reaches the cap — touching the loop's general shape risks
breaking this passing test (REF-01 would then be violated by an unrelated regression).
**Why it happens:** The cleanest-looking fix is to wrap the whole `for` loop in a new
try/catch, which is tempting but changes the catch semantics for every iteration, not just the
100th.
**How to avoid:** Add a dedicated branch (not a restructuring) that fires only when the loop's own
exit condition (`attempt < 100` false, `File.Exists(candidate)` true) is reached — i.e. catch the
specific "ran out of attempts" case distinctly from the `catch (IOException) when (attempt < 100 &&
File.Exists(candidate))` filter already there, rather than loosening that filter.
**Warning signs:** `Backup_survives_a_final_rename_collision_by_suffixing_the_archive` or
`Archive_timestamp_is_taken_after_writing_and_verifying_not_before` start failing after the REF-04a
fix — both exercise the same loop.

## Code Examples

### REF-04a — exact site of the rename-retry cap (verified, read this session)
```csharp
// Source: src/SavegameBunker/Services/BackupService.cs, lines 152-177 (shown with the enclosing
// try/catch it sits inside, lines 142/171-177, elided above/below for brevity)
try
{
    for (var attempt = 0; ; attempt++)
    {
        var candidate = attempt == 0
            ? finalPath
            : Path.Combine(targetDir, $"{timestamp}{suffix}-{attempt}.zip");
        try
        {
            File.Move(tempPath, candidate);
            finalPath = candidate;
            break;
        }
        catch (IOException) when (attempt < 100 && File.Exists(candidate))
        {
            // Candidate pre-exists: try the next suffix. Anything other than a
            // name collision (disk full, vanished target dir, …) escapes to the
            // catch below and is reported as before.
        }
    }
}
catch (Exception ex)
{
    // Same cleanup discipline as the two failure paths above it — the already-verified
    // staging file is not left for CleanStaleTempFiles to find on some later backup.
    TryDelete(tempPath);
    return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
}
```
On the 100th exhausted attempt, the `when` filter's `attempt < 100` term is false, so the
`IOException` is **not** caught by the collision-retry filter and instead propagates to the outer
`catch (Exception ex)` above, producing the exact same message shape
(`"Backup failed for {game.DisplayName}: {ex.Message}"`) as a disk-full or permissions error. This
is REF-04a's defect, confirmed by reading the code — not inferred from the roadmap's description.
The fix needs a branch that recognizes "loop exhausted its retry budget" specifically (e.g. check
`attempt == 100` after the loop, or add an explicit post-loop guard) and returns a message naming
the collision-cap scenario distinctly, before any generic `IOException` wording applies.

### REF-04b — exact site of the missing skip-surfacing (verified, read this session)
```csharp
// Source: src/SavegameBunker/Services/SaveStatusScanner.cs, lines 51-57
DirectoryWalk.ForEachFile(dir, info =>
{
    fileCount++;
    totalBytes += info.Length;
    if (newest is null || info.LastWriteTime > newest)
        newest = info.LastWriteTime;
});
```
Compare to `DirectoryWalk.ForEachFile`'s own signature (verified, read this session):
```csharp
// Source: src/SavegameBunker/Services/DirectoryWalk.cs, lines 41-43
public static void ForEachFile(
    string rootDir, Action<FileInfo> onFile, IReadOnlyCollection<string>? excludedDirs = null,
    Action<string>? onSkipped = null)
```
`SaveStatusScanner.Scan` never passes an `onSkipped` delegate, so every skip inside the scan's own
walk silently no-ops (`DirectoryWalk`'s internal `Skip(string path) =>
onSkipped?.Invoke(...)` — a null-conditional call on a null delegate). `BackupService` passes one
(via `EnumerateRelativeFiles`'s `skipped` parameter forwarding to `ForEachFile`'s `onSkipped`) and
surfaces the result through `DescribeSkippedPathsWarning`. The fix is to (1) pass a collecting
delegate here too, (2) carry the collected paths out of `SaveStatus` (the record currently has no
field for this — `Models/SaveStatus.cs` verified this session has exactly `Kind, FileCount,
TotalBytes, LastModified, Oversized, Error` and no skipped-paths field, so either a new field must
be added to the record or the skip must be surfaced through a separate return channel), and (3)
decide the user-visible surface: `BackupService` appends its warning to the `OperationResult`
message, but `SaveStatusScanner.Scan` returns a bare `SaveStatus`, not an `OperationResult` — there
is no message channel on this type today. The planner must choose where the warning actually
lands (candidates: a new `SaveStatus.SkippedPaths` collection surfaced via `GameRowViewModel`'s
existing Status cell text, or an aggregate warning folded into `MainViewModel.StartStatusScan`'s own
sweep-completion message) — this is a real design decision this research did not find already made
anywhere in the codebase, and REF-04's "the same way backups do" phrasing in
`.planning/REQUIREMENTS.md` does not by itself specify which surface to use.

### Services public surface (full enumeration for REF-05's contract test, verified this session)
```
# grep -n "public (sealed|static) class" src/SavegameBunker/Services/*.cs
BackupComparer (static)      GameCatalog (static)        DialogService (sealed, WPF — needs a fake, not direct call)
DropFolder (static)          ElapsedFormat (static)      CrashReport (static)
HelpPath (static)            IdSlug (static)              KnownGamesScanner (sealed)
KnownGamesCatalog (static)   RestoreService (sealed)      PathResolver (static)
WikiUrl (static)             ShellOpen (static)           SaveStatusScanner (static)
Strings (static)             SizeFormat (static)          BackupService (sealed)
```
Public methods returning `OperationResult` or a `bool`-with-`out` "Try" shape across these classes
(grep, verified this session): `BackupService.Backup` (×2 overloads), `BackupService.TryListBackups`,
`BackupService.DeleteArchive`, `BackupService.TryGetAvailableFreeSpace`,
`GameCatalog.SaveAsync` (×2 overloads), `RestoreService.Restore`,
`DialogService.OpenInEditor`/`OpenHelpGuide`/`OpenHttps`. A reflection-driven contract test should
enumerate every `public` method on every `public` type in the `SavegameBunker.Services` namespace
and either (a) call it with deliberately invalid/hostile arguments (empty paths, nonexistent
directories, locked files via `TestFileSystem`) and assert no exception escapes, or (b) assert by
convention that any method whose return type is neither `OperationResult`/`bool` nor a primitive
query has a documented reason to be exempt. `DialogService` is WPF-bound and already excluded from
direct unit testing elsewhere in the suite (per `TESTING.md`'s "WPF-bound code" gap) — the contract
test should scope itself to the services that are already unit-testable today rather than trying to
instantiate `DialogService` without a `Window`.

## State of the Art

Not applicable — this is a same-codebase refactor with no external ecosystem drift to track. The
.NET 10 / C# 14 toolchain, WPF, and the two pinned NuGet packages are unchanged from the shipped
v1.0 baseline.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | REF-04b's user-visible surface for skipped-scan-paths should be decided by the planner (new `SaveStatus` field vs. a separate aggregate warning), since no existing code path or doc commits to one | Code Examples (REF-04b) | If the planner picks a surface that conflicts with an unstated owner expectation, the fix may need rework after owner review; flag this as a discuss-worthy point even though discuss-phase was skipped for this phase |
| A2 | REF-05's contract test should use `System.Reflection` to enumerate the Services surface rather than a hand-maintained list | Don't Hand-Roll, Validation Architecture | Low risk — reflection is the stated recommendation in both directions (project already does something similar for `ResourceKeyTests`), but the planner could reasonably choose a hand list instead without contradicting any stated requirement |
| A3 | The ~1,000-line post-extraction estimate for `MainViewModel.cs` (before command-wrapper trimming) will not by itself hit REF-02's ≤~700-line target, and additional wrapper-trimming work is needed | Architecture Patterns | If the estimate is off (actual extraction removes more or less than estimated), the plan's line-count checkpoint may need adjustment mid-phase; the line ranges themselves are verified, the arithmetic is an estimate |

## Open Questions

1. **(RESOLVED: see 01-04-PLAN.md decision D-P04-01) Where does the REF-04b skip-warning actually surface to the user?**
   - What we know: `BackupService` appends its warning to the returned `OperationResult.Message`
     string. `SaveStatusScanner.Scan` returns a bare `SaveStatus` record with no message field, and
     is called from inside `MainViewModel.StartStatusScan`'s per-row parallel loop, where the result
     is applied to `GameRowViewModel.ApplyStatus` — there's no single place a sweep's per-row
     outcome today becomes one user-facing sentence the way a single `Backup` call's does.
   - What's unclear: whether the fix should (a) add a `SkippedPaths`/`HasSkippedPaths` field to
     `SaveStatus` and show it per-row (e.g. folding it into `GameRowViewModel.StatusText`/tooltip),
     or (b) aggregate skip counts across the whole sweep into one `MainViewModel.StatusMessage`
     warning after `StartStatusScan` settles, mirroring how a single backup's warning is one string.
   - Recommendation: (a) is more consistent with "Scan's status rows surface skipped unreadable
     paths" (REF-04's own wording names *rows*, plural, implying per-row) — plan for a per-row
     surface first, and treat an aggregate sweep-level dialog as a fallback only if a per-row cell
     has no room for it.

2. **(RESOLVED: see 01-05-PLAN.md decisions D-P05-01/D-P05-02) Does the row-actions coordinator
   absorb `OpenSaveFolderAsync`/`ChangeSaveFolderAsync`/`RemoveGameAsync`, or just
   `BackupAsync`/`RestoreAsync`?**
   - What we know: ROADMAP.md names the extraction target as "row-actions service"; REQUIREMENTS.md
     REF-02 names "per-row actions" without enumerating which of the five per-row command methods
     qualify. `ChangeSaveFolderAsync` and `RemoveGameAsync` already delegate their actual writes to
     `CatalogSession` (not to `GameActions`), so they are architecturally closer to "catalog
     gestures" than "backup/restore actions."
   - What's unclear: whether folding all five into one new coordinator over-widens it relative to
     the "single-responsibility" goal REF-02 states, versus leaving `ChangeSaveFolderAsync`/
     `RemoveGameAsync` on the composition root (they are comparatively short: ~125 combined lines)
     and scoping the new row-actions coordinator to just `BackupAsync`/`RestoreAsync`/
     `OpenSaveFolderAsync`.
   - Recommendation: Scope the new row-actions coordinator to `BackupAsync`/`RestoreAsync`/
     `OpenSaveFolderAsync` (the three that read `row.IsBusy` and a service response, no catalog
     write), and leave `ChangeSaveFolderAsync`/`RemoveGameAsync` on `MainViewModel` since they are
     catalog-session gestures, not backup/restore orchestration — this keeps the three REF-02
     coordinators aligned with the roadmap's own naming ("bulk-backup coordinator," "status-sweep
     coordinator," "row-actions service") without inventing a fourth grouping.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | `dotnet build`/`dotnet test`/`dotnet run` | ✓ | 10.0.404 installed, `global.json` pins 10.0.400 with `rollForward: latestFeature` — compatible | — |
| Windows (for `dotnet test`/`dotnet run`) | Full local test run, WPF runtime | ✓ (this session's environment is Windows 11) | — | — |
| git | Version control, dispatching `test.yml` | ✓ (repo present, git status clean per session context) | — | — |
| GitHub Actions dispatch (`test.yml`) | Each plan's "dispatched test.yml run green" exit gate (per ROADMAP.md) | Not probed this session (requires network/`gh` auth) | — | If `gh`/network access is unavailable mid-execution, the plan's own gate language already treats the Owner's Windows run as "the verification of record," so a missing CI dispatch is a documented fallback, not a blocker |

**Missing dependencies with no fallback:** None identified.

**Missing dependencies with fallback:** GitHub Actions dispatch access — not verified this session;
falls back to the Owner's local Windows `dotnet test` run per the phase's own stated gate.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`[VERIFIED: tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj]`) |
| Config file | none — no `xunit.runner.json` committed |
| Quick run command | `dotnet test tests/SavegameBunker.Tests --filter "FullyQualifiedName~<Subject>Tests"` |
| Full suite command | `dotnet test SavegameBunker.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REF-01 | Full suite passes unchanged after every extraction | regression (full suite) | `dotnet test SavegameBunker.sln` | ✅ (existing suite, 555 `[Fact]`/`[Theory]` attributes counted this session — `[VERIFIED: grep count this session]`; the stated "629 tests" figure in STATE.md/ROADMAP.md is the *runtime* test-case count once `[Theory]`/`[InlineData]` rows multiply out, which is consistent with, not contradicted by, the 555 attribute count) |
| REF-02 | `MainViewModel.cs` ≤~700 lines; 3 sealed coordinators extracted | structural check | `wc -l src/SavegameBunker/ViewModels/MainViewModel.cs` (manual gate, not a unit test) + new coordinator unit tests | ❌ Wave 0 — new test files needed per coordinator (naming convention: `<Subject>Tests.cs`, e.g. `BulkBackupCoordinatorTests.cs`) |
| REF-03 | `IsAnyRowChecking`/reset-to-Checking guard preserved; OnUi/IProgress pattern uniform | unit + integration | Existing `MainViewModelTests.cs` races/guard tests should continue to pass unchanged; add new coordinator-level tests for the same guard if logic moves into a class the existing tests can no longer reach directly | Existing tests ✅; new coordinator-level tests ❌ Wave 0 if the guard logic becomes unreachable from `MainViewModel`'s own public surface |
| REF-04 (rename cap) | 100-collision message distinguishable from disk-error message | unit | `dotnet test --filter "FullyQualifiedName~BackupServiceTests"` | ❌ Wave 0 — no existing test creates 100 colliding filenames; `Backup_survives_a_final_rename_collision_by_suffixing_the_archive` (lines 516-540) only covers one collision |
| REF-04 (scan skip) | Scan surfaces skipped unreadable paths | unit | `dotnet test --filter "FullyQualifiedName~SaveStatusScannerTests"` | ❌ Wave 0 — `SaveStatusScannerTests.cs` (173 lines, 8 facts) has no test for an unreadable subtree/file today (verified by grep this session returning no "skip"/"unreadable" matches in that file) |
| REF-05 | Every public Services entry point returns failure/warning, never throws | contract test | New `dotnet test --filter "FullyQualifiedName~ServiceContract"` or similar | ❌ Wave 0 — no contract test file exists in `tests/SavegameBunker.Tests/` today (verified by directory listing this session) |

### Sampling Rate
- **Per task commit:** the filtered single-subject command for whatever file(s) that commit touched
- **Per wave merge:** `dotnet test SavegameBunker.sln` (full suite — matches the phase's own "Linux
  Release build 0/0 + dispatched test.yml green" gate language, understood as "Linux build" for the
  compile-only check and the actual Windows-based `test.yml`/Owner run for the real test pass)
- **Phase gate:** Full suite green on the Owner's Windows run before the phase is considered closed,
  per ROADMAP.md's own stated verification-of-record language

### Wave 0 Gaps
- [ ] A new test proving the ≥100-collision rename-retry path produces a distinguishable message
      (REF-04a) — extend `BackupServiceTests.cs`
- [ ] A new test proving `SaveStatusScanner.Scan`/`StartStatusScan` surfaces a skipped unreadable
      path somewhere user-visible (REF-04b) — extend `SaveStatusScannerTests.cs` and/or
      `MainViewModelTests.cs` depending on which surface (Open Question 1) the plan picks
- [ ] A new contract-test file enumerating the Services surface via reflection and asserting no
      public method throws across its boundary on hostile input (REF-05)
- [ ] New unit test file(s) for each extracted coordinator (bulk-backup, status-sweep, row-actions)
      — the exact file names depend on the coordinator class names the plan chooses, following the
      existing `<Subject>Tests.cs` convention (e.g. `CatalogSessionTests.cs` is the closest existing
      precedent for a non-WPF `internal sealed` collaborator's own dedicated test file)

## Security Domain

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Local-only desktop app, no auth surface (`ARCHITECTURE.md` confirms) |
| V3 Session Management | No | Not applicable |
| V4 Access Control | No | Not applicable — no multi-user/permission model |
| V5 Input Validation | Partial, unchanged by this phase | `games.json` catalog validation already lives in `GameCatalog.LoadAsync`/`IdSlug`/`PathResolver`; this phase does not add new user input surfaces (no new dialog, no new catalog field) |
| V6 Cryptography | Partial, unchanged | `BackupService` already uses `SHA256`/`IncrementalHash` for archive-content verification (pre-existing, not touched by REF-01..05) |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path-template-driven over-broad backup/restore (`CONCERNS.md`'s "Path template expansion trusts the catalog") | Tampering | Pre-existing, out of this phase's scope — `BackupService.IsSameOrInside` guard already exists and is unchanged by REF-01..05 |
| A new contract test (REF-05) that calls Services with "hostile" input (e.g. deeply nested paths, reparse points) | — | Reuse `tests/SavegameBunker.Tests/TestFileSystem.cs`'s existing ACL-deny/junction helpers rather than writing new filesystem-attack fixtures — this phase introduces no new threat surface, only verifies an existing defensive contract |

This phase introduces no new attack surface: it moves code between C# classes and tightens two
error messages. No new ASVS gap was identified beyond what `CONCERNS.md` already documents as
pre-existing and out of scope for REF-01..05.

## Sources

### Primary (HIGH confidence — all read directly this session)
- `src/SavegameBunker/ViewModels/MainViewModel.cs` (full 1,886 lines)
- `src/SavegameBunker/ViewModels/GameActions.cs`, `CatalogSession.cs`, `GameRowViewModel.cs`
- `src/SavegameBunker/Services/BackupService.cs`, `SaveStatusScanner.cs`, `DirectoryWalk.cs`,
  `RestoreService.cs`, `IDialogService.cs`
- `src/SavegameBunker/Models/OperationResult.cs`, `SaveStatus.cs`
- `tests/SavegameBunker.Tests/MainViewModelTests.cs` (header/fakes), `BackupServiceTests.cs`
  (rename-collision test), `SaveStatusScannerTests.cs` (grep, no skip-path test found)
- `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/ROADMAP.md`, `.planning/config.json`
- `.planning/codebase/ARCHITECTURE.md`, `CONCERNS.md`, `STACK.md`, `CONVENTIONS.md`, `TESTING.md`
- `CLAUDE.md`
- `src/SavegameBunker/SavegameBunker.csproj`, `global.json` (version pins re-confirmed via
  `dotnet --version` this session)
- `git log` (confirmed the A1-A12 audit-fix commit sequence that produced the current
  `BackupService`/`RestoreService` shape; no "R2" literal label found in commit history — R1/R2 are
  the project's informal round labels, not commit-message tags)

### Secondary (MEDIUM confidence)
- None used — every claim in this document traces to a file read or command run this session.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, existing pins directly re-verified
- Architecture: HIGH — every line range and code example quoted above was read from the actual
  current file this session, not inferred from the roadmap's description
- Pitfalls: HIGH — derived directly from reading the exact threading-sensitive code (`Progress<T>`
  construction sites, `OnUi`, the re-entrancy reset sequence), not from general WPF/MVVM knowledge
- Open Questions: MEDIUM — REF-04b's exact user-visible surface and the row-actions coordinator's
  precise scope are genuinely underspecified in the upstream requirements/roadmap text, not a gap
  in this research

**Research date:** 2026-11-17
**Valid until:** No external drift risk (closed-world refactor of the current codebase state) —
valid until the codebase itself changes; re-read the three files above before planning if any
commit lands between this research and `/gsd-plan-phase 1`.
