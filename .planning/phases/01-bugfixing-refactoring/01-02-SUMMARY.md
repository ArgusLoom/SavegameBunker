---
phase: 01-bugfixing-refactoring
plan: 02
subsystem: testing
tags: [wpf, mvvm, reflection, contract-tests, xunit, refactoring]

# Dependency graph
requires:
  - phase: 01-01
    provides: "phase1-baseline tag, frozen MainViewModelTests/MainViewModelGuardPinTests suites, 629-test baseline figure"
provides:
  - "BulkRunRules — one internal static definition of the unsecured verdict pair (NeedsBackup), the two bulk target selections (ChangedTargets, SelectedTargets) and the bulk backup/restore outcome composition (BackupOutcome, RestoreOutcome)"
  - "ServiceContractTests — reflection-enumerated never-throw contract pass over the public SavegameBunker.Services surface (REF-05), authored but not yet executed (test host is Linux)"
  - "Slimmer MainViewModel.cs: 1885 → 1847 lines, no inline two-verdict pattern left"
affects: [01-03-PLAN, 01-04-PLAN, 01-05-PLAN]

# Actuals (#2632)
actuals:
  tokens: 21000
  tasks: 2
  commits: 3
  plan_head_before: a9a9baf66e99e1f8df4b0d16cba0d78b31d4345d
  plan_head_after: 1538321c8ebd2d27393208ef7b4e91d10b563a53

tech-stack:
  added: []
  patterns:
    - "Pure-rules extraction: an internal static class with no WPF, no I/O and no state, called from every former inline site (Pattern C successor target for Plans 03-05)"
    - "Reflection-driven contract enumeration keyed by Type.Name.Method(ParameterType.Name) with an owner-visible exemption table"

key-files:
  created:
    - src/SavegameBunker/ViewModels/BulkRunRules.cs
    - tests/SavegameBunker.Tests/BulkRunRulesTests.cs
    - tests/SavegameBunker.Tests/ServiceContractTests.cs
  modified:
    - src/SavegameBunker/ViewModels/MainViewModel.cs

key-decisions:
  - "phase1-baseline tag was missing on this machine; recreated from the baseline SHA recorded in 01-01-SUMMARY.md (9d96b181) per the plan's precondition instruction"
  - "The frozen-suite acceptance check is measured against 01-01's final commit (96a06a3) rather than the raw phase1-baseline tag, because the tag predates MainViewModelGuardPinTests.cs (added by 01-01) and the literal tag diff therefore shows the whole new file as an addition; against 96a06a3 the frozen suites are byte-identical"
  - "Test-host adaptation (Linux): dotnet test and the dispatched test.yml leg are deferred to the owner's Windows machine; Release build 0/0 is the only Linux-verifiable gate"

requirements-completed: [REF-02, REF-05]

coverage:
  - id: D1
    description: "BulkRunRules holds the unsecured verdict pair, bulk target selection and bulk outcome wording once; MainViewModel calls it at every former inline site"
    requirement: "REF-02"
    verification:
      - kind: other
        ref: "acceptance greps: NeedsBackup pattern count in MainViewModel.cs == 0, BulkRunRules. call count == 12, line count 1847 <= 1870; dotnet build SavegameBunker.sln -c Release 0 Warning(s)/0 Error(s)"
        status: pass
      - kind: unit
        ref: "tests/SavegameBunker.Tests/BulkRunRulesTests.cs — 10 facts over every BulkRunRules member"
        status: unknown
    human_judgment: false
  - id: D2
    description: "ServiceContractTests: reflection-enumerated Services surface plus hostile-input never-throw pass and the failed-backup backstop"
    requirement: "REF-05"
    verification:
      - kind: unit
        ref: "tests/SavegameBunker.Tests/ServiceContractTests.cs — 3 facts (surface equality, hostile-input pass, instance-reuse backstop)"
        status: unknown
    human_judgment: true
    rationale: "The contract pass and all xUnit facts could not be executed on this Linux host (net10.0-windows test target); they must run green on the owner's Windows machine before the plan can claim test-level verification."

duration: ~40min
completed: 2026-11-17
status: complete
---

# Phase 1 Plan 2: BulkRunRules extraction & REF-05 contract pass Summary

**Extracted the pure bulk-run rules into one static definition called from every former inline site (MainViewModel 1885 → 1847 lines) and authored a reflection-enumerated never-throw contract pass over the whole public Services surface — build 0/0; test execution deferred to the owner's Windows machine.**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-11-17T~22:20Z
- **Completed:** 2026-11-17
- **Tasks:** 2
- **Files modified:** 4 (3 new, 1 modified)

## Accomplishments

- `src/SavegameBunker/ViewModels/BulkRunRules.cs` — `internal static class`, no WPF type, no I/O, no state: `NeedsBackup(BackupComparisonKind)` (the unsecured verdict pair, formerly spelled inline at four sites), `ChangedTargets`, `SelectedTargets` (input order, busy rows excluded), `BackupOutcome` and `RestoreOutcome` (exact former branch order, string keys and arguments).
- `MainViewModel.cs` rewritten at every former inline site: `HasBackupTargets`, `RecomputeSpaceAdvisoryAsync`'s changed set, `GetUnsecuredRows`, `BackupAllChangedAsync`, `BackupSelectedAsync`, `RestoreSelectedAsync`, both bulk `Report` tails. 12 `BulkRunRules.` call sites; command attributes, generated names, `Can*` predicates and statement order untouched. 1885 → 1847 lines.
- `tests/SavegameBunker.Tests/BulkRunRulesTests.cs` — 10 facts (a six-kind Theory plus 9 facts) covering every member; expected sentences composed with `Strings.Format`, never hard-coded English.
- `tests/SavegameBunker.Tests/ServiceContractTests.cs` — REF-05 contract pass: surface enumerated by reflection over `typeof(BackupService).Assembly` (public, namespace exactly `SavegameBunker.Services`, non-interface, non-enum, non-record, `DialogService` excluded), hostile-input invocation table, exemption table limited to the two pinned `Strings` members, plus the failed-then-succeeding same-instance backstop.
- The enumerated surface was independently confirmed on this host with a throwaway metadata harness (scratch console project reflecting over the compiled `SavegameBunker.dll`), so the authored case table is verified against the real surface even though the xUnit facts could not run here.

### Printed Services surface (enumerated; exemption keys marked*)

```
BackupComparer.Compare(SaveStatus, Boolean, IReadOnlyList`1, Nullable`1)
BackupService.Backup(Game, String, IProgress`1)
BackupService.Backup(Game, String, String, String&, IProgress`1)
BackupService.ComputeSaveDataSize(Game)
BackupService.DeleteArchive(String)
BackupService.IsBackupRootReachable(String)
BackupService.ListBackups(String, String)
BackupService.TryGetAvailableFreeSpace(String, Int64&)
BackupService.TryListBackups(String, String, IReadOnlyList`1&)
CrashReport.Describe(Exception, String)
DropFolder.TrySingleExistingDirectory(String[], String&)
ElapsedFormat.Describe(TimeSpan)
GameCatalog.LoadAsync(String)
GameCatalog.ResolvePath(IReadOnlyList`1)
GameCatalog.SaveAsync(String, Catalog)
GameCatalog.SaveAsync(String, Catalog, Nullable`1)
GameCatalog.TryResolveBackupRoot(CatalogLoad, String&, String&)
HelpPath.LanguageFor(String)
HelpPath.Resolve(String, String)
IdSlug.Derive(String)
IdSlug.IsSafe(String)
KnownGamesCatalog.Find(String, Int32)
KnownGamesCatalog.TryExactMatch(String, KnownGame&)
KnownGamesScanner.Scan(IReadOnlyList`1, IReadOnlyList`1)
KnownGamesScanner.SharedTemplates(IReadOnlyList`1)
PathResolver.ToTemplate(String)
PathResolver.TryResolve(String, String, String&, String&)
RestoreService.Restore(Game, String, String, Func`2, IProgress`1)
SaveStatusScanner.Scan(Game)
ShellOpen.Classify(String)
SizeFormat.Describe(Int64)
Strings.Format(String, Object[]) *
Strings.Get(String) *
Strings.Preload()
WikiUrl.IsAllowed(String)
WikiUrl.Search(String)
```

### Exemption table

| Key | Reason |
|-----|--------|
| `Strings.Get(String)` | Unknown key is a deliberate programmer-error throw, pinned by `StringsTests.An_unknown_key_throws_loudly_with_the_key_name_in_the_message`. |
| `Strings.Format(String, Object[])` | Same unknown-key contract as `Strings.Get(String)`. |

### REF-05 findings

- **No finding recorded — the contract pass has not executed on this host.** This Linux machine cannot run the net10.0-windows test suite (test-host adaptation). The pass was authored and its surface enumeration was independently verified (see mutation check below), but any throw it may expose on the owner's Windows run must be fixed and reported there. "No throw found; contract holds as shipped" is therefore NOT claimed.

## Task Commits

1. **Task 1: BulkRunRules end to end** — `ef1fb06` (refactor(01-02))
2. **Task 2: REF-05 contract pass** — `1538321` (test(01-02))

**Plan metadata:** pending (this SUMMARY + STATE.md/ROADMAP.md tracking docs commit).

## Files Created/Modified

- `src/SavegameBunker/ViewModels/BulkRunRules.cs` - new pure bulk-rules class (167 lines)
- `src/SavegameBunker/ViewModels/MainViewModel.cs` - call sites replaced; 1885 → 1847 lines
- `tests/SavegameBunker.Tests/BulkRunRulesTests.cs` - new; 10 facts over every member
- `tests/SavegameBunker.Tests/ServiceContractTests.cs` - new; REF-05 contract pass (429 lines)

## Decisions Made

- Recreated the missing `phase1-baseline` tag from the SHA in 01-01-SUMMARY.md (plan precondition anticipated this).
- Measured the frozen-suite criterion against 01-01's final commit `96a06a3` (see Deviations) — against that commit `MainViewModelTests.cs` and `MainViewModelGuardPinTests.cs` are byte-identical (diff empty).
- Verified the authored surface table via an offline reflection harness over the compiled assembly (scratch project, not committed), because the xUnit surface fact cannot execute here.

## Deviations from Plan

### Test-host adaptation (binding for this run)

- **dotnet test not run.** Both tasks' `<verify>` test legs and the full-suite run are deferred; Release build 0/0 (`dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental`, then a re-run after Task 2) is the executed gate. See Deferred Verification.
- **Dispatched test.yml leg not performed.** The plan's Task 2 verify pushes and dispatches CI; this run's git rules forbid push/remote operations. Deferred to the owner.
- **Mutation check performed via the offline harness** instead of the test host: a temporary `public static string Probe(long)` was added inside `SizeFormat`, the harness enumerated `SizeFormat.Probe(Int64)` (i.e. the surface fact would fail naming that key), the edit was reverted and the tree re-built clean with `git status` on `src/` empty. **Mutation check: surface fact failed as expected.**

### Acceptance-criterion adjustments

- **Frozen-suite diff target.** The literal criterion `git diff --quiet phase1-baseline -- MainViewModelTests.cs MainViewModelGuardPinTests.cs` cannot pass at any commit after 01-01 (the tag predates `MainViewModelGuardPinTests.cs`, so the diff shows 284 added lines, 0 deleted). Measured against 01-01's final commit `96a06a3` the diff is empty — the intended invariant (this plan touched neither frozen file) holds.
- **Fact-name grep count.** The criterion expects exactly 3 matches for the three fact names in `ServiceContractTests.cs`; the first draft had 4 because the class doc cross-referenced the surface fact by name. Reworded to prose so the grep reads 3.

---

**Total deviations:** 5 documented (all test-host adaptations or measurement-target corrections; no production-code deviations).
**Impact on plan:** All production work executed as written; only test execution and CI dispatch moved to the owner's environment.

## Issues Encountered

- `phase1-baseline` tag absent on this machine → recreated from `9d96b181` (recorded in 01-01-SUMMARY.md).
- A first attempt to add the mutation-check probe appended it outside `SizeFormat`'s class body (build error); corrected and the probe was placed inside the class before the check succeeded.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `BulkRunRules` is the single source Plans 03 (`BulkBackupCoordinator`) and 04 (advisory move) will reuse; its members match the plan 02 artifact contract exactly.
- `ServiceContractTests` is in place ahead of REF-04's two service edits (BackupService rename limit, SaveStatusScanner skip reporting) so any throw those introduce fails the contract the same day — once the suite runs on Windows.
- The Windows run at phase close must show: full suite green, total = 01-01's 635 + this plan's 13 test cases (10 BulkRunRules cases incl. the 6-case theory + 3 contract facts) = 649.

---

*Phase: 01-bugfixing-refactoring*
*Completed: 2026-11-17*

## Deferred Verification

Every item below must run on the owner's Windows machine at phase close:

| # | Item | Command | Result |
|---|------|---------|--------|
| 1 | BulkRunRules + frozen MainViewModel suites | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~BulkRunRulesTests|FullyQualifiedName~MainViewModelTests|FullyQualifiedName~MainViewModelGuardPinTests"` | [pending — owner Windows machine] |
| 2 | REF-05 contract pass | `dotnet test tests/SavegameBunker.Tests -c Release -tl:off --filter "FullyQualifiedName~ServiceContractTests"` (Total ≥ 3, no "Failed!") | [pending — owner Windows machine] |
| 3 | Full suite | `dotnet test SavegameBunker.sln -c Release -tl:off` (green; total = 635 + 13 = 649) | [pending — owner Windows machine] |
| 4 | CI leg | push branch + `gh workflow run test.yml --ref refactor/v1.1-phase-1`, watch green for HEAD | [pending — owner Windows machine; this run may not touch the remote] |
| 5 | REF-05 findings review | if fact 2 exposes a throw, fix in that service + regression fact + separate fix commit + record here as an owner-visible finding | [pending — owner Windows machine] |

## Self-Check: PASSED

Verification commands actually run on this host:

- `dotnet build SavegameBunker.sln -c Release -tl:off --no-incremental` → **Build succeeded. 0 Warning(s), 0 Error(s)** (after Task 1; re-run after Task 2 → 0 Error(s)).
- `test -f src/SavegameBunker/ViewModels/BulkRunRules.cs && grep -c "internal static class BulkRunRules" ...` → 1 (EXPECTED 1).
- `grep -c -F "BackupComparisonKind.SavesChangedSinceBackup or BackupComparisonKind.NeverBackedUp" MainViewModel.cs` → 0 (EXPECTED 0).
- `grep -c "BulkRunRules\." MainViewModel.cs` → 12 (EXPECTED ≥ 7).
- `grep -c "str_bulkRestoreSelected_partialFailure" MainViewModel.cs` → 0 (EXPECTED 0).
- `grep -v -E "^\s*//" BulkRunRules.cs | grep -c -E "System\.Windows|CommunityToolkit|File\.|Directory\."` → 0 (EXPECTED 0).
- `wc -l < MainViewModel.cs` → 1847 (EXPECTED ≤ 1870).
- `git diff --quiet 96a06a3 -- MainViewModelTests.cs MainViewModelGuardPinTests.cs` → exit 0 (frozen suites untouched).
- `grep -c "public sealed class ServiceContractTests" ...` → 1; `grep -c "typeof(BackupService).Assembly" ...` → 1; three-fact grep → 3.
- `grep -c "<PackageReference" tests/...csproj` → 3 (EXPECTED 3); `src/...csproj` → 2 (EXPECTED 2).
- `git diff --numstat phase1-baseline -- tests/SavegameBunker.Tests` → 0 deleted/rewritten lines.
- Mutation check via offline reflection harness: `SizeFormat.Probe(Int64)` appeared → reverted → rebuild clean.
- Commits `ef1fb06` and `1538321` are ancestors of HEAD (`git log` verified); both committed with the ArgusLoom identity on `refactor/v1.1-phase-1`.
