# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-11-17

First release. SavegameBunker backs up and restores PC game save folders: pick a game, pick a
backup destination, back up and restore with a couple of clicks. One window, no account, no cloud,
nothing to install.

### Highlights

- **It finds your games for you.** One click on **Scan** checks a hand-curated list
  against your PC and shows every game with real save files on it. Tick the
  ones you want, add them all at once.
- **You see at a glance what needs attention.** Every game shows one clear verdict — up to
  date, *3 hours of play not backed up yet*, never backed up, no save data, or backup folder
  unavailable — plus file count and size.
- **Backing up is one click.** For one game, for the ones you tick, or with **Back up all
  changed** for everything you played since the last backup.
- **Restoring is safe by design.** Before anything is replaced, SavegameBunker takes a safety
  backup of your current saves, extracts to a temporary folder and only then swaps it in. An
  interrupted restore never leaves a half-written save folder, and it warns you when your current
  saves are newer than the backup you picked.
- **Your backups go where you want** — USB stick, external drive, network share or just another
  folder.
- **Single file, nothing to install.** Unzip, run `SavegameBunker.exe`. No .NET install needed.

### Added

- **Add Game dialog** — type a game's name and pick it from suggestions; the save folder fills in
  by itself. Not in the list? Browse to the folder, drag it onto the window, or look it up on
  PCGamingWiki with the built-in globe button. Every folder is checked (file count and size, or
  "not there yet") before you save it, and duplicates are caught.
- **Bulk actions** — tick games (or all of them) and use **Back up selected** or **Restore
  selected**, with a progress bar. A bulk restore still asks about every game separately.
- **Restore picker** — every backup listed newest first with its exact date and time, plus a
  delete button to remove a single old archive (after confirmation).
- **Startup reminder** — if games have unsaved changes, SavegameBunker asks once at startup whether
  to back them up now. No schedule, no nagging.
- **Search box** narrows the list as you type without touching your selection.
- **Free-space hint** before a backup compares the destination's room with what you are about to
  write.
- **Change save folder** — repair a row whose saves moved, from inside the app.
- **Oversized-folder warning** in case you picked the wrong folder.
- **Offline user guide** in English and German, opened from the toolbar help button in the
  language Windows runs in — no internet needed.
- **Keyboard first** — Ctrl+B / Ctrl+R back up or restore the focused row, Alt-mnemonics on every
  toolbar button, tooltips everywhere, usable at Windows text sizes up to 150%.
- **Light and dark theme** with a green accent. It starts in the mode Windows uses, **Switch theme**
  flips it for the session, and dark High Contrast mode is followed.
- **Portable mode** — `SavegameBunker.exe D:\retro-games.json` runs against another catalog, and a
  relative `backupRoot` resolves next to it. Exe, list and backups on one USB stick work on any PC.
- **One honest config file** — `games.json` holds the game list and the backup folder. A missing
  file is created with a commented template; a broken one is reported and never overwritten. Paths
  accept `%DOCUMENTS%`, `%SAVEDGAMES%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%` and forward
  slashes — and `%DOCUMENTS%` follows OneDrive's folder redirection.
- **Built to be trusted** — every backup is written under a temporary name and verified before it
  counts; backups skip links that point outside the save folder; tampered or absurdly oversized
  archives are refused on restore; an unexpected error shows a plain message instead of crashing.

### Known limitations

- **Windows High Contrast, light schemes.** Only dark High Contrast themes are followed correctly;
  a light contrast theme still renders the window dark, though accent colors follow the OS palette.
- Some games bind saves to a Steam ID or checksum them, so a save restored onto a *different*
  machine or account may be rejected by the game itself. SavegameBunker restores the bytes
  faithfully; same-machine reinstall always works.
- No scheduled or continuous auto-backup — beyond the one-time startup reminder, the tool is
  deliberately reactive.
- The known-games table is a bundled snapshot and is not updated online; other games are added by
  hand.

### What it deliberately does not do

Cloud backup or sync, automatic pruning of old backups, encryption, auto-update, a translated UI
(the user guide is bilingual; the app is English), launching games, and any operating system other
than Windows 11.
