# Change the backup folder

The first time you back up, SavegameBunker asks where to put backups — this is the **backup folder**.
You can point it at a USB stick, an external drive, a network share, or even the same disk your
saves are already on. Whatever you choose is honored without comment — SavegameBunker never refuses,
redirects, or silently relocates a backup based on where it points.

SavegameBunker remembers your choice and checks on startup whether the backup folder is still
reachable, rather than only telling you when you try to back up or restore. The header always
shows **Backup folder: _path_**, or **Backup folder: not set** if you haven't picked one yet.

## Why the backup folder matters

A backup on the **same disk** as your saves protects you from accidentally deleting or
overwriting the save files themselves, but it dies along with that disk if the drive fails. If
the whole point of backing up is surviving a dead drive, pick a different volume — an external
drive, a second internal disk, or a network share.

## Changing it later

Click **Change backup folder** in the toolbar. This opens a folder picker titled **Select the
folder SavegameBunker writes backups to**. Existing backups stay where they are; only new backups go
to the new location.

## What SavegameBunker does not do here

- It does not back up automatically — you decide when to click **Back up all changed**. The only
  nudge is one question at startup when games have changes not backed up yet.
- It is not cloud sync and not Steam Cloud. It copies the save folder you pointed it at, to the
  folder you chose, and nothing else.
