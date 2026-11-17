# What SavegameBunker doesn't do

These are deliberate choices, not missing features planned for later.

- **No auto-backup.** You click **Back up all changed** (or a per-row backup) when you want a
  copy. SavegameBunker never backs up on its own; it only asks once at startup when games have
  changes not backed up yet.
- **A backup on the same disk as your saves dies with that disk.** If the point of backing up is
  surviving a dead drive, pick a different volume.
- **Not a launcher.** SavegameBunker will not launch programs via **Open save folder** — it opens the
  folder in Explorer, nothing more. Treat a games list or backup folder someone else gave you like
  any other untrusted files.
- **Not cloud sync and not Steam Cloud.** It copies the save folder you pointed it at, to the
  folder you chose, and nothing else.
- **The known-games list is a shortcut, not a living database.** It's a hand-maintained snapshot bundled with the app — there is no online update, so it is never
  fetched, never refreshed, and goes stale as games update or move their save folder. Scan only
  ever finds games that are in that table; every other game has to be added by hand. If Scan
  picks the wrong store's path, or a game changed its save location, use
  [Add a game](../how-to/add-a-game.md) or **Change save folder** on that row.
- **Does not touch the Windows registry.** Nothing in SavegameBunker reads install locations or
  uninstall strings — every action works whether or not a game is even installed on this PC.
