# Einen Spielstand wiederherstellen

## Ein einzelnes Spiel wiederherstellen

Klicken Sie auf **Restore** in der Zeile dieses Spiels. Es öffnet sich das Fenster
**Restore — _Spielname_** mit allen Sicherungen für dieses Spiel, neueste zuerst. Wählen Sie eine
aus. SavegameBunker geht dann so vor:

1. Schreibt zuerst eine Sicherheitskopie Ihrer **aktuellen** Spielstände.
2. Entpackt die gewählte Sicherung in einen temporären Ordner neben Ihren Spielständen.
3. Benennt den alten Spielstand-Ordner beiseite.
4. Benennt den neuen Ordner an dessen Stelle und entfernt danach die beiseitegelegte Kopie.

Diese Reihenfolge ist wichtig: Ihre aktuellen Spielstände werden erst gelöscht, nachdem bereits
eine Sicherheitskopie davon im Sicherungsordner geschrieben wurde.

Vor Schritt 2 fragt SavegameBunker nach einer Bestätigung. Wählen Sie dort **Cancel**, ändert sich
nichts: Ihre Spielstände bleiben, wie sie sind, und die Sicherheitskopie aus Schritt 1 wird wieder
entfernt.

SavegameBunker stellt kein Spiel wieder her, dessen Spielstand-Ordner Ihren Sicherungsordner enthält —
eine Wiederherstellung ersetzt den gesamten Spielstand-Ordner und würde die Sicherungen mit
löschen. Verlegen Sie den Sicherungsordner vorher an einen anderen Ort
([Sicherungsordner ändern](change-the-backup-folder.md)).

Falls die Spielstand-Dateien auf der Festplatte neuer sind als die gewählte Sicherung, warnt
SavegameBunker vor dem Fortfahren — Sie könnten gerade aktuelleren Fortschritt überschreiben. Diese
Prüfung vergleicht Dateizeitstempel, nicht den Inhalt, sodass eine falsch eingestellte Systemuhr
diese Warnung gelegentlich auslösen kann, obwohl eigentlich alles in Ordnung ist.

## Ein altes Backup löschen

Jede Zeile im Wiederherstellen-Fenster hat eine Lösch-Schaltfläche. Nach einer Bestätigung wird
genau dieses eine Archiv aus dem Sicherungsordner entfernt — die ZIP-Datei auf der Festplatte
wird gelöscht, nicht nur der Listeneintrag. Das ist endgültig. Sonst wird nichts angerührt:
andere Sicherungen des Spiels und die Spielstände des Spiels bleiben, wie sie sind.

Die Lösch-Schaltfläche ist deaktiviert, solange in diesem Fenster eine Wiederherstellung läuft.

## Mehrere Spiele auf einmal wiederherstellen

1. Markieren Sie die gewünschten Spiele.
2. Klicken Sie auf **Restore selected**.

SavegameBunker stellt für jedes markierte Spiel die jeweils neueste Sicherung wieder her, eine nach
der anderen, und fragt für jede einzeln um Bestätigung. Das Bestätigen eines Spiels startet nicht
im Stillen das nächste — Sie behalten bei jedem einzelnen Spiel die Kontrolle.

## Tastenkombination

**Ctrl+R** stellt die gerade fokussierte Zeile wieder her.

## Spielstände zwischen PCs oder Konten verschieben

Die Wiederherstellung auf **demselben PC und demselben Konto**, auf dem die Sicherung erstellt
wurde, funktioniert immer. Ein auf einem anderen PC oder einem anderen Steam-Konto
wiederhergestellter Spielstand kann vom Spiel selbst abgelehnt werden — viele Spiele binden
Spielstände an eine Steam-ID oder eine Prüfsumme. SavegameBunker legt die Dateien exakt so zurück, wie
sie gesichert wurden; es kann das Spiel nicht dazu bringen, sie zu akzeptieren. Siehe
[Umzug auf einen neuen PC](move-to-a-new-pc.md) für Ansätze, die einen Versuch wert sind.
