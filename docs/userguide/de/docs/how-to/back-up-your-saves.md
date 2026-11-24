# Spielstände sichern

## Alles sichern, was sich geändert hat

Klicken Sie in der Symbolleiste auf **Back up all changed**. SavegameBunker sichert jedes Spiel, dessen
**Status**-Spalte anzeigt, dass es seit der letzten Sicherung ungesicherte Änderungen gibt, und
jedes Spiel, das noch nie gesichert wurde. Bereits aktuelle Spiele werden übersprungen.

## Bestimmte Spiele sichern

1. Markieren Sie die Kontrollkästchen der gewünschten Zeilen, oder nutzen Sie das Kontrollkästchen
   in der Kopfzeile, um alle sichtbaren Spiele auf einmal zu markieren oder zu entmarkieren.
2. Klicken Sie auf **Back up**.

Nur die markierten Spiele werden gesichert, unabhängig von ihrem Status.

## Ein einzelnes Spiel sichern

Klicken Sie auf **Back up** in der Zeile dieses Spiels. Das funktioniert unabhängig davon, ob das
Spiel ausgewählt ist.

## Fortschritt beobachten

Bei großen Sicherungen füllt sich ein Fortschrittsbalken entsprechend der tatsächlich
geschriebenen Bytes, statt nur zu rotieren. Vor dem Start einer Sicherung zeigt SavegameBunker einen
Hinweis zum freien Speicherplatz, der den freien Platz im Sicherungsordner mit der zu kopierenden
Datenmenge vergleicht. Das ist rein informativ — es blockiert oder warnt niemals vor einer
Sicherung, ein knapper Sicherungsordner hält Sie also nicht auf.

## Tastenkombination

**Ctrl+B** sichert die gerade fokussierte Zeile — ganz ohne Maus.

## Wenn noch kein Sicherungsordner festgelegt ist

Beim ersten Sichern fragt SavegameBunker, wohin gesichert werden soll, und merkt sich die Antwort.
Siehe [Den Sicherungsordner ändern](change-the-backup-folder.md).
