# Wishlist

A prioritized pool of ideas, deferred conveniences and accepted limitations worth revisiting.
**This is not a roadmap.** Nothing here is planned, scheduled or committed; a future milestone
picks from it (or doesn't). Tiers reflect the goal of a learning project that should also become a genuinely useful tool;
within a tier, order is rough preference, not a schedule.

## Tier 1 — biggest gap to "genuinely useful", do these first

- **Auto-backup trigger**. Without *some* automation the app stays
  a reminder list. Cheapest first: a "back up on app start" option, then a configurable interval;
  a background save-folder watcher is the ambitious middle; a Steam-play-session hook is the
  long-term goal. Needs persisted machine-level preferences — see the `settings.json` item, which this promotes
  from "someday" to a prerequisite. *Shipped: one-shot "N games have changes not
  backed up yet — back up now?" prompt on app start (no settings file); open: a persisted,
  configurable trigger — still waits on `settings.json`*
- **Backup retention + delete UI**. Every backup is a full zip and the
  folder grows without bound. *Shipped: single-archive delete from the restore
  picker; open: capping archives per game (last N or by age) and prune UI —
  still one workstream, not two*

## Tier 2 — real value, more work; natural second milestone

- **Online-sourced known-games table** — the shipped table is a static, hand-curated snapshot
  (~286 titles) that goes stale with every game that moves its save location. Ludusavi's
  community manifest (open data, thousands of titles, actively maintained) could feed a
  periodically refreshed download instead, with the bundled table as offline fallback. Changes
  the trust story: fetched data must be validated the same way the hand table is
- **Cancelling a running backup or restore** — a long bulk backup on a slow USB drive
  currently owns the app until it finishes. Cooperative cancellation through the existing
  progress plumbing; the restore path must only allow cancellation at its stage boundaries
  (never mid-swap)
- **File logging / diagnostics** (DIAG-01..02) — unglamorous, but the first thing that turns
  "it doesn't work" bug reports into actionable ones once real users exist
- **`settings.json` for machine-level preferences** — window state, a remembered
  catalog, a theme choice. Deliberately absent in v1 (the catalog is the entire configuration);
  promoted to Tier 2 *because* Tier 1's auto-backup trigger needs somewhere to persist its
  settings. Currently listed there as its prerequisite
- **Steam integration** — detect installed Steam games directly (instead of guessing from the
  known-games table) and optionally read Steam playtime, which would ground the
  "N hours not backed up yet" figure in real playtime rather than save mtimes. Highest single
  feature value in this tier, but also the most platform-specific, fragile work; only worth
  starting after the Tier 1 items have landed

## Tier 3 — kept for completeness, deliberately low

- **Extensible known-games table** — a user-editable `known-games.local.json` merged over the
  shipped table. Partially superseded by the online table: once entries refresh automatically,
  a local override is only an escape hatch, not the main extension path
- **Per-game backup destination** overriding `backupRoot` — only matters to users who
  juggle several drives with per-drive preferences; `backupRoot` plus the reachability status
  covers the common case
- **Optional backup encryption** — archives are plain zips today; an opt-in password would
  matter once backups go to cloud or shared network drives. Without cloud targets on this
  wishlist, its audience is thin; revisit only if a network-share threat model becomes real
- **Heuristic discovery of unknown vendor folders** in the Find games scan — deferred, not
  rejected. Superseded in intent by Steam integration; only revisit if Steam
  integration stalls
- **Backing up a save folder equal to its own archive folder repacks every earlier archive on
  each run** (2026-11-19 code review, WR-01). Not data loss — Restore already refuses this
  layout — but wasteful. Fix means detecting the save/archive overlap before the zip write, not
  just before restore
- **Orphaned `xs` spacing token left in `Styles.xaml`** (2026-11-19 code review, IN-01) by a since-landed
  layout change (`Styles.xaml:57`). Cosmetic, no behavior impact; remove on the next pass through
  that file
- **Keyboard-shortcuts reference still documents "Alt, H -> Help"** in both user-guide languages
  (2026-11-24 code review, WR-04) — stale since Help was replaced by the About button (Alt+B) in an
  earlier commit. Fix is a one-line edit in each of the two keyboard-shortcuts.md files
- **Alt+B mnemonic collision between "Back up" and "About"** (2026-11-24 code review, WR-05) — WPF's
  `AccessKeyManager` is case-insensitive, so `_Back up`'s `B` and `A_bout`'s `b` collide in the same
  unscoped toolbar region; `StringsTests.cs`'s mnemonic-uniqueness check deliberately excludes
  "about" from its letter set, so this is untested. Pre-existing, not a regression from any single
  change. Fix means picking a different mnemonic for one of the two

## Accepted limitations that could one day be lifted

Each is documented in SPEC.md's *Known limits*; lifting one is a scope decision, not a bug fix.
Unordered by design — these are correctness edges, not features.

- **Save-vs-backup comparison uses local wall-clock times** and can compare wrongly across
  machines or a system clock change. A manifest timestamp inside the archive would be the start
  of a fix
- **Archive verification compares entry names and uncompressed lengths, never a checksum** —
  .NET 10 does not validate CRC-32 on read; .NET 11's `ZipArchive` does, so retargeting later
  may make this nearly free. Cheapest of the five: opportunistic, take it when a retarget
  happens anyway
- **Cross-process catalog guard compares a file timestamp, not content** — FAT32's 2-second
  resolution can miss a collision, and a sync client touching the stamp can cause a false
  refusal. Closing both needs a content hash plus a session-lifetime lock or merge strategy
- **High Contrast support is dark-schemes-only** — WPF-UI applies one fixed dark high-contrast
  dictionary; a light contrast scheme still yields a dark window. Proper support means replacing
  the shell library's dictionary, a piece of work, not a patch
- **Light theme exists only as the session-only toggle** — no persisted theme choice without a
  machine-level settings file (see the `settings.json` item above)
