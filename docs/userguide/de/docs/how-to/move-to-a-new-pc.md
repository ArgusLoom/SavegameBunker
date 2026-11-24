# Umzug auf einen neuen PC

SavegameBunker hat kein Konto und keine Cloud-Synchronisierung, daher bedeutet "Umzug" das manuelle
Kopieren von Dateien. Dabei geht es um zwei Dinge: die **Spieleliste** (`games.json` — welche
Spiele Sie verfolgen und wo deren Spielstände liegen) und die **Sicherungen** (die eigentlichen
Spielstand-Daten).

## Der portable Weg: eine Spieleliste direkt auf dem USB-Stick

Legen Sie `games.json` und Ihre Sicherungen auf demselben USB-Stick oder derselben externen
Festplatte ab und geben Sie SavegameBunker ein relatives `backupRoot` (siehe
[das Format der Spieleliste](../reference/games-list-format.md)). Ein relatives `backupRoot`
wird relativ zu der Spieleliste-Datei aufgelöst, in der es definiert ist, nicht relativ zur
`.exe` — so funktioniert derselbe Stick unabhängig davon, ob er im alten oder im neuen PC steckt.

Auf dem neuen PC:

1. Kopieren Sie `SavegameBunker.exe` ebenfalls dorthin (oder lassen Sie sie auf dem Stick neben der
   Spieleliste).
2. Starten Sie die App mit dem Pfad der Spieleliste als Kommandozeilenargument:
   `SavegameBunker.exe D:\games.json`.
3. Nutzen Sie **Restore** für jedes Spiel, das Sie auf dem neuen Rechner einrichten möchten.

## Der manuelle Weg

1. Stellen Sie auf dem alten PC sicher, dass jedes wichtige Spiel eine aktuelle Sicherung hat.
2. Kopieren Sie den Sicherungsordner auf den neuen PC (oder auf ein Laufwerk, das der neue PC
   lesen kann).
3. Starten Sie SavegameBunker auf dem neuen PC und nutzen Sie **Add game**, um jeden Eintrag neu
   anzulegen, mit dem Spielstand-Pfad auf dem neuen Rechner.
4. Klicken Sie auf **Backup folder**, zeigen Sie auf den kopierten Ordner, und stellen Sie
   dann jedes Spiel mit **Restore** wieder her.

## Ob das Spiel den Spielstand akzeptiert

SavegameBunker stellt die Dateien exakt so wieder her, wie sie gesichert wurden. Ob das **Spiel**
selbst sie auf einem neuen PC akzeptiert, hängt vom Spiel ab, nicht von SavegameBunker:

- Gleiches Steam-Konto, gleicher PC: funktioniert immer.
- Gleiches Steam-Konto, anderer PC: funktioniert meist.
- Anderes Steam-Konto oder ein anderer Store (z. B. Steam → GOG): wird oft vom Spiel selbst
  abgelehnt, da viele Spiele Spielstände an eine Konto-ID oder eine Prüfsumme binden. Dagegen
  kann SavegameBunker nichts tun — es legt die Dateien zurück, und das Spiel entscheidet, ob es sie
  verwendet.
