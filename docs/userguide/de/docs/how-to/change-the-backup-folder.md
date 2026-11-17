# Den Sicherungsordner ändern

Beim ersten Sichern fragt SavegameBunker, wohin gesichert werden soll — das ist der
**backup folder** (Sicherungsordner). Sie können einen USB-Stick, eine externe Festplatte, eine
Netzwerkfreigabe oder sogar dieselbe Festplatte wählen, auf der Ihre Spielstände schon liegen. Was
immer Sie wählen, wird ohne Rückfrage akzeptiert — SavegameBunker verweigert nie, leitet nie um und
verlegt eine Sicherung nie heimlich, egal wohin das Ziel zeigt.

SavegameBunker merkt sich die Wahl und prüft beim Start, ob der Sicherungsordner noch erreichbar ist,
statt es erst beim Sichern oder Wiederherstellen zu melden. In der Kopfzeile steht immer
**Backup folder: _Pfad_**, oder **Backup folder: not set**, falls noch keiner gewählt wurde.

## Warum der Sicherungsordner wichtig ist

Eine Sicherung auf der **gleichen Festplatte** wie Ihre Spielstände schützt Sie davor, die
Spielstand-Dateien selbst versehentlich zu löschen oder zu überschreiben, geht aber mit dieser
Festplatte verloren, falls das Laufwerk ausfällt. Wenn der Sinn der Sicherung ist, einen
Laufwerksausfall zu überstehen, wählen Sie ein anderes Laufwerk — eine externe Festplatte, eine
zweite interne Platte oder eine Netzwerkfreigabe.

## Den Sicherungsordner später ändern

Klicken Sie in der Symbolleiste auf **Change backup folder**. Es öffnet sich ein Ordnerauswahl-
Dialog mit dem Titel **Select the folder SavegameBunker writes backups to**. Vorhandene Sicherungen
bleiben, wo sie sind; nur neue Sicherungen landen am neuen Ort.

## Was SavegameBunker hier nicht tut

- Es sichert nicht automatisch — Sie entscheiden, wann Sie auf **Back up all changed** klicken.
  Einzige Erinnerung ist eine Frage beim Start, wenn Spiele ungesicherte Änderungen haben.
- Es ist keine Cloud-Synchronisierung und kein Steam Cloud. Es kopiert genau den Spielstand-Ordner,
  auf den Sie zeigen, in den gewählten Ordner, und sonst nichts.
