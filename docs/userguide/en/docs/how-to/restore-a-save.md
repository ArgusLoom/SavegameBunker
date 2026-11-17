# Restore a save

## Restore one game

Click **Restore** on that game's row. This opens the **Restore — _game name_** window, listing
every backup for that game, newest first. Pick one. SavegameBunker then:

1. Writes a safety backup of your **current** saves first.
2. Extracts the backup you picked into a temporary folder next to your saves.
3. Renames the old save folder aside.
4. Renames the new one into place, then removes the renamed-aside copy.

That order matters: your current saves are only ever deleted after a safety copy of them has
already been written to the backup volume.

Before step 2, SavegameBunker asks you to confirm. Choose **Cancel** there and nothing changes: your
saves stay as they are, and the safety backup from step 1 is removed again.

SavegameBunker won't restore a game whose save folder contains your backup folder — restoring replaces
the whole save folder, which would delete the backups with it. Move the backup folder somewhere
else first ([Change the backup folder](change-the-backup-folder.md)).

If the save files on disk are newer than the backup you picked, SavegameBunker warns you before
continuing — you could be about to overwrite more recent progress. That check compares file
timestamps, not file contents, so an incorrect system clock can occasionally trigger it when
nothing is actually wrong.

## Delete an old backup

Each row in the restore window has a delete button. It asks for confirmation and then removes
exactly that one archive from the backup folder — the ZIP file on disk is deleted, not just the
list entry. This is permanent. Nothing else is touched: other backups of the game, and the game's
saves, stay as they are.

The delete button is greyed out while a restore is running in that window.

## Restore several games at once

1. Check the games you want to restore.
2. Click **Restore selected**.

SavegameBunker restores each checked game's newest backup, one at a time, and asks you to confirm
each one individually. Approving one game's restore does not silently start the next — you stay
in control of every single game that gets touched.

## Keyboard shortcut

**Ctrl+R** restores whichever row is currently focused.

## Moving saves between PCs or accounts

Restoring on the **same PC and account** the backup was made on always works. A save restored
onto a different machine or a different Steam account may be rejected by the game itself — many
games bind saves to a Steam ID or checksum. SavegameBunker puts the files back exactly as they were
backed up; it cannot make the game accept them. See
[Move to a new PC](move-to-a-new-pc.md) for what's worth trying.
