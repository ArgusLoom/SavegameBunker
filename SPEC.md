# SavegameBunker – Specification

**Revised 2026-11-17.**

## Purpose
Windows 11 application that keeps a list of PC games in one window and does two things with each of
them: back up its save data as a ZIP to a folder the user chooses, and restore a chosen backup again.

## Scope (v1)
- Game list maintained by the user, from the UI or by hand. The only detection is **Scan**, which
  checks the bundled known-games table against this PC and offers what it finds; games outside that
  table are never detected.
- The backup destination is user-configurable and may be any folder, including one on the same disk
  as the save data.
- Backup and restore are both in v1. A backup that has never been restored is an unverified backup,
  so the round trip is the core feature and ships as one unit.
- Every action works regardless of whether the game is currently installed. Save data outlives an
  uninstall, and archiving the saves of a game you just removed is a primary use case.
- No cloud sync, no auto-update, no localization (UI text language doesn't matter, code/comments in English).

## Tech Stack
- .NET 10 (LTS, pinned by `global.json`), C#
- WPF (desktop GUI)
- Target platform: Windows 11

## Data Model: games.json

A catalog file holding where backups go and which games exist. By default it lives next to the
executable; a different one can be passed on the command line, so one `SavegameBunker.exe` can drive any
number of catalogs.

```json
{
  // Where backups are written. A relative path is resolved next to THIS file,
  // so an .exe and its catalog on a USB stick keep working whatever drive
  // letter Windows assigns them. Omit it and SavegameBunker asks on first backup.
  "backupRoot": "SavegameBunkerBackups",

  "games": [
    // { "id": "witcher3",
    //   "displayName": "The Witcher 3: Wild Hunt",
    //   "saveDirTemplate": "%DOCUMENTS%/The Witcher 3/gamesaves" }
  ]
}
```

Fields:
- `backupRoot`: destination folder for all backups. Absolute (`E:/SavegameBunkerBackups`) or relative to
  the catalog file. Supports the same variables as `saveDirTemplate`. Optional.
- `games[].id`: stable internal key, used as the backup subfolder name. Restricted to `a-z`, `0-9`
  and `-`, so it can never escape `backupRoot`.
- `games[].displayName`: name shown in the UI
- `games[].saveDirTemplate`: absolute path with Windows environment variables (`%APPDATA%`,
  `%LOCALAPPDATA%`, `%USERPROFILE%`) and the pseudo-variables `%DOCUMENTS%` and `%SAVEDGAMES%`.
  It names **one** directory — see *Why one save directory per entry* below

**Forward slashes are accepted and preferred in every path.** Windows APIs take `/` as a separator,
and it removes JSON's one genuine hand-editing hazard: `"C:\\Users\\..."` requires doubling every
backslash and is the mistake everyone makes. Both forms work; the seeded template shows the slash
form. Paths are normalised on load.

`%DOCUMENTS%` is not a Windows environment variable. It is substituted from
`SpecialFolder.MyDocuments` before the rest is expanded, because Windows 11 with OneDrive Known
Folder Move relocates Documents to `%USERPROFILE%\OneDrive\Documents` — a hardcoded
`%USERPROFILE%\Documents` is wrong on those machines.

`%SAVEDGAMES%` is not one either, and unlike Documents it has no `SpecialFolder` member at all — it
is read from `SHGetKnownFolderPath(FOLDERID_SavedGames)`, a P/Invoke into `shell32`. It exists for
the same reason `%DOCUMENTS%` does: Saved Games is a relocatable known folder, so hardcoding
`%USERPROFILE%\Saved Games` is wrong wherever the user or a sync client has moved it. It earns its
place because a large share of modern titles save there rather than in Documents. Both
pseudo-variables beat `%USERPROFILE%` when a path is converted back into a template, since both
normally sit *inside* the profile and the longest match is the portable one.

### Why one save directory per entry
`saveDirTemplate` names a single directory. Some games do split their data — saves in Documents,
settings in `%LOCALAPPDATA%` — and a list of named sources was designed for this: one archive per
backup with a subfolder per source and an in-archive manifest mapping subfolders back to templates.
It was rejected for v1 on two grounds. Most apparent splits are not splits: Bethesda, Paradox,
Larian and the `Saved Games` titles all keep everything under one parent folder, and naming that
parent covers both. The genuine exceptions hold *settings*, whose loss costs a few adjusted sliders
rather than progress. Against that, N directories mean N swaps during restore, and restore's
strongest guarantee — the save directory afterwards holds either entirely the old set or entirely
the new one — degrades from guaranteed to merely likely. Trading the app's only hard safety promise
for a convenience in the rare case is the wrong direction. A game that genuinely needs two folders
gets two catalog entries, which already works.

The file supports `//` comments and trailing commas, so the seeded template can explain itself.
**Comments do not survive a write from the app** (adding a game re-serialises the file); the
template says so where the user will read it.

### Why JSON and not XML or INI
XML removes the backslash problem but costs roughly three times the typing per entry and introduces
its own escaping (`&`, `<`). INI would be the most hand-editable format for a flat list of two-field
records — no quoting, no escaping, native comments, section name *is* the id — but there is no INI
parser in the BCL, and the zero-dependency rule would mean hand-rolling one. Given that the in-app
"Add game" dialog makes hand-editing optional rather than mandatory, hand-editing ergonomics is a
second-order criterion, and accepting forward slashes removes the only first-order JSON problem at a
cost of two lines. JSON also keeps `System.Text.Json`, universal editor support and validation.

### Which catalog is loaded
Resolution is stateless, in this order:

1. A path passed on the command line — `SavegameBunker.exe D:\retro-games.json`
2. Otherwise `games.json` next to the executable

The loaded path is always visible in the UI, so it is never ambiguous which catalog is being edited
or backed up. One executable therefore drives any number of independent catalogs, each with its own
`backupRoot` and its own backups.

### Why there is no settings.json
Separating settings from data is right as soon as settings exist. In v1 the only configurable value
is `backupRoot`, and that belongs to the catalog rather than to the machine: pointing the app at a
different catalog should bring that catalog's backup destination with it. The one genuinely
machine-level candidate — "remember the last catalog opened" — does not exist because resolution is
stateless by design. A `settings.json` in v1 would be a file with nothing correct to put in it.
It earns its existence in v2, when machine-level preferences (window state, logging, a remembered
catalog) actually appear.

### Catalog bootstrap
Three cases, deliberately distinct:

| State at startup | Behaviour |
|---|---|
| `games.json` absent | Write a commented template, start with an empty list, name the path in the UI |
| `games.json` present and valid | Load it |
| `games.json` present and malformed | Show an error naming the path, **leave the file untouched**, keep running with an empty list |

A malformed file is never overwritten by the template. Absent and broken are different situations,
and silently replacing a broken catalog would destroy the user's own configuration.

## Features

### 1. Catalog
- The list of games comes from the loaded catalog file and nowhere else.
- **Backup folder**: the user picks `backupRoot` in a folder dialog and it is written back to the
  catalog. Until one is set, the first backup asks for it. It can be changed at any time; existing
  backups under the old root are not moved.
- **Add game**: the user types a display name and picks the save folder in a folder dialog.
  SavegameBunker derives an `id` from the name (or takes the known-games table's stored `id` verbatim
  when the user picked a suggestion), converts the picked path back into a template
  (`%SAVEDGAMES%`, `%DOCUMENTS%`, `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`, longest match
  wins) so the entry stays portable, and appends it to `games.json`. The `id` is not shown or
  edited in the UI — it is an implementation key, and implementation keys stay off the screen. It is
  still validated against `a-z0-9-` (and against Windows reserved device names) and against the
  ids already in the catalog; a collision is refused by asking for a different display name, never
  silently renumbered, because the `id` is the name of the folder that will hold their backups.
- **Finding the save folder**: a folder picker only helps someone who already knows the path, and
  most players do not. SavegameBunker therefore ships an embedded table of known games — display name and
  aliases mapped to a `saveDirTemplate` — that the name field autocompletes against; picking a match
  fills the template. Two properties keep this honest. It is never the only route: browsing is always
  available, and a game missing from the table costs nothing but a manual pick. And it is never
  trusted blindly: the filled template is resolved immediately and the dialog reports what is
  actually there (file count, size, or "does not exist yet"), so an outdated or simply wrong entry is
  visible before it is saved rather than discovered at the first backup.

  This is deliberately close to a non-goal. *Automatic detection of unknown games* stays out of
  scope — nothing outside the table is guessed or matched against the machine. A static shipped
  table is a starting point for typing, and it is the one artifact in the project that decays on its
  own as games change where they save.
- **Scan**: resolves every known-games entry on this PC and lists the ones whose folder holds at
  least one file. Entries already in the catalog, and entries whose folder is shared with other
  titles, are skipped (and can be shown on demand). The user ticks the hits and adds them in one
  step. Scan only reads; it never adds anything without that confirmation, and it never looks
  beyond the table.
- **Open the games list**: opens `games.json` in the system default editor for hand editing. When no
  application is associated with `.json`, SavegameBunker opens Explorer with the file selected instead of
  reporting a failure the user can do nothing with.
- **Refresh**: reloads the catalog from disk and re-reads save-directory status, without restarting.
  One action, because "does the screen match the file" is one question. Problems in an edited file
  are reported exactly as they are at startup, and a file that has become malformed leaves the
  previously loaded list on screen rather than emptying it.

### 2. Save status and backup comparison
- Per game, resolve `saveDirTemplate` and report whether that directory exists, with file count,
  total size and **last-modified date** (the newest modification time inside it).
- Per game, report when the **newest backup** was taken, read from `backupRoot`.
- Per game, compare the two and state the result in plain language, so the user can judge *before*
  acting whether either operation is worth doing:

| Situation | Shown as | What it means |
|---|---|---|
| Newest backup taken at or after the save directory's last change | **Up to date** | Backup already holds the current state; a restore would change nothing meaningful |
| Save directory changed after the newest backup | **2 days of play not backed up yet** | Backing up captures those changes; restoring rolls them back by that much |
| No backup for this game | **Never backed up** | Restore is not possible |
| No save directory | **No save data yet** (or *No save data right now — you can still restore* when backups exist) | Restore would recreate it from scratch |
| `backupRoot` unset or unreachable | **Backup folder unavailable** | Backup state is unknown, not "missing" |
| Template unresolvable | **No save data yet** (name cell is the error) | The comparison column does not grow a sixth kind; the Game cell already names a path error, and a second, possibly contradictory sentence here would be worse than sharing the no-save-data bucket |

  The comparison is `save directory last-modified` vs `time the backup was taken` (the backup's
  own timestamp, which is exactly the state it captured). A backup can never hold *newer* data than
  the saves it was made from, so the only two meaningful directions are "up to date" and "saves have
  moved on since".

  This is an honest heuristic, not an oracle: a save directory that was corrupted or reset a minute
  ago also reads as "saves changed since backup", and there a restore is exactly right. What the
  comparison reliably answers is *how much recent play a restore would undo* — which is the question
  the user actually has, and the risk the automatic pre-restore backup covers.

- Installation status is not shown. It is irrelevant to backing up save data.

### 3. Backup
- Resolve `saveDirTemplate`. Missing or empty directory → error naming the path, abort, write nothing.
  A save directory whose only content is the backup folder counts as empty too: the backup fails
  and no archive is written, because an archive with nothing in it would otherwise verify and be
  reported as a backup.
- Resolve `backupRoot`. If none is configured, ask for one via a folder dialog; cancelling aborts the
  backup cleanly. The folder dialog opens on a detected external volume when one is present, so the
  common case stays one click.
- Target path: `<backupRoot>\<id>\<yyyy-MM-dd_HHmmss>.zip`. The folder is created if missing. The
  timestamp is taken after the archive is written and verified, not before, so a long backup never
  dates itself earlier than the save files it just captured.
- The destination's volume is never inspected: a `backupRoot` on the save data's own volume is
  written to exactly like any other. See *Backup destination* below.
- If `backupRoot` is unreachable (drive not connected, path gone, not writable), say which path
  failed and abort without writing.
- The archive is written under a temporary name and renamed only after it is complete, so a failed
  or interrupted backup leaves no partial `.zip` behind.
- After writing, the archive is reopened and its entries checked against the source before success
  is reported. An archive nobody can read is not a backup.
- A `backupRoot` inside the game's own save directory, or equal to it, is honoured for Backup —
  the archive walk leaves out exactly the folders that hold backups: a subfolder that is or
  contains `backupRoot`, and, when the save directory is itself `backupRoot` or the game's archive
  folder `<backupRoot>\<id>`, every subfolder of it, because other games' archive folders cannot be
  told apart from save data there. Nothing else is left out, so a save directory that merely sits
  inside `backupRoot` (backup folder `Documents`, save folder `Documents\My Games\X`) or inside its
  own archive folder (`<backupRoot>\<id>\saves`) is archived with all its subfolders, since no
  archive of this app lives below it. The success message names each left-out folder in a plain
  note (example: "Left out: backups (backup folder).") which describes what the archive holds and
  is not a warning about the destination, so the *Backup destination* rule still holds; left-out
  folders are not unreadable paths and never raise the "NOT in this backup" warning.
- Restore is refused for a game whose archive folder `<backupRoot>\<id>` lies inside, or is, its
  save directory — this covers every `backupRoot` inside or equal to the save directory — because
  restore renames the save directory aside and deletes it, which would delete the archives with
  it, including the safety backup taken a moment earlier. Backing up in such a layout stays
  supported; the error names the cause and the fix (move the backup folder).
- **Startup reminder.** Once per start, after the first status scan, if any game has saves changed
  since its last backup or has never been backed up, one confirmation asks whether to back those
  games up now. Yes runs the same bulk backup as *Back up all changed*; No does nothing and is not
  asked again until the next start. This is a one-time nudge, not a schedule: the tool still never
  backs up on its own.

#### Backup destination
The destination is a plain folder the user chooses. Any folder: a USB stick, an external SSD, a NAS
share, a second internal disk — or the same disk the saves are on.

Same-disk backups are explicitly allowed, because they still cover the failure modes that actually
destroy save data most often: the game overwriting a save, a corrupted save file, a bad patch or mod,
an uninstall that cleans up the save folder, or simply wanting to keep a state before trying
something. The one thing they do not survive is the disk itself failing — the rarest of that list.
Refusing them would trade the common cases for the rare one.

SavegameBunker states the trade-off here, in this document, and respects the choice at runtime without
comment: it never warns, never silently redirects and never blocks.

`DriveType.Removable` is deliberately not a rule anywhere: external USB SSDs and HDDs report as
`DriveType.Fixed`, so it would reject the most common backup medium. Volume type is used only to
pick a sensible starting folder in the picker.

### 4. Restore
- Source: `<backupRoot>\<id>\`, the same destination the backups were written to.
- List the backups present for that game, newest first, by timestamp and size.
- User picks one; cancelling the selection restores nothing.
- **Before anything is written, the current save directory is backed up automatically.** This is an
  ordinary backup written to the backup folder with a `-pre-restore` suffix, so it appears in the
  restore list like any other. If that safety backup fails, the restore aborts and nothing changes.
- The user confirms an explicit prompt naming the save directory, the safety backup that was just
  taken, and **the direction of the change**: when the current save data is newer than the selected
  backup, the prompt says so with the elapsed time ("your saves have changed in the last 2 days —
  restoring this backup undoes that"). Declining changes nothing: the save directory is untouched
  and the safety backup written for the declined restore is removed again.
- The archive is extracted into a temporary sibling directory, the old save directory is renamed
  aside (never recursively deleted — a locked file partway through a delete would leave a mixture
  of old and new content), and the new one is renamed into its place. The save directory is
  therefore never left half-written, and afterwards it holds exactly the archive contents — no
  leftovers from the previous save set mixed in with the restored one. If that final rename fails
  (the save folder is in use), the old directory is renamed straight back rather than left aside
  under a temporary name, so a failed restore leaves the save directory exactly as it was.
- Unreachable `backupRoot`, no backups for that game, or a corrupt archive: distinct visible error,
  save directory untouched.
- Each entry in the restore list has a delete button that removes exactly that one archive after a
  confirmation. It is the only way the app deletes a backup; there is no automatic pruning.

Deleting the current save set is only acceptable because the step before it put that set on the
backup destination. The safety backup is what makes "exactly the archive contents" a safe promise.

## UI (v1, minimal)
- One list of all games from `games.json`
- Per row: name, a merged Status cell (plain-language comparison plus relative time of the newest
  backup / last play, exact timestamp on hover), buttons: Backup, Restore, and an overflow for
  Open save folder / Change save folder. File count and size remain as their own columns; last
  modified does not — it lives in the Status cell's relative-time subline
- Save-directory status is gathered on a background thread as the window opens; rows appear
  immediately and fill themselves in. Startup is never blocked by a slow or absent volume
- Window actions: Add game, Scan, Refresh, Open the games list, Change backup folder, Back up all
  changed, Back up selected, Restore selected, Switch theme (light/dark for the session) and About
  — the rightmost button, opening a small window with the app name, version, build date and a link
  to the user guide (the offline copy, falling back to the same guide on GitHub Pages when no
  offline copy is installed next to the executable). The header shows the catalog file name (full
  path in the tooltip) and the current backup folder path, since every error message in the app
  names one of the two
- Each row has a checkbox (with a select-all header) feeding the two "selected" actions, and a search
  box above the list filters rows by name without changing which ones are ticked
- Row buttons never depend on installation status; Restore is disabled only while `backupRoot` is
  unset or unreachable
- The window refuses to close while a backup, restore or bulk backup is running; it shows an info
  message and the user waits rather than the process being torn down mid-write
- No onboarding, no settings screen in v1

## Error Handling
- All filesystem operations: guarded, errors shown as MessageBox or status line, app must never crash
- No silent failures – every failed action must give visible feedback
- Every operation that touches the filesystem runs off the UI thread, with a visible busy state

## Non-Goals (explicitly out of scope)
- **Launching games** — the Steam library, the Start menu and the desktop already do this. Keeping it
  would require the whole registry layer for one redundant button.
- **Uninstalling games** — Windows' own Apps & Features does this. Removed 2026-07-24.
- **Installation detection / registry scanning** — nothing left in the product needs it.
- Automatic detection of unknown games
- Cloud backup and cloud sync
- Backup management beyond the restore window (listing a game's backups and deleting a single
  archive there are part of restore; automatic pruning or retention rules are not)
- Scheduled or continuous automatic backup (the startup reminder only asks; it never backs up by itself)
- Support for other operating systems
- Save data encryption — plain ZIP keeps manual recovery possible, which is the point of the backup

## Known limits of the promise
- Restoring a save on a *different* machine or account can fail at the game's own level: several
  titles bind saves to a Steam ID or checksum them (Elden Ring keeps saves under
  `%APPDATA%\EldenRing\<steamid64>\`). SavegameBunker restores the bytes faithfully; whether the game
  accepts them is outside its control. Same-machine reinstall is the case that always works.
- A backup stored on the same disk as the saves does not survive that disk failing. SavegameBunker states
  that limit here rather than at runtime; it is the user's trade-off to make, and it still covers the
  more common ways save data is lost.
- The known-games table is a hand-curated snapshot, not a maintained database. Entries can be wrong
  on a given machine (different store, modded install, relocated folder) and go stale as games change
  where they save. It is a shortcut past typing a path, and the dialog's resolve-and-report step is
  what keeps a wrong entry from becoming a wrong backup.
- A game writing to its save directory during a backup can produce an inconsistent archive. Locked
  files surface as a visible backup failure rather than a silent one, but the user is expected to
  close the game first.
- The save-vs-backup comparison uses local wall-clock times: the save directory's modification time
  and the timestamp in the backup's file name. Backups taken on another machine, or across a system
  clock change, can therefore compare wrongly. The comparison informs a decision; it never gates one,
  and every restore is preceded by a safety backup regardless of what it says.
- A save folder that contains a reparse point (a junction or symlink) is not followed into: the
  empty-check, the status scanner and the archive writer all skip a directory reparse point, and a
  *file* reparse point only when `FileSystemInfo.LinkTarget` proves it is an actual symbolic link.
  An all-junction save folder reads as empty and is refused. A mixed folder is backed up without
  the junction's target — so a link sitting next to the saves cannot silently pack Documents or SSH
  keys into the zip. A OneDrive Files On-Demand placeholder file also sets the reparse-point
  attribute but is not a symlink, so it is backed up like any other file — the OS transparently
  downloads it on read. Restore of an older archive that *did* follow junctions (built before this
  rule) still extracts whatever that archive holds.
- A restore whose archive declares more than 100,000 files or 32 GB uncompressed is refused before
  extraction. That cap is a zip-bomb guard, not a size limit on legitimate saves; SavegameBunker's own
  backups of a normal save folder sit far under it. .NET's `ExtractToDirectory` also refuses
  entries that would write outside the extraction folder (zip slip). Backup itself enforces no such
  cap — it never refuses to protect data because it is large — but a just-written archive that
  exceeds Restore's own caps gets a warning appended to its success message, since an archive this
  app can never restore back is worth flagging when it is created rather than when a later restore
  refuses it.
- A games list file or backup zip from someone else is untrusted input. SavegameBunker will not
  shell-execute a path that looks like a program (`.exe`, `.bat`, shortcuts, …) when you click Open.
  It will restore bytes from a zip you pick. Don't run SavegameBunker against a catalog or backup folder
  you would not open in Explorer.
- Backup verification (write side) and restore verification (read side) both compare only entry names
  and uncompressed lengths, never a checksum: .NET 10 does not validate an entry's CRC-32 on read (that
  arrived in .NET 11). A bit flip inside an otherwise structurally valid archive passes verification
  on both sides. The threat this guards against — an incomplete or truncated write — is covered;
  silent bit rot on the storage medium is not.
- High Contrast support covers only dark contrast schemes: WPF-UI's `HighContrast` theme applies a
  single dark dictionary, so a user on a light contrast scheme still gets SavegameBunker's dark
  window rather than the OS palette. Adopting the light case properly means replacing the shell
  library's high-contrast dictionary rather than tinting it — out of scope for v1.
- The cross-process save guard (a catalog write refuses when the file's own last-write time no longer
  matches what this session last read, so a second running copy of SavegameBunker can't silently discard
  the first's change) compares a filesystem timestamp, not content. Two consequences follow from
  that choice: on a volume with coarser time resolution than NTFS — FAT32, the format most USB
  sticks ship pre-formatted with, records to 2 seconds — two processes that each load and then save
  within the same 2-second window can still collide undetected, because both see the identical
  stamp. And a background process that touches the file's timestamp without changing its meaning
  (a sync client, a search indexer) can trip the guard into refusing a save that was never actually
  in conflict. Both are the honest edges of a timestamp-based check; a content hash would close the
  first without helping the second, and closing both fully would need either a real file lock held
  for the session's lifetime or a merge strategy, neither of which fits a single-file, no-daemon tool.
