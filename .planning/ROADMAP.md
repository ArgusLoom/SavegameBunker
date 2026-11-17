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
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 1 to break down)

**Execution order is strictly sequential (Plan 01→05):** baseline test pin → pure helpers →
bulk-backup coordinator → status-sweep coordinator → row-actions service → rest form. Each
plan ends with: Linux Release build 0/0, a dispatched `test.yml` run green, one commit per
concern; the Owner's Windows suite run is the verification of record at phase close.
