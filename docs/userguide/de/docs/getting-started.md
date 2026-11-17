# Erste Schritte

## 1. Herunterladen und starten

[Laden Sie die neueste Version herunter](https://github.com/ArgusLoom/SavegameBunker/releases/latest),
entpacken Sie sie und starten Sie `SavegameBunker.exe`. Es muss nichts installiert werden.

Beim ersten Start wird eine Datei `games.json` (die Spieleliste) neben der `.exe` angelegt, und
SavegameBunker zeigt **No games yet** an.

## 2. Spiele finden

Klicken Sie auf **Scan**. SavegameBunker prüft seine eingebaute Bibliothek handkuratierter
Spielstand-Pfade gegen diesen PC und listet die gefundenen Spiele auf. Prüfen Sie die Liste und
fügen Sie die Spiele hinzu, die Sie verfolgen möchten.

Scan findet nicht alles — manche Spiele verwenden einen Pfad, den SavegameBunker nicht kennt, oder
speichern an ungewöhnlicher Stelle. Für diese Fälle nutzen Sie **Add game** (siehe
[Ein Spiel hinzufügen, das Scan nicht findet](how-to/add-a-game.md)).

## 3. Sichern

Klicken Sie auf **Back up all changed**, oder markieren Sie die gewünschten Spiele und klicken Sie
auf **Back up selected**.

Falls Spiele seit ihrer letzten Sicherung verändert wurden, fragt SavegameBunker beim Start einmal:
„N Spiele haben Änderungen, die noch nicht gesichert sind — jetzt sichern?" Mit „Ja" wird dieselbe Sicherung wie
**Back up all changed** gestartet; mit „Nein" passiert nichts.

Falls noch kein Sicherungsordner (**backup folder**) festgelegt ist, fragt SavegameBunker danach — ein
USB-Stick, eine externe Festplatte, eine Netzwerkfreigabe oder sogar dieselbe Festplatte. Die
Wahl wird gemerkt. Siehe [Den Sicherungsordner ändern](how-to/change-the-backup-folder.md) für die
Abwägungen.

Die Spalte **Status** zeigt für jedes Spiel an, ob sich eine Sicherung lohnt. **Refresh** prüft
den Status erneut, ohne nach neuen Spielen zu suchen.

## 4. Die Tastenkombinationen kennen

Jede Symbolleisten-Schaltfläche hat eine Alt-Tastenkombination, und **Ctrl+B** / **Ctrl+R**
sichern bzw. stellen die gerade fokussierte Zeile wieder her. Siehe die vollständige
[Referenz der Tastenkombinationen](reference/keyboard-shortcuts.md).

!!! warning "Spiel vorher schließen"
    Ein laufendes Spiel kann zu einer halb geschriebenen Sicherung führen. Sichern Sie erst, nachdem
    Sie das Spiel beendet haben, nicht während es noch läuft.

## Weitere Schaltflächen in der Symbolleiste

- **Open the games list** öffnet `games.json` direkt, für alle, die die Datei selbst einsehen
  oder bearbeiten möchten.
- **Switch theme** schaltet SavegameBunkers eigenes helles/dunkles Fensterdesign um — unabhängig von
  den in Windows eingestellten Farben.

## Nächste Schritte

- [Spielstände sichern](how-to/back-up-your-saves.md)
- [Einen Spielstand wiederherstellen](how-to/restore-a-save.md)
- [Was SavegameBunker nicht tut](reference/limitations.md)
