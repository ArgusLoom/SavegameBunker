# Move to a new PC

SavegameBunker has no account and no cloud sync, so "moving" means copying files yourself. There are
two things to move: the **games list** (`games.json` — which games you track, and where their
saves live) and the **backups** (the actual save data).

## The portable way: one games list on the USB stick itself

Keep `games.json` and your backups on the same USB stick or external drive, and give SavegameBunker a
relative `backupRoot` (see [the games list file format](../reference/games-list-format.md)). A
relative `backupRoot` resolves next to the games list file it's defined in, not next to the
`.exe` — so the same stick works whether it's plugged into the old PC or the new one.

On the new PC:

1. Copy `SavegameBunker.exe` there too (or keep it on the stick alongside the games list).
2. Run it with the games list's path on the command line: `SavegameBunker.exe D:\games.json`.
3. Use **Restore** on each game you want set up on the new machine.

## The manual way

1. On the old PC, make sure every game you care about has a recent backup.
2. Copy the backup folder to the new PC (or to a drive the new PC can read).
3. On the new PC, run SavegameBunker and use **Add game** to recreate each entry, pointing it at the
   save folder location on the new machine.
4. Click **Backup folder** and point it at the folder you copied over, then **Restore**
   each game.

## Whether the game will accept the save

SavegameBunker restores the files exactly as they were backed up. Whether the **game** accepts them
on a new PC depends on the game, not on SavegameBunker:

- Same Steam account, same PC: always works.
- Same Steam account, different PC: usually works.
- Different Steam account, or a different store (Steam → GOG, for example): often rejected by the
  game itself, because many games bind saves to an account ID or checksum. There is nothing
  SavegameBunker can do about this — it puts the files back, and the game decides whether to use them.
