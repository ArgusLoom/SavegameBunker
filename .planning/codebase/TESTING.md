---
last_mapped_commit: 83ebbea19dcfdb14b3bc7077fb16fedb14c1475f
last_mapped_at: 2026-11-17
---
# Testing Patterns

**Analysis Date:** 2026-11-17

## Test Framework

**Runner:**
- xUnit 2.9.3 (`xunit`), with `xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk` 18.12.0.
- Config: no `xunit.runner.json` is committed. Defaults apply.
- Project: `tests/SavegameBunker.Tests/SavegameBunker.Tests.csproj`, targeting `net10.0-windows` with WPF enabled.
- `Xunit` is a global using (`<Using Include="Xunit" />`), so test files do not need `using Xunit;`.

**Assertion Library:**
- xUnit's built-in `Assert` class. No FluentAssertions, Shouldly or other assertion package is referenced.
- Most-used assertions in the suite: `Assert.Equal` (about 437 uses), `Assert.True` and `Assert.False` (about 432), `Assert.Contains`, `Assert.Throws`.

**Mocking:**
- No mocking library. `Moq`, `NSubstitute` and similar are not referenced (zero occurrences in `tests/`).
- Fakes are hand-written and live in test files or shared helpers (see Mocking below).

**Run Commands:**
```powershell
dotnet test SavegameBunker.sln                              # Run all tests (Windows only)
dotnet test SavegameBunker.sln -c Release --no-build        # Run the same way CI does, after a build
dotnet test tests/SavegameBunker.Tests --filter "FullyQualifiedName~BackupServiceTests"   # One class
dotnet test SavegameBunker.sln --collect:"XPlat Code Coverage"   # Coverage (collector not configured in repo)
```

- The suite runs only on Windows, because the test project targets `net10.0-windows` and uses WPF types. `dotnet build` also works on Linux and macOS (`EnableWindowsTargeting`), but `dotnet test` does not.
- CI: `.github/workflows/test.yml` runs `dotnet build SavegameBunker.sln -c Release` and then `dotnet test SavegameBunker.sln -c Release --no-build` on `windows-latest` for every pull request to `main`. `.github/workflows/release.yml` calls the same workflow first, so a red suite blocks a release.
- `global.json` pins the SDK to 10.0.400 with `latestFeature` roll-forward, so the test host uses the same SDK as CI.

## Test File Organization

**Location:**
- All tests live in one project, `tests/SavegameBunker.Tests/`, separate from `src/`. Tests are not co-located with source.

**Naming:**
- One test class per subject: `<Subject>Tests.cs`, for example `BackupServiceTests.cs`, `GameCatalogTests.cs`, `MainViewModelTests.cs`.
- Some tests cover a workflow rather than a class: `AddGameFlowTests.cs`, `StartupUnsecuredPromptTests.cs`.
- Test helpers have no `Tests` suffix: `TestFileSystem.cs` (`internal static class TestFileSystem`).
- Fake dialog services are defined in the file that first needs them and reused from there: `ThrowingDialogService` is declared in `AddGameDialogViewModelTests.cs`, and `RecordingDialogService` is declared in `MainViewModelTests.cs`.

**Namespace:**
- All test code is in `SavegameBunker.Tests`, file-scoped, and references `SavegameBunker.Models`, `SavegameBunker.Services` and `SavegameBunker.ViewModels` directly.

**Structure:**
```
tests/SavegameBunker.Tests/
├── <Subject>Tests.cs           # one class per subject, sealed
├── TestFileSystem.cs           # shared filesystem helpers (ACL deny, junctions)
└── SavegameBunker.Tests.csproj
```

## Test Structure

**Class shape:**
- Test classes are `public sealed class`. Those that create temp state implement `IDisposable`.
- Set-up is done in the constructor, and tear-down is in `Dispose()`. xUnit creates a new instance per test, so each `[Fact]` gets fresh state.

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
        catch (UnauthorizedAccessException) { /* ... */ }
    }
}
```

**Test naming:**
- Test method names are full sentences in snake_case with the first letter capitalised, describing behaviour rather than the method under test: `Fresh_view_model_has_add_disabled`, `Empty_template_is_rejected`, `Wiki_search_escapes_spaces_in_the_typed_name`, `A_known_key_returns_its_authored_sentence`.
- Do not use `MethodName_Scenario_Expected` names. Follow the sentence style used across the suite.

**Patterns:**
- Set-up: build the fixture inline with a private helper that returns an object, e.g. `GameWithSaves(fileName, content)` and `GameWithEmptySaveDir()` in `BackupServiceTests.cs`. Each returns a `Game` whose `SaveDirTemplate` points into the per-test temp folder, with forward slashes, as the catalog requires.
- Arrange-Act-Assert with blank-line separation. Assertions are plain `Assert.*` calls at the end of the test.
- Doc comments on the test class explain which SPEC rule is being pinned. For example, `BackupServiceTests` says nothing in the tests "may reintroduce a volume check". New tests for a rule should carry this kind of comment on the class.

**Theories:**
- Used for input variations (17 `[Theory]` attributes in the suite):

```csharp
// tests/SavegameBunker.Tests/PathResolverTests.cs
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void Empty_template_is_rejected(string? template)
{
    Assert.False(PathResolver.TryResolve(template, null, out _, out var error));
}
```

**Facts:**
- About 538 `[Fact]` attributes. Prefer `[Fact]` for single scenarios.

## Async Testing

**Pattern:** async tests return `Task` and `await` the operation under test.

**Settling asynchronous state:**
- The test host has no `SynchronizationContext`, so `Progress<T>` reports arrive on the thread pool and may land after a command returns. Assert settled state with a polling helper, not a snapshot taken right after the command finishes (`CLAUDE.md`).
- Each file that needs it currently defines its own copy of a `WaitForAsync` helper with the same signature and a 3000 ms default timeout:

```csharp
private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
```

- The helper is defined in `AddGameDialogViewModelTests.cs`, `ChangeSaveFolderDialogViewModelTests.cs`, `MainViewModelTests.cs` and `StartupUnsecuredPromptTests.cs`. When a new test needs it, reuse the same signature. When you touch these files, consider moving the helper into `TestFileSystem.cs` or a new shared `TestWait.cs`, but do not do it as part of an unrelated change.

**Command tests:**
- `AsyncRelayCommand` returns a `Task` from `ExecuteAsync`. Tests call `await vm.SomeCommand.ExecuteAsync(null)` when they need the result, and `WaitForAsync` when the work is started from a `Progress` callback.

## Mocking

**Framework:** None. Hand-written fakes.

**Dialog fakes:**
- `ThrowingDialogService` (`tests/SavegameBunker.Tests/AddGameDialogViewModelTests.cs`) implements `IDialogService` and throws on any call. Use it when a test must prove that no dialog is shown.
- `RecordingDialogService` (`tests/SavegameBunker.Tests/MainViewModelTests.cs`) records every call into `InfoMessages`, `ErrorMessages`, `ConfirmMessages` and the `ConfirmReturnSequence` queue. It also exposes an `OnReport` hook, which tests use to observe the view model mid-operation without a timing assumption.

```csharp
var dialog = new RecordingDialogService { ConfirmToReturn = true };
var vm = new MainViewModel(load, dialog);
```

**What to Mock:**
- Anything behind `IDialogService`: folder pickers, message boxes, confirmations.
- Filesystem state that is hard to create, such as a locked subfolder, by applying real ACLs through `TestFileSystem.TryDenyAccess`.

**What NOT to Mock:**
- Services (`BackupService`, `RestoreService`, `GameCatalog`, `PathResolver`). They are concrete and cheap; tests run them against a real temp folder with real zip files.
- The filesystem itself. Tests create `Path.GetTempPath()/SavegameBunkerTests/<guid>` directories and real archives.

**Filesystem helpers:**
- `tests/SavegameBunker.Tests/TestFileSystem.cs` is the single home for filesystem tricks:
  - `TryDenyAccess(path)` applies a Deny ACE for the current user, then confirms the ACL actually blocks enumeration. It returns `false` when the environment cannot enforce the rule, so the test no-ops instead of failing.
  - `RemoveDenyRules(path)` lifts ACEs level by level before recursive delete.
  - `RemoveJunctions(path)` unlinks junctions without descending into them.
- Tests that need a locked folder call `TryDenyAccess` and return early when it returns `false`.

## Fixtures and Factories

**Test Data:**
- Built inline per test. Helper methods on the test class create the scenario (`GameWithSaves`, `GameWithEmptySaveDir`). No shared fixture classes or `IClassFixture<T>` are used.
- Model objects are created with object initialisers: `new Game { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') }`.
- Files are written with `File.WriteAllText` and archives with `ZipFile` directly.

**Location:**
- No fixture directory. Test data is generated into per-test temp folders and removed in `Dispose`.
- Shipped data used by tests: `src/SavegameBunker/games.json` and `src/SavegameBunker/Data/known-games.json` are read as-is by `GameCatalogTests.cs` and `KnownGamesCatalogTests.cs`.

**Temp folder rule:**
- Every test that writes to disk uses `Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"))` and deletes it in `Dispose`. Cleanup is wrapped in `try`/`catch` for `IOException` and `UnauthorizedAccessException`, so a leftover folder does not fail the run.

## Coverage

**Requirements:** None enforced. No threshold is set in the test project, the solution or the CI workflow.

**View Coverage:**
```powershell
dotnet test SavegameBunker.sln --collect:"XPlat Code Coverage"
```

- No coverage tool is referenced by the test project. Add `coverlet.collector` only if coverage reporting is required.

## Test Types

**Unit Tests:**
- The majority of the suite. Services are tested directly with real files. View models are tested with fake dialogs and real services.
- Examples: `BackupServiceTests.cs`, `RestoreServiceTests.cs`, `PathResolverTests.cs`, `GameCatalogTests.cs`, `ElapsedFormatTests.cs`.

**Integration Tests:**
- Multi-step workflows across view models and services: `AddGameFlowTests.cs`, `CatalogSessionTests.cs`, `StartupUnsecuredPromptTests.cs`, `SaveStatusScannerTests.cs`.
- There is no separate integration project and no test database or HTTP host.

**Resource and Contract Tests:**
- `StringsTests.cs` and `ResourceKeyTests.cs` check that every key used in code exists in `src/SavegameBunker/Themes/Strings.en.xaml`. Add a key to the XAML before using it in code, or these tests fail.
- `WikiUrlTests.cs` and `HelpPathTests.cs` pin URL and path shapes.

**E2E Tests:**
- Not used. The WPF UI is verified manually. The `dotnet run` command is the manual check.

## Common Patterns

**Testing a `Try`-style method:**
```csharp
Assert.True(PathResolver.TryResolve(template, null, out var saveDir, out var error));
Assert.Equal(expected, saveDir);
```

**Testing an `OperationResult`:**
```csharp
var result = _service.Backup(game, _backupRoot);
Assert.True(result.Success, result.Message);
```
- Pass `result.Message` as the second argument to `Assert.True` so a failure shows the reason.

**Testing a cancelled confirmation:**
- Use `dialog.ConfirmToReturn = false` (or a `ConfirmReturnSequence` queue for multiple prompts) and assert `result.Cancelled` rather than `result.Success == false`.

**Testing error messages:**
```csharp
var result = _service.Backup(GameWithEmptySaveDir(), _backupRoot);
Assert.False(result.Success);
Assert.Contains("Save folder is empty", result.Message);
```
- Assert on a stable fragment of the message, not the whole string.

**Testing exceptions that must not escape:**
- Services should never throw across their boundary. Test this by asserting on the `OperationResult`. Use `Assert.Throws<T>` only for code that is documented to throw (`Strings.Get` on a missing key).

**Testing a locked folder:**
```csharp
if (!TestFileSystem.TryDenyAccess(lockedDir))
    return; // This environment cannot enforce an ACL deny; nothing to assert.
var result = _service.Backup(game, _backupRoot);
Assert.False(result.Success);
```

**Testing async view model commands:**
```csharp
await vm.BackupCommand.ExecuteAsync(game);
await WaitForAsync(() => dialog.InfoMessages.Count == 1);
Assert.Equal("Backup created.", dialog.InfoMessages[0]);
```

## Writing a New Test

1. Create `tests/SavegameBunker.Tests/<Subject>Tests.cs` if the subject has no test class yet. Use the file-scoped namespace `SavegameBunker.Tests;`.
2. Declare the class `public sealed`. Implement `IDisposable` if the test writes to disk and create the temp folder in the constructor as shown above.
3. Name each test as a sentence in snake_case that states the behaviour, for example `Restore_refuses_when_the_safety_backup_fails`.
4. Use `[Theory]` with `[InlineData]` only when the same assertion holds across several inputs.
5. Verify with `dotnet test SavegameBunker.sln --filter "FullyQualifiedName~<Subject>Tests"`, then the full suite before pushing.

---

*Testing analysis: 2026-11-17*
