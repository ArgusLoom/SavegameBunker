# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — Initial Release

**Shipped:** 2026-11-17
**Phases:** 11 | **Plans:** 50 | **Tasks:** 105

### What Was Built
- Catalog foundation: self-seeding, self-validating `games.json`, path resolution with 5
  variables, registry layer fully deleted
- In-app Add Game with a ~280-entry known-games type-ahead, drag-and-drop, a PCGamingWiki search
  control, off-UI-thread save-status scanning
- Crash-safe, verified ZIP backups to any user-chosen folder, including the saves' own volume
- Known-games "Scan": bounded-parallel sweep, dedup, collision-safe stable ids
- Restore with a mandatory safety-backup-first ordering, save-vs-backup comparison per row, and a
  per-archive delete button
- Checkbox selection (per-row + header select-all) with a durable, id-keyed selection set, a
  debounced real-time name search, "Back up selected"/"Restore selected" bulk actions, a
  determinate byte-based progress bar, and an advisory-only free-space line
- A startup reminder for games with unbacked-up changes and a startup backup-destination health
  check
- Alt-key access on every toolbar action, Ctrl+B/Ctrl+R row shortcuts, and 125-150% text scaling
  conformance across every fixed dialog
- WPF-UI Fluent dark shell, plain-language copy, per-row repair actions, never-crash guarantees,
  single self-contained shippable `.exe`, retargeted to .NET 10 LTS

### What Worked
- Async service shape decided in the first phase and never retrofitted — every later phase's
  tests kept working because the signature was already right
- One reusable seam per capability (`ListBackups`, `BackupComparer`, `IdSlug.IsSafe`) instead of
  private per-caller copies — the restore picker, verdict and safety backup all bound to the same
  `ListBackups` built early on
- Deliberate scope cuts (registry/launch/uninstall) removed roughly half the original defect list
  by deletion rather than by fixing
- 555+ xUnit tests grown incrementally, phase by phase, catching real regressions rather than
  being written after the fact
- Proving the bulk-loop mechanics on the lower-stakes backup path first, then reusing them for the
  destructive restore path — the per-game confirmation and strict sequencing contracts came along
  intact
- Gap-closure plans scoped to exactly one UAT finding each, with pinned regression facts — no
  rework bloat

### What Was Inefficient
- UAT walkthroughs needing a second physical machine (clean-machine, games-machine checks) caused
  the biggest scheduling friction of the project
- The Windows High Contrast fix needed two attempts: substituting individual `SystemColors` brush
  keys was tried, found to half-render the palette, and reverted — the correct fix (covering
  WPF-UI's own theme dictionary) stayed scoped out
- A ContextMenu/Popup visual-tree boundary silently broke `RelativeSource AncestorType=Window`
  command bindings in the row overflow menu (found during a UAT walkthrough, not by any test) —
  WPF's separate visual root for popups is a recurring trap worth a standing note
- The selection checkbox's TwoWay binding took three attempts before the DataGrid's
  never-commits-CellTemplate-bindings behaviour was correctly worked around
- Execution spanned two long bursts with a two-month gap; verification digests went stale in the
  gap and could not be cheaply refreshed on a non-Windows machine (WPF tests are Windows-only) — a
  milestone close needed an owner-confirmed override

### Patterns Established
- Every destructive operation gets a load-bearing, comment-documented ordering (restore:
  safety-backup → extract-to-temp-sibling → remove-old → rename-in) that must not be reordered
- Catalog-level configuration only — no `settings.json`, ever, because no genuinely machine-level
  setting exists; revisit only if a real one appears
- UAT items quote the exact shipped string in `expected:`, never a paraphrase, so a UAT pass is
  provably against real product copy
- Bulk operations target the selection set, are filter- and comparison-kind independent, and own a
  global run flag that per-row commands must respect
- Free-space checks stay advisory forever — information, never a gate
- Keyboard and mouse bind to the identical RelayCommands; no parallel command path
- Verification digests go stale the moment code moves; verification must be re-run (on Windows)
  after any post-verification commit that touches covered files, or explicitly overridden at close

### Key Lessons
1. Decide cross-cutting architectural shape (threading, error contract, UI seam) in the first
   phase that touches it — retrofitting it later invalidates the very tests that are supposed to
   give confidence
2. A menu or popup surface needs its own explicit command-wiring check; `RelativeSource
   AncestorType` silently fails across the ContextMenu/Popup visual-tree boundary with no error
3. When a UAT item needs a second physical machine, expect it to be the slowest item to close —
   schedule those checks first, not last, so they don't become the final blocker
4. A same-volume backup "warning" that nobody asked for and that blocks the common case (external
   SSD, same-disk convenience backup) is worse than no warning
5. Prove a risky loop pattern on the least destructive consumer before reusing it on the
   destructive one — contract reuse beats contract re-derivation
6. A read-only WPF DataGrid never commits a CellTemplate control's bindings by itself; expect one
   explicit `UpdateSource()` flush per cell-editable-bound control
7. When work pauses for weeks, plan the re-verification cost into the close — digests age, and a
   Windows-only test suite cannot refresh them from a non-Windows machine
8. A milestone audit is cheapest when run against final HEAD *after* the release-prep commits, not
   before them

### Cost Observations
- Sessions: multiple, spanning 2026-07-24 to 2026-11-17
- Notable: owner-declined/deferred UAT checkpoints needing a second physical machine, and a
  two-month calendar gap mid-project, were the main sources of elapsed-time-vs-work-time gap, not
  implementation effort
