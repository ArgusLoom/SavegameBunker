# Roadmap: SavegameBunker

## Milestones

- ✅ **v1.0 — Initial Release** — shipped 2026-11-17
- 🔄 **v1.1 — Hardening & Refactoring** — in progress

## v1.0 — Initial Release

A self-contained Windows desktop app that backs up PC game save data to a folder the user
chooses and restores it again: catalog management, a known-games scan, bulk backup/restore with
selection and search, startup health checks, and keyboard/text-scaling accessibility. Full detail
in `CHANGELOG.md`'s `[1.0.0]` entry.

## v1.1 — Hardening & Refactoring

Behavior-preserving hardening of the v1.0 codebase: the post-release audit rounds (R1/R2,
2026-11-17) produced a fix batch already merged; this milestone completes the remaining
audit-derived defects and pays down the single structural debt item — `MainViewModel.cs`
(1,885 lines) — via an incremental, behavior-locked split.

**Goal:** Same behavior everywhere (the 629-test suite stays green, verified on Windows),
remaining R2 defects closed, and `MainViewModel` reduced to a composition root of sealed
coordinators with no new dependencies and no abstraction beyond the named seams.

## Phases

### Phase 1: Bugfixing & Refactoring

**Goal:** Close the remaining R2 audit defects and split `MainViewModel.cs` (1,885 lines)
into sealed, single-responsibility coordinators — behavior-locked at every step, verified by
the unchanged 629-test suite.

**Requirements**: REF-01, REF-02, REF-03, REF-04, REF-05
**Depends on:** —
**Plans:** 5 plans

Plans:
**Wave 1**
- [ ] 01-01-PLAN.md — Baseline pin: tag + branch, Release 0/0, green suite and CI recorded; six guard characterization pins

**Wave 2** *(blocked on Wave 1 completion)*
- [ ] 01-02-PLAN.md — Pure helpers (BulkRunRules) + REF-05 reflection-driven never-throw contract test

**Wave 3** *(blocked on Wave 2 completion)*
- [ ] 01-03-PLAN.md — BulkBackupCoordinator (bulk backup + bulk restore) + REF-04a rename-retry-limit message

**Wave 4** *(blocked on Wave 3 completion)*
- [ ] 01-04-PLAN.md — StatusSweepCoordinator (sweep engine + advisory I/O) + REF-04b per-row unreadable-path warning

**Wave 5** *(blocked on Wave 4 completion)*
- [ ] 01-05-PLAN.md — RowActionsCoordinator + composition-root pass + owner decision on REF-02's ~700-line ceiling

**Execution order is strictly sequential (Plan 01→05):** baseline test pin → pure helpers →
bulk-backup coordinator → status-sweep coordinator → row-actions service → rest form. Each
plan ends with: Linux Release build 0/0, a dispatched `test.yml` run green, one commit per
concern; the Owner's Windows suite run is the verification of record at phase close.
