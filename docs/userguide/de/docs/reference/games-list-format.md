# Format der Spieleliste

Alles, was SavegameBunker weiß — welche Spiele verfolgt werden und wohin Sicherungen gehen — steht in
einer einzigen JSON-Datei, `games.json`, neben der `.exe`. SavegameBunker nennt diese Datei Ihre
**games list** (Spieleliste); die Oberfläche bearbeitet sie für Sie, über **Add game**,
**Change save folder** und die anderen Dialoge. Manuelles Bearbeiten funktioniert ebenfalls, aber
Kommentare, die Sie von Hand hinzufügen, überleben nicht die nächste Nutzung von **Add game** über
die Oberfläche.

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

## Felder

| Feld | Bedeutung |
|---|---|
| `backupRoot` | Der Sicherungsordner. Ein relativer Pfad wird relativ zur Spieleliste-Datei aufgelöst — das macht einen portablen USB-Stick-Aufbau über mehrere Rechner hinweg möglich. |
| `games[].id` | Eine stabile interne Kennung für das Spiel. Ändern Sie diese bei einem vorhandenen Eintrag nicht — darüber ordnet SavegameBunker ein Spiel seinen vorhandenen Sicherungen zu. |
| `games[].displayName` | Der im SavegameBunker-Fenster angezeigte Name — der **Name**, den Sie im Dialog Add game eingegeben haben. |
| `games[].saveDirTemplate` | Der Pfad zum Spielstand-Ordner, mit optionalen Platzhaltervariablen (unten). |

## Pfad-Platzhalter

`saveDirTemplate` akzeptiert diese Platzhalter sowie Schrägstriche, damit Sie beim manuellen
Bearbeiten keine Backslashes verdoppeln müssen:

- `%DOCUMENTS%` — der Dokumente-Ordner des Benutzers. Bevorzugen Sie dies gegenüber einem fest
  codierten `%USERPROFILE%\Documents`, da die Windows-Funktion "Bekannte Ordner verschieben" (oft
  mit OneDrive genutzt) den Dokumente-Ordner aus dem Benutzerprofil heraus verlegen kann.
- `%SAVEDGAMES%`
- `%APPDATA%`
- `%LOCALAPPDATA%`
- `%USERPROFILE%`

## Die Spieleliste öffnen

Klicken Sie in der Symbolleiste auf **Open the games list**, um `games.json` direkt zu öffnen,
wenn Sie die Datei selbst einsehen oder bearbeiten möchten.

## Eine andere Spieleliste verwenden

Übergeben Sie den Pfad einer anderen Spieleliste als Kommandozeilenargument, um SavegameBunker damit
statt mit der Standard-`games.json` zu starten:

```powershell
SavegameBunker.exe D:\retro-games.json
```

Ein relatives `backupRoot` in dieser Datei wird relativ zu `D:\retro-games.json` aufgelöst, nicht
relativ zur `.exe`. Dadurch können eine Spieleliste und ihre Sicherungen gemeinsam auf einem
portablen Laufwerk liegen.

## Fehlend vs. fehlerhaft

- **Keine Spieleliste gefunden**: SavegameBunker schreibt eine neue Vorlagendatei und startet mit
  einer leeren Liste.
- **Spieleliste vorhanden, aber nicht lesbar**: SavegameBunker meldet den Fehler und lässt die Datei
  unverändert. Eine möglicherweise von Hand bearbeitete Datei wird niemals überschrieben, selbst
  wenn sie nicht gelesen werden kann.
