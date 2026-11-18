---
last_mapped_commit: 02dc0e339858df545077056bf299b0007a8d4dfd
last_mapped_at: 2026-11-18
---
# Testing Patterns

**Analysis Date:** 2026-11-18

## Test Framework

**Runner:**
- xUnit 2.9.3 (`xunit` package) with `xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk` 18.12.0
- Config: `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`. No separate `xunit.runner.json` is committed.
- Target: `net10.0-windows` with `<UseWPF>true</UseWPF>`. The suite runs on Windows only.
- Implicit usings are enabled and `Xunit` is added as a global `<Using Include="Xunit" />`, so test files do not declare `using Xunit;`.

**Assertion library:**
- xUnit's built-in `Assert` class only. No FluentAssertions, Shouldly, or similar package is referenced.
- Common calls: `Assert.True`, `Assert.Equal`, `Assert.Single`, `Assert.NotNull`, `Assert.Throws<T>`, `Assert.ThrowsAsync<T>`. Pass the failure message as the second argument when the result carries one: `Assert.True(result.Success, result.Message);`

**Run commands:**
```powershell
dotnet test SavegameBunker.sln                               # Run the full suite (Windows)
dotnet test SavegameBunker.sln -c Release --no-build        # What CI runs after a Release build
dotnet test tests/SavegameBunker.Tests --filter "FullyQualifiedName~BackupServiceTests"   # One class
dotnet build SavegameBunker.sln                              # Compile check; works on Linux/macOS too (EnableWindowsTargeting)
```
- No watch mode and no coverage tooling is configured. A coverage run would need an external collector.

**CI:** `.github/workflows/test.yml` builds and tests on `windows-latest` for every pull request to `main`, on `workflow_dispatch`, and via `workflow_call` from `release.yml`. The SDK comes from `global.json`. NuGet packages are cached by the hash of the csproj and `Directory.Build.props`.

## Test File Organization

**Location:**
- All tests live in one separate project: `tests/SavegameBunker.Tests/`. Tests are not co-located with source.
- The namespace is `SavegameBunker.Tests` for every file.

**Naming:**
- One test class per production type, named `<Type>Tests.cs`: `BackupServiceTests.cs` tests `src/SavegameBunker/Services/BackupService.cs`; `MainViewModelTests.cs` tests `MainViewModel.cs`.
- Some classes split by concern when one type grows: `MainViewModelGuardPinTests.cs`, `StartupUnsecuredPromptTests.cs`, `AddGameFlowTests.cs` alongside `AddGameDialogViewModelTests.cs`.
- Shared helpers have their own file: `TestFileSystem.cs`, the `RecordingDialogService` and `ThrowingDialogService` fakes.
- `ServiceContractTests.cs` and `ResourceKeyTests.cs` check cross-cutting rules rather than one class.

**Structure:**
```
tests/SavegameBunker.Tests/
├── <Type>Tests.cs           # one per production type, sealed
├── TestFileSystem.cs        # shared ACL/junction helpers (internal static)
├── RecordingDialogService.cs or inline fakes   # IDialogService doubles
└── SavegameBunker.Tests.csproj
```

## Test Structure

**Class shape:**
- Test classes are `public sealed class`, per the project convention. Helper classes that hold state use `private sealed class`.
- Classes that need per-test setup and teardown implement `IDisposable` and create a unique temp folder in the constructor. `BackupServiceTests` is the model:

```csharp
// tests/SavegameBunker.Tests/BackupServiceTests.cs
public sealed class BackupServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly BackupService _service = new();

    public BackupServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            TestFileSystem.RemoveDenyRules(_dir);
            TestFileSystem.RemoveJunctions(_dir);
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException) { /* A leftover temp directory must not fail the test run. */ }
        catch (UnauthorizedAccessException) { /* Same. */ }
    }
}
```

**Test naming:**
- Test method names are full sentences in snake_case, describing behaviour rather than method name: `Backup_writes_to_the_configured_folder_on_an_ordinary_fixed_volume`, `Refresh_and_change_backup_folder_are_disabled_while_a_backup_runs`, `Every_SavegameBunker_owned_palette_resource_referenced_in_XAML_is_also_defined`
- Prefix with the subject under test (`Backup_`, `Restore_`, `ListBackups_`) when a class covers one service
- Keep the snake_case convention in new tests. Do not switch to `Should...` or `MethodName_State_Expected` style.

**Setup helpers:** private factory methods build fixtures on demand, named for the scenario they produce:
- `GameWithSaves(fileName, content)` creates a save folder with one file
- `GameWithEmptySaveDir()` creates an existing but empty folder
- `GameWithNestedSaves()` seeds nested content so archive structure can be pinned
- `SeedArchiveFile(backupRoot, gameId, fileName, bytes)` writes an archive by name, bypassing `Backup`, when only the listing rules are under test

**Fact vs Theory:** about 595 `[Fact]` and 19 `[Theory]` attributes. Use `[Theory]` with `[InlineData]` for boundary tables, for example the size thresholds in `SaveStatusScannerTests.cs` (line 150): `[InlineData(20_001, 0, true)]`.

**Patterns (one assertion focus per test):**
- Arrange the state on disk or through a fake, act once, then assert on the returned `OperationResult` and on the filesystem
- Assert the on-disk outcome, not only the return value: check that the archive exists in the folder and is readable (`Directory.GetFiles(..., "*.zip")` followed by `Assert.Single`)

## Async Testing

**Pattern:** tests that use `async` await the subject directly. Do not block with `.Result` or `.Wait()`.

**Settling state:** the test host has no `SynchronizationContext`, so `Progress<T>` reports arrive on the thread pool and can land late. Assert settled state with a polling helper, not a snapshot taken right after a command finishes:

```csharp
// tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs (line 58) and MainViewModelTests.cs (line 176)
private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
{
    var deadline = Environment.TickCount64 + timeoutMs;
    while (!condition() && Environment.TickCount64 < deadline)
        await Task.Delay(5);
}

await WaitForAsync(() => vm.ResolvedStatus == "42 files, 3.1 MB");
```

- Copies of `WaitForAsync` exist in more than one test file. Move a shared version into a helper if a third copy appears.
- Use a `Task`-returning test method. `async void` is not used.

**Exceptions from async code:** `Assert.ThrowsAsync<KeyNotFoundException>(() => coordinator.BackupAsync(...))` in `BulkBackupCoordinatorTests.cs`.

## Mocking

**Framework:** none. There is no Moq, NSubstitute, or FakeItEasy reference. Test doubles are hand-written classes that implement the production interface.

**Dialog doubles:** `IDialogService` is the seam for every UI interaction. Two doubles are in use:

- `RecordingDialogService` (in `tests/SavegameBunker.Tests/MainViewModelTests.cs`) records messages, returns scripted values, and exposes an `OnReport` hook to observe state mid-operation:
  - `ConfirmToReturn` for a fixed answer, or `ConfirmReturnSequence` (a `Queue<bool>`) for a sequence
  - `FolderToReturn` for `PickFolder`
  - `AddGameToReturn` for `ShowAddGameDialog`
- `ThrowingDialogService` (in `tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs`) throws `InvalidOperationException` from every method. Use it when a test must prove the view model did not show a dialog.

**Host-owned fake:** `FakeRowActionHost : IRowActionHost` is a `private sealed class` nested inside `RowActionsCoordinatorTests.cs` (line 36).

**What to Mock:**
- Anything that would show UI: implement `IDialogService` by hand
- Interactions with the outside world that a test cannot reproduce on a developer machine, such as a locked folder or a junction

**What NOT to Mock:**
- Filesystem. Tests create real temp folders under `Path.GetTempPath()\SavegameBunkerTests\<guid>` and clean them up in `Dispose`. `BackupService`, `RestoreService`, and `GameCatalog` are exercised directly.
- Real OS locations. `KnownFolders` is `internal` and is asserted against its real value, not mocked (`SavegameBunker.csproj` InternalsVisibleTo comment)
- `Strings`. Tests load the real `Themes/Strings.en.xaml` through the embedded-resource fallback. Assert on the real text or on the key lookup result.

**Access to privates:** prefer public seams. `InternalsVisibleTo("SavegameBunker.Tests")` covers the internal seams that tests need. Do not use reflection.

## Fixtures and Factories

**Test data:**
- Build `Game` records inline with object initialisers. Use stable ids such as `Id = "test-game"` and `DisplayName = "Test Game"`
- Save paths are built from the temp directory and converted to forward slashes before they reach the model: `SaveDirTemplate = saveDir.Replace('\\', '/')`. This matches how the catalog stores paths

**Filesystem helpers (`tests/SavegameBunker.Tests/TestFileSystem.cs`):**
- `TryDenyAccess(path)` applies a Deny ACE to simulate a locked subfolder. It verifies the deny rule takes effect before returning true. It returns false on environments where the ACL cannot be set, and tests must then skip their assertion rather than fail
- `RemoveDenyRules(dir)` lifts Deny ACEs level by level, so the folder can be listed and deleted
- `RemoveJunctions(dir)` unlinks reparse points without descending into them

**Location:** all fixtures are built inside each test class. There is no shared fixture folder and no `[ClassData]` or `[MemberData]` provider.

## Coverage

**Requirements:** None enforced. No coverage threshold, collector, or report is configured in CI.

**Coverage approach:** the suite checks behaviour through named scenarios, and the important rules are pinned by name. Examples: `Backup_accepts_a_destination_on_the_save_data_own_volume` pins the no-volume-restriction rule from SPEC; `ResourceKeyTests` guards against an undefined `DynamicResource` silently rendering the default colour.

**View Coverage:** not configured. Adding a collector would be a new test-only package, which the hard constraints permit.

## Test Types

**Unit Tests:**
- Most of the suite. Services are tested directly against real temp folders. View models are tested with hand-written dialog doubles.
- Example: `BackupServiceTests.cs`, `RestoreServiceTests.cs`, `BackupComparerTests.cs`, `ElapsedFormatTests.cs`

**Integration Tests:**
- Filesystem-backed tests are the nearest equivalent. They exercise the real `ZipArchive`, `File.Replace`, and directory renames.
- `MainViewModelTests.cs` builds a `MainViewModel` from a saved catalog on disk (`CreateViewModelWithSavedCatalogAsync`), so it crosses `GameCatalog`, `CatalogSession`, and the coordinators.
- `KnownGamesScannerTests.cs` and `SaveStatusScannerTests.cs` run against real folder trees.

**Contract tests:**
- `ServiceContractTests.cs` checks the error contract: `The_public_service_surface_is_exactly_the_cases_plus_the_documented_exemptions` and `Every_public_service_method_returns_instead_of_throwing_on_hostile_input`.
- `ResourceKeyTests.cs` scans every `.xaml` file under `src/SavegameBunker` to confirm each `DynamicResource` palette key is defined. It locates the source folder by walking up from `AppContext.BaseDirectory`.
- `StringsTests.cs` checks lookup behaviour: an unknown key throws with the key name, concurrent first access resolves every key, and toolbar mnemonics are distinct.
- `WikiUrlTests.cs` and `HelpPathTests.cs` pin help-link rules.

**E2E Tests:** not used. The WPF UI is not driven by automation. Manual walkthroughs are part of the release process, per the comments in `ResourceKeyTests.cs`.

## Common Patterns

**Asserting a failure result:**
```csharp
var result = _service.Restore(game, archive);
Assert.False(result.Success);
Assert.Contains("...", result.Message);
```

**Asserting an exception type:**
```csharp
Assert.Throws<ArgumentNullException>(() => CrashReport.Describe(null!, "backing up"));
Assert.Throws<NotSupportedException>(() => ...);
```

**Conditional skip when the environment cannot simulate the case:**
- Tests that rely on ACLs or junctions check `TestFileSystem.TryDenyAccess` (or the equivalent) and return early when it returns false. They must not fail on a machine where the deny rule cannot be applied.

**Pinning SPEC rules:** name the test after the rule it defends, and put the SPEC reference in a comment when the rule is subtle. Keep the test even when it looks obvious. The destination rule is pinned by `Backup_accepts_a_destination_on_the_save_data_own_volume`.

**Adding a test:**
1. Create or extend `tests/SavegameBunker.Tests/<Type>Tests.cs`. Make it `public sealed`.
2. Build temp state under `Path.GetTempPath()` with a `Guid` folder name, and implement `IDisposable` to delete it.
3. Use the existing hand-written `IDialogService` doubles for any UI interaction.
4. Name the method as a sentence in snake_case.
5. Run `dotnet test SavegameBunker.sln` before committing. Use `WaitForAsync` for any state that a background `Progress<T>` or `Task.Run` settles.

---

*Testing analysis: 2026-11-18*
