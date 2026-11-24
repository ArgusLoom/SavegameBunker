# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **An About window**, opened from a new About button at the far right of the toolbar (replacing
  Help). Shows the app icon, version and build date, and carries the user guide link Help used to
  be — the offline copy, falling back to the online one.

### Changed

- **Shorter toolbar labels.** "Change backup folder" is now "Backup folder", "Back up selected" is
  now "Back up", and "Restore selected" is now "Restore" — the selection count already shown on
  the last two buttons made "selected" redundant.

### Fixed

- **Each row's overflow ("more actions") button no longer renders cut off at the window's right
  edge.** The Actions column was 104px — just enough for the three 32px icon buttons in theory, but
  too tight at the window's minimum width once real padding was accounted for. It is now 128px,
  with a 1px gap between the icon buttons.
- **Help now opens the online user guide if the offline copy isn't there.** A stripped install or
  a deleted `help` folder used to make the Help button report that the guide was missing; it now
  opens the same guide at https://argusloom.github.io/SavegameBunker/ instead.
- **The startup reminder about unsecured games no longer turns up mid-session.** With a backup
  folder set, the reminder could miss its moment at startup and appear later, after a Refresh; it
  now appears at startup or not at all, and an empty games list no longer saves it up for later.
- **"Back up selected" can no longer overlap a row's own backup.** While it was still looking for
  a starting folder or waiting in the folder dialog, a row's Back up button, Refresh and Change
  backup folder stayed clickable, so a row backup could run on the same game at the same time;
  they now wait.
- **Choosing a backup folder no longer stalls on a disconnected network drive.** The first backup
  asks for a folder and suggests a start location; with a mapped network drive that was offline,
  finding that suggestion could hold the folder dialog back for a long time. Network and optical
  drives are no longer asked whether they are ready.
- **Backups of a save folder inside your backup folder now include its subfolders.** With a backup
  folder such as Documents and a game that saves under Documents\My Games, backups held only the
  files at the top of the save folder; every subfolder was skipped without a word, and restoring
  such a backup removed those subfolders. Backups now include them. When a backup leaves a folder
  out on purpose because it holds backups, the backup message now names it. Restore also refuses a
  save folder that holds the game's own backups instead of deleting them with it.
- **Backups now show the right date with Thai, Persian or Arabic regional formats.** With one of
  those Windows formats, backups were named in that calendar's year (for example 2569 instead of
  2026). A Thai-format PC then showed every game as "up to date" even after its saves had
  changed, so "Back up all changed" skipped it. Backups made from now on use the usual date.
  Older archives keep their names.
- **Drive roots now work as save and backup folders.** Picking the root of a drive such as E:\
  as a save folder stored it as "E:", which Windows reads as the current folder on that drive;
  and a backup folder at a drive root was not recognised as holding anything, so the rules that
  keep backups out of backups did not apply to it.
- **A save folder that holds nothing but your backup folder is no longer "backed up" as an empty archive.**
  The backup now fails and says why.
- **Deleting a backup in the restore list and then cancelling now updates the game's row.**
  Before, the row kept showing the deleted backup until you clicked Refresh.
- **Deleting a backup in the restore list now asks inside the restore list.** The confirmation
  used to open in the main window behind the restore list, where it could not be seen or clicked
  while the list was open.
- **Save files next to a backup folder inside the save folder are backed up again.** With the
  backup folder at, say, `data\Backups` inside a save folder, every file in `data` was left out of
  the backup without a word. Only the backup folder itself is left out now.
- **A save folder that is also the backup folder now warns about the subfolders it skips.** Those
  subfolders were named as "backup folder" in the backup message even when they held save data;
  they are now listed in a warning as not backed up.
- **Opening the user guide from the About window now reports inside the About window.** An error
  used to open in the main window behind it, where it could not be seen or clicked, and the note
  that only the online guide was found showed behind it too.
- **The restore list can no longer be closed while its delete confirmation is open**, which left
  the confirmation unanswerable.
- **Removing, adding or finding games and changing a save folder now wait while "Back up" is still
  choosing its backup folder**, so the backup can no longer run on a removed game or an old save
  folder, and two catalog saves no longer race.
- **A save folder that is the game's own backup folder no longer packs earlier backups.** Each
  backup used to include every earlier one and warn about a file it could not read; its
  subfolders are now backed up too. Restore also refuses a save folder that holds another game's
  backups, instead of deleting them with it.
- **The startup reminder no longer gets lost when you click Refresh right after starting.** With a
  slow backup folder it could count zero unsecured games while the list was still being checked.
- **Find Games says "1 file" instead of "1 files".**
- **The "safety backup" tag in the restore list stays readable when its row is selected.**

### Changed

- **Dialog buttons are one width, and the title bar's close button turns the standard Windows red
  on hover again.** The app's own error text and icons keep their contrast-corrected red; a few
  gaps in the status bar, the progress panel and the empty list are tidier.
- **A row's backup/restore progress (spinner, bar, restore steps) now shows in the Status column
  instead of Actions.** It is a status fact about that row, not something to click; the Actions
  column keeps just the Back up, Restore and overflow buttons.
- **The empty-state illustration moved to the new About window.** The "no games yet" and
  "no matches" screens now show only their text and buttons.

## [1.0.1] - 2026-11-18

A maintenance release: two clearer error situations and a lot of work under the hood. Nothing
about how you use SavegameBunker has changed.

### Fixed

- **A full backup folder now says so.** When a game's backup folder already holds an archive with
  today's name — and every numbered variant of it — the backup error now says exactly that
  ("rename retry limit reached") instead of reading like a disk failure. Your existing archives
  were never in danger; only the message was misleading.
- **A partly unreadable save folder is now visible.** Some save folders contain files Windows
  won't let SavegameBunker read. The game's row now names those paths and says how many there
  are, so the file count no longer looks complete while a backup would quietly leave those files
  out. You can spot the gap before you trust the backup.

### Changed

- Under the hood: the window's logic is now split into focused components (bulk runs, status
  scan, per-row actions), and the test suite grew from 629 to 689 checks. Every existing
  behavior is pinned by a test and unchanged.

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
