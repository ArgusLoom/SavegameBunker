# Phase 1: Bugfixing & Refactoring - Pattern Map

**Mapped:** 2026-11-17
**Files analyzed:** 9 (3 new coordinators + 1 new contract test + 2 modified services + 2 new/extended regression tests + 1 composition-root trim)
**Analogs found:** 9 / 9 (every file has a same-codebase analog; no "no analog" bucket needed — RESEARCH.md confirms this is a closed-world refactor)

No CONTEXT.md exists for this phase (discuss-phase was skipped by owner decision). File list and
classification below is derived entirely from `01-RESEARCH.md`'s "Architecture Patterns" line-range
table and "Validation Architecture" test map, cross-checked against the actual tracked source files.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/SavegameBunker/ViewModels/BulkBackupCoordinator.cs` (name per planner) | service (`internal sealed` VM collaborator) | batch (loop over rows, per-item progress) | `src/SavegameBunker/ViewModels/GameActions.cs` | exact — same tier, same "plain class + callbacks" shape |
| `src/SavegameBunker/ViewModels/StatusSweepCoordinator.cs` (name per planner) | service (`internal sealed` VM collaborator) | batch / event-driven (bounded-concurrency scan, per-row token) | `src/SavegameBunker/ViewModels/GameActions.cs` | exact |
| `src/SavegameBunker/ViewModels/RowActionsCoordinator.cs` (name per planner) | service (`internal sealed` VM collaborator) | request-response (single-row backup/restore/open-folder) | `src/SavegameBunker/ViewModels/GameActions.cs` | exact — GameActions is literally the row-actions precedent for Backup/Restore; this split narrows scope |
| `src/SavegameBunker/ViewModels/MainViewModel.cs` (trimmed composition root) | controller/provider (view model) | request-response (thin `[RelayCommand]` forwarders) | itself, pre-extraction (`AddGameAsync() => AddGameCoreAsync(...)` wrapper shape) | exact — self-analog, no external file needed |
| `src/SavegameBunker/Services/BackupService.cs` (REF-04a edit, lines ~152-177) | service | request-response (narrow catch-branch addition) | same file's existing `catch (IOException) when (...)` filter immediately above the edit site | exact — modify in place, do not restructure |
| `src/SavegameBunker/Services/SaveStatusScanner.cs` (REF-04b edit, lines 37-68) + `Models/SaveStatus.cs` (field addition) | service + model | transform (directory walk → record) | `src/SavegameBunker/Services/BackupService.cs`'s `DescribeSkippedPathsWarning`/`skippedPaths` wiring (lines ~183-207) | exact — same mechanism (`DirectoryWalk.onSkipped`), different record shape to carry it |
| `tests/SavegameBunker.Tests/BulkBackupCoordinatorTests.cs` / `StatusSweepCoordinatorTests.cs` / `RowActionsCoordinatorTests.cs` (names per planner) | test | — | `tests/SavegameBunker.Tests/CatalogSessionTests.cs` | exact — closest existing dedicated test file for a non-WPF `internal sealed` VM collaborator |
| `tests/SavegameBunker.Tests/BackupServiceTests.cs` (extended, new `[Fact]` for ≥100-collision) | test | — | same file's existing `Backup_survives_a_final_rename_collision_by_suffixing_the_archive` (lines 516-540) | exact |
| `tests/SavegameBunker.Tests/SaveStatusScannerTests.cs` (extended, new skip-path `[Fact]`) | test | — | same file's existing `[Fact]`s (8 facts, 173 lines) | exact |
| `tests/SavegameBunker.Tests/ServiceContractTests.cs` (new, REF-05) | test | — | none existing in-repo (new category) — structurally nearest is `ResourceKeyTests`/`StringsTests` (reflection-driven enumeration pattern per RESEARCH.md's "Don't Hand-Roll") | role-match only — no prior Services-wide contract test exists; follow the reflection-enumeration shape those two tests use, not a Services-specific precedent |

## Pattern Assignments

### New coordinators (`BulkBackupCoordinator`, `StatusSweepCoordinator`, `RowActionsCoordinator`)

**Analog:** `src/SavegameBunker/ViewModels/GameActions.cs` (full file, 30-224; also
`CatalogSession.cs` for the "own no observable state, resolve-and-save shape" half of the
convention, if any coordinator needs a request/response write — it should not; writes stay on
`CatalogSession`).

**Imports pattern** (`GameActions.cs` lines 1-7):
```csharp
using System.Globalization;
using System.Linq;
using SavegameBunker.Converters;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;
```
No `CommunityToolkit.Mvvm` imports — coordinators never derive from `ObservableObject`.

**Class shape / ctor pattern** (`GameActions.cs` lines 30-41):
```csharp
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
```
Copy exactly: `internal sealed`, constructor-injected dependencies only, no `[ObservableProperty]`,
no WPF type in any signature. Doc-comment the class with the same two-paragraph shape (what it
owns / why `internal` + private-field-only, never public) — see `GameActions.cs` lines 9-29 and
`CatalogSession.cs` lines 8-20 for the two existing templates to copy prose structure from.

**Core pattern — accept `IProgress<T>`, never construct it** (`GameActions.cs` lines 43-54):
```csharp
public Task<OperationResult> BackupAsync(GameRowViewModel row, string backupRoot, IProgress<long>? progress = null) =>
    Task.Run(() => _backup.Backup(row.Game, backupRoot, progress));
```
Every new coordinator method that reports progress must take `IProgress<T>?` as a parameter with
default `null`, never `new Progress<T>(...)` internally. The caller (`MainViewModel`) builds it. See
RESEARCH.md Pitfall 1 and Pattern 2 — this is the single most load-bearing rule for this phase.

**Caller-side construction site to preserve verbatim** (`MainViewModel.cs` lines 1268-1273, single-row
backup — same shape repeats at lines 1346-1352 for restore and 1568-1580/1577-1580 for bulk):
```csharp
// Constructed here, on the UI thread, so its callbacks marshal back onto the
// dispatcher rather than arriving on the Task.Run thread inside GameActions.BackupAsync
// — the same reason RestoreAsync constructs its own Progress<int> here.
var progress = new Progress<long>(written => { if (row.BackupBytesTotal > 0) row.BackupBytesWritten = written; });

var result = await _gameActions.BackupAsync(row, root, progress);
```
When `BackupAsync`/`RestoreAsync` move into `RowActionsCoordinator`, this `Progress<T>` construction
stays in `MainViewModel`'s thin wrapper method, not inside the coordinator.

**Bulk-backup's `OnUi()` wrapping pattern** (`MainViewModel.cs` lines 1599-1625, `RunBulkBackupAsync`):
```csharp
var size = _backup.ComputeSaveDataSize(row.Game);
OnUi(() =>
{
    row.BackupBytesTotal = size;
    row.BackupBytesWritten = 0;
});
...
OnUi(() => row.RestoredArchiveTakenAt = null);
```
Every write to a bound `GameRowViewModel` property from inside the bulk loop's `Task.Run` body goes
through `OnUi()`. If `BulkBackupCoordinator` takes over the loop body, it must receive `OnUi` as a
passed-in `Action<Action>` delegate (RESEARCH.md Pattern 3, option (b)) rather than reimplementing
dispatcher-checking itself — `OnUi`'s own definition (lines 1086-1094) should stay on
`MainViewModel` and be handed down as a delegate parameter.

**Re-entrancy reset sequence to preserve literally, in order** (`MainViewModel.cs` lines 1552-1566):
```csharp
foreach (var row in targets)
    row.ApplyBackupComparison(BackupComparison.Checking());

BulkBackupTotal = targets.Count;
BulkBackupCurrent = 0;
BulkBackupCurrentGameName = targets[0].DisplayName;

foreach (var row in targets)
    row.IsBusy = true;
```
This ordering — comparison reset, then counters, then `IsBusy` — is the re-entrancy guard itself
(RESEARCH.md Pitfall 2), not incidental. If `BulkBackupCoordinator` owns this loop, it must run this
exact sequence before any `await`/`Task.Run` yields control back to the UI thread.

**`finally`-block contract to preserve verbatim** (`MainViewModel.cs` lines 1629-1643):
```csharp
finally
{
    foreach (var row in targets)
    {
        row.IsBusy = false;
        row.BackupBytesTotal = 0;
        row.BackupBytesWritten = 0;
    }

    BulkBackupTotal = 0;
    BulkBackupCurrent = 0;
    BulkBackupCurrentGameName = null;

    StartStatusScan(only: targets);
}
```
RESEARCH.md's Anti-Patterns section names this exact block as the one that must not be reordered or
abbreviated — it is the only thing preventing a mid-loop exception from leaving rows permanently
busy.

**Thin composition-root wrapper shape** (existing precedent, `MainViewModel.cs`, referenced in
RESEARCH.md line 123):
```csharp
private Task AddGameAsync() => AddGameCoreAsync(initialSaveFolder: null);
```
Every `[RelayCommand]`-decorated method on `MainViewModel` that delegates to a new coordinator
should shrink to this one-line-forward shape. Keep the `[RelayCommand(CanExecute = nameof(...))]`
attribute, the generated command property name, and the `Can*` predicate's boolean logic
byte-for-byte identical (RESEARCH.md Pitfall 3) — `MainViewModelTests.cs` drives everything through
these generated command properties and must not need to change.

**Status-sweep specifics** (`MainViewModel.cs` lines 1056-1145, `StartStatusScan`):
Per-row scan token bumping (`++row.ScanToken`), the `Checking()` reset applied to both
`SaveStatus`/`BackupComparison` at once, and the single reachable/root read before the loop (not
per-row) are the three details `StatusSweepCoordinator` must reproduce exactly — see lines
1110-1145 for the full snapshot-and-reset sequence to copy verbatim.

---

### `src/SavegameBunker/Services/BackupService.cs` — REF-04a (rename-retry cap)

**Analog:** the method's own existing structure, lines 152-193.

**Current code to extend, not restructure** (verified lines 152-193):
```csharp
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
Per RESEARCH.md Pitfall 4: add a dedicated branch that fires only when the loop's own retry budget
is exhausted (`attempt == 100` reached via the `when` filter going false), returning a distinct
`OperationResult.Fail` message before falling into the existing generic `catch (Exception ex)` —
do not wrap the whole loop in a new try/catch, do not loosen the `when` filter.

**Error-message formatting convention to match** (same file, line 176/191):
```csharp
return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
```
A new "cap exceeded" message should follow the same `"Backup failed for {game.DisplayName}: ..."`
prefix convention so both message shapes read consistently in the UI, while differing enough in
the trailing clause to be distinguishable by the new regression test.

---

### `src/SavegameBunker/Services/SaveStatusScanner.cs` + `Models/SaveStatus.cs` — REF-04b (skip surfacing)

**Analog for the mechanism:** `DirectoryWalk.ForEachFile`'s `onSkipped` parameter, already consumed
by `BackupService`; **analog for the record-carrying + user-facing wiring:**
`BackupService.cs`'s `skippedPaths`/`DescribeSkippedPathsWarning` pattern (lines ~183-207).

**Current scan call, missing the delegate** (`SaveStatusScanner.cs` lines 37-68, full method):
```csharp
public static SaveStatus Scan(Game game)
{
    try
    {
        if (!PathResolver.TryResolve(game.SaveDirTemplate, null, out var dir, out var error))
            return SaveStatus.PathError(error ?? "unresolved save folder");

        if (!Directory.Exists(dir))
            return SaveStatus.NoSaves();

        long fileCount = 0;
        long totalBytes = 0;
        DateTime? newest = null;

        DirectoryWalk.ForEachFile(dir, info =>
        {
            fileCount++;
            totalBytes += info.Length;
            if (newest is null || info.LastWriteTime > newest)
                newest = info.LastWriteTime;
        });

        if (fileCount == 0)
            return SaveStatus.NoSaves();

        return SaveStatus.Found(fileCount, totalBytes, newest, IsOversized(fileCount, totalBytes));
    }
    catch (Exception ex)
    {
        return SaveStatus.PathError(ex.Message);
    }
}
```
`DirectoryWalk.ForEachFile`'s signature (`DirectoryWalk.cs` lines 41-43, already quoted in
RESEARCH.md) takes a fourth optional `Action<string>? onSkipped` — pass a collecting delegate here
(`var skipped = new List<string>(); ... DirectoryWalk.ForEachFile(dir, onFile, onSkipped: skipped.Add);`)
matching how `BackupService` collects into its own `skippedPaths` list before this point in its
own walk.

**`SaveStatus` record — add a field, following the existing record + static-factory shape**
(`Models/SaveStatus.cs`, full file, lines 32-51):
```csharp
public sealed record SaveStatus(
    SaveStatusKind Kind,
    long FileCount,
    long TotalBytes,
    DateTime? LastModified,
    bool Oversized,
    string? Error)
{
    public static SaveStatus Checking() =>
        new(SaveStatusKind.Checking, 0, 0, null, false, null);

    public static SaveStatus Found(long fileCount, long totalBytes, DateTime? lastModified, bool oversized) =>
        new(SaveStatusKind.Found, fileCount, totalBytes, lastModified, oversized, null);
    ...
}
```
Per RESEARCH.md's Open Question 1 recommendation, add a `SkippedPaths`/`HasSkippedPaths`-shaped
field to this record (positional record — appending a new parameter requires updating every static
factory call site above, all four of them) and surface it per-row via `GameRowViewModel`'s existing
Status cell/tooltip rather than an aggregate sweep-level message — mirroring `BackupService`'s own
"name up to five skipped paths" convention below, scaled to a shorter tooltip string.

**Warning-text-composition convention to mirror in the new per-row surface**
(`BackupService.cs` lines 195-207):
```csharp
private static string DescribeSkippedPathsWarning(IReadOnlyCollection<string> skippedPaths)
{
    var shown = string.Join(", ", skippedPaths.Take(5));
    if (skippedPaths.Count > 5)
        shown += $", and {skippedPaths.Count - 5} more";
    return $" Warning: {skippedPaths.Count} path(s) could not be read and are NOT in this backup: {shown}";
}
```
Reuse this "take 5, count the rest" shape for whatever string the per-row surface composes, so the
two skip-warning surfaces (backup, scan) read consistently rather than inventing a second format.

---

### `tests/SavegameBunker.Tests/*CoordinatorTests.cs` (new, one per coordinator)

**Analog:** `tests/SavegameBunker.Tests/CatalogSessionTests.cs` (full file header, lines 1-58).

**Fixture shape to copy** (lines 1-35):
```csharp
using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

public sealed class CatalogSessionTests : IDisposable
{
    private readonly string _dir;

    public CatalogSessionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp directory must not fail the test run */ }
    }
```
Construct the coordinator directly (it is `internal`, same-assembly test project reaches it fine),
no mocking library available — use the project's existing fakes (`TestFileSystem`,
`IDialogService` test double) the same way `MainViewModelTests.cs`/`CatalogSessionTests.cs` already
do, rather than introducing a new fake type per coordinator unless one does not already exist for
the exact dependency shape needed.

---

### `tests/SavegameBunker.Tests/ServiceContractTests.cs` (new, REF-05)

**No direct in-repo analog for a Services-wide contract test.** Follow the reflection-enumeration
shape RESEARCH.md's "Don't Hand-Roll" section names (`ResourceKeyTests`/`StringsTests` — not read
this session, but named by RESEARCH.md as the existing precedent for "enumerate a surface via
reflection rather than a hand list"). Enumerate via:
```csharp
typeof(BackupService).Assembly.GetTypes()
    .Where(t => t.Namespace == "SavegameBunker.Services" && t.IsPublic)
```
then every `public` method on each type, excluding `DialogService` (WPF-bound, already excluded
elsewhere per `TESTING.md`'s "WPF-bound code" gap — see RESEARCH.md's Code Examples section for the
full public-surface enumeration list to iterate).

---

## Shared Patterns

### No-observable-state collaborator (applies to all three new coordinators)
**Source:** `src/SavegameBunker/ViewModels/GameActions.cs` lines 30-41, `CatalogSession.cs` lines
21-30
**Apply to:** `BulkBackupCoordinator`, `StatusSweepCoordinator`, `RowActionsCoordinator`
```csharp
internal sealed class GameActions
{
    private readonly BackupService _backup;
    ...
    public GameActions(BackupService backup, IDialogService dialog) { ... }
```
`internal sealed`, constructor injection only, held as a `private readonly` field on
`MainViewModel`, never a `public` property, never derives from `ObservableObject`.

### `Progress<T>` constructed on the UI thread, never inside a coordinator
**Source:** `MainViewModel.cs` lines 1268-1273, 1346-1352, 1568-1580
**Apply to:** Every coordinator method that reports progress
```csharp
var progress = new Progress<long>(written => { if (row.BackupBytesTotal > 0) row.BackupBytesWritten = written; });
var result = await _gameActions.BackupAsync(row, root, progress);
```
This is RESEARCH.md's single highest-severity pitfall (Pitfall 1) — the planner must call this out
explicitly in every plan touching `BulkBackupCoordinator`/`RowActionsCoordinator`.

### `OnUi()` marshalling for any background write to a bound row property
**Source:** `MainViewModel.cs` lines 1086-1094 (definition), 1599-1625 (usage)
**Apply to:** `BulkBackupCoordinator`, `StatusSweepCoordinator` (anywhere a `Task.Run` body writes
to `GameRowViewModel`)
```csharp
private static void OnUi(Action action)
{
    var dispatcher = System.Windows.Application.Current?.Dispatcher;
    if (dispatcher is null || dispatcher.CheckAccess())
        action();
    else
        dispatcher.Invoke(action);
}
```
Keep `OnUi` itself on `MainViewModel`; pass it down as an `Action<Action>` delegate parameter if a
coordinator needs it, per RESEARCH.md Pattern 3 option (b).

### Never-throw-across-boundary / `OperationResult` error contract
**Source:** `BackupService.cs` lines 171-177, 189-192; `SaveStatusScanner.cs` lines 64-67
**Apply to:** Any coordinator or service edit in this phase
```csharp
catch (Exception ex)
{
    return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
}
```
and for a bare-value return type (no `OperationResult` channel):
```csharp
catch (Exception ex)
{
    return SaveStatus.PathError(ex.Message);
}
```
REF-05's contract test verifies this holds across every public Services method; new/modified code
in this phase must not introduce an exception that escapes a public boundary.

## No Analog Found

None — every file in this phase's scope has at least a role-match analog in the current
codebase (confirmed above). This matches RESEARCH.md's framing of the phase as a closed-world
refactor with no external-ecosystem unknowns.

## Metadata

**Analog search scope:** `src/SavegameBunker/ViewModels/`, `src/SavegameBunker/Services/`,
`src/SavegameBunker/Models/`, `tests/SavegameBunker.Tests/`
**Files scanned:** `MainViewModel.cs` (targeted ranges: 1-70, 1056-1145, 1241-1390, 1513-1663),
`GameActions.cs` (full), `CatalogSession.cs` (full), `BackupService.cs` (targeted: 152-220),
`SaveStatusScanner.cs` (full), `Models/SaveStatus.cs` (full), `CatalogSessionTests.cs` (header,
lines 1-60); `DirectoryWalk.cs` signature taken from RESEARCH.md's already-verified quote
(lines 41-43) rather than re-read, since that range was already in context via RESEARCH.md
**Pattern extraction date:** 2026-11-17
