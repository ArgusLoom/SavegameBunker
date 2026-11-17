# Requirements: v1.1 — Hardening & Refactoring

Behavior-locked hardening. Nothing observable changes; the 629-test suite is the regression
contract and the Owner's Windows run is the verification of record.

## v1.1 Requirements

### Code Quality (refactoring)
- [x] **REF-01**: Behavior parity — every observable behavior of v1.0 is preserved exactly; the
      full test suite passes unchanged (no test rewritten except where a test asserts a removed
      defect contract that an R2 fix supersedes, and then only with owner-visible justification).
- [x] **REF-02**: `MainViewModel.cs` is reduced to a composition root (observable properties,
      command wrappers, coordinator wiring) of 1,556 lines (761 code-only lines, measured
      2026-11-17 after the phase's three named extractions; the plan-time ~700 ceiling was
      revised by owner decision "accept-measured" — reaching ~700 would have required
      abstractions beyond the phase's named seams or deleting design-rationale XML docs, both
      excluded by the phase's own constraints); bulk-backup orchestration,
      status-sweep engine and per-row actions each live in their own sealed class with no WPF
      references, matching the Services-layer contract.
- [x] **REF-03**: Threading discipline is uniform after the split: every cross-thread state write
      goes through the established OnUi()/IProgress pattern; the re-entrancy guard semantics
      (`IsAnyRowChecking`, reset-to-Checking) are preserved and documented where they now live.

### Bugfixing (remaining R2 audit defects)
- [x] **REF-04**: The rename-retry cap (100 collisions) is distinguishable from a disk error in
      the user-facing message, and Scan's status rows surface skipped unreadable paths the same
      way backups do — Scan and Backup can no longer disagree about completeness without saying so.
- [x] **REF-05**: The documented never-throw service contract holds without exception: every
      public service entry point returns a failure or warning instead of throwing, verified by a
      contract test pass over the Services surface.

## Future Requirements (deferred)

- Opt-in games-library update from GitHub (SHA-256-pinned, default off) + "Suggest a game"
  issue template — decided against for v1.1 scope; revisit when the curated list matures.
- Authenticode signing of release binaries (needs an owner-held certificate).

## Out of Scope

- Any new NuGet dependency (CLAUDE.md two-dependency rule).
- Any feature visible to the user beyond the REF-04 message-quality fixes.
- Splitting or touching `GameRowViewModel`, `DialogService` contract, or SPEC-defined behavior.

## Traceability

| REQ | Phase |
|-----|-------|
| REF-01 | 1 |
| REF-02 | 1 |
| REF-03 | 1 |
| REF-04 | 1 |
| REF-05 | 1 |
