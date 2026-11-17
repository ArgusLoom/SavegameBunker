# Getting started

## 1. Download and run

[Download the latest release](https://github.com/ArgusLoom/SavegameBunker/releases/latest), unzip it,
and run `SavegameBunker.exe`. There is nothing to install.

First launch writes a `games.json` file next to the `.exe` and shows **No games yet**.

## 2. Find your games

Click **Scan**. SavegameBunker looks for save folders it already recognizes on this PC and lists the
ones it finds. Review the list and add the ones you want tracked.

Scan won't find everything — some games use a save path SavegameBunker doesn't know, or store saves
somewhere unusual. For those, use **Add game** (see
[Add a game Scan doesn't find](how-to/add-a-game.md)).

## 3. Back up

Click **Back up all changed**, or check the games you care about and click **Back up selected**.

If you haven't set a backup folder yet, SavegameBunker asks where to put backups — a USB stick, an
external drive, a network share, or even the same disk. It remembers your choice. See
[Change the backup folder](how-to/change-the-backup-folder.md) for the tradeoffs.

The **Status** column tells you whether a backup is worth doing for each game. **Refresh**
rechecks status without scanning for new games.

If games have changed since their last backup, SavegameBunker asks once at startup: "N games have
changes not backed up yet — back up now?" Saying yes runs the same backup as **Back up all changed**;
saying no changes nothing.

## 4. Know the shortcuts

Every toolbar button has an Alt+letter shortcut, and **Ctrl+B** / **Ctrl+R** back up or restore
whichever row is currently focused. See the full
[keyboard shortcuts reference](reference/keyboard-shortcuts.md).

!!! warning "Close the game first"
    A running game can leave you with a half-written backup. Back up after you've quit the game,
    not while it's still running.

## Other toolbar buttons

- **Open the games list** opens `games.json` directly, for anyone who wants to inspect or
  hand-edit it.
- **Switch theme** toggles SavegameBunker's own light/dark window theme — separate from the colors
  you've set in Windows.

## Next steps

- [Back up your saves](how-to/back-up-your-saves.md)
- [Restore a save](how-to/restore-a-save.md)
- [What SavegameBunker doesn't do](reference/limitations.md)
