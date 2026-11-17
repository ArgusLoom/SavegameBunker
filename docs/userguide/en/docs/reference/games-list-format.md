# Games list file format

Everything SavegameBunker knows — which games to track and where to put backups — lives in one JSON
file, `games.json`, next to the `.exe`. SavegameBunker calls this file your **games list**; the UI
edits it for you, through **Add game**, **Change save folder**, and the rest. Hand-editing works
too, but comments you add by hand won't survive the next time you use **Add game** through the
UI.

```json
{
  "backupRoot": "SavegameBunkerBackups",
  "games": [
    {
      "id": "witcher3",
      "displayName": "The Witcher 3: Wild Hunt",
      "saveDirTemplate": "%DOCUMENTS%/The Witcher 3/gamesaves"
    }
  ]
}
```

## Fields

| Field | Meaning |
|---|---|
| `backupRoot` | The backup folder. A relative path resolves next to the games list file, which is what makes a portable USB-stick setup work across machines. |
| `games[].id` | A stable internal identifier for the game. Don't change this on an existing entry — it's how SavegameBunker matches a game to its existing backups. |
| `games[].displayName` | The name shown in the SavegameBunker window — the **Name** you typed in Add game. |
| `games[].saveDirTemplate` | The save folder's path, with optional placeholder variables (below). |

## Path placeholders

`saveDirTemplate` accepts these placeholders, plus forward slashes so you don't have to double up
backslashes when editing by hand:

- `%DOCUMENTS%` — the user's Documents folder. Prefer this over a hardcoded
  `%USERPROFILE%\Documents`, because Windows' Known Folder Move (often used with OneDrive) can
  relocate Documents outside the user profile.
- `%SAVEDGAMES%`
- `%APPDATA%`
- `%LOCALAPPDATA%`
- `%USERPROFILE%`

## Opening the games list

Click **Open the games list** in the toolbar to open `games.json` directly, if you want to
inspect or hand-edit it yourself.

## Using a different games list

Pass a different games list's path on the command line to run SavegameBunker against it instead of the
default `games.json`:

```powershell
SavegameBunker.exe D:\retro-games.json
```

A relative `backupRoot` in that file resolves next to `D:\retro-games.json`, not next to the
`.exe`. This is what lets a games list and its backups live together on a portable drive.

## Absent vs. malformed

- **No games list found**: SavegameBunker writes a fresh template file and starts with an empty list.
- **Games list exists but can't be parsed**: SavegameBunker reports the error and leaves the file
  untouched. It will never overwrite a file you may have hand-edited, even if it can't read it.
