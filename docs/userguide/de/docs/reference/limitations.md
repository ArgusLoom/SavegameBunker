# Was SavegameBunker nicht tut

Das sind bewusste Entscheidungen, keine für später geplanten fehlenden Funktionen.

- **Keine automatische Sicherung.** Sie klicken auf **Back up all changed** (oder **Back up** pro
  Zeile), wenn Sie eine Kopie möchten. SavegameBunker sichert niemals von selbst; es fragt nur einmal
  beim Start, wenn Spiele ungesicherte Änderungen haben.
- **Eine Sicherung auf derselben Festplatte wie Ihre Spielstände geht mit dieser Festplatte
  verloren.** Wenn der Sinn der Sicherung ist, einen Laufwerksausfall zu überstehen, wählen Sie
  ein anderes Laufwerk.
- **Kein Launcher.** SavegameBunker startet keine Programme über **Open save folder** — es öffnet den
  Ordner im Explorer, nicht mehr. Behandeln Sie eine von jemand anderem erhaltene Spieleliste
  oder einen Sicherungsordner wie jede andere nicht vertrauenswürdige Datei.
- **Keine Cloud-Synchronisierung und kein Steam Cloud.** Es kopiert genau den Spielstand-Ordner,
  auf den Sie zeigen, in den gewählten Ordner, und sonst nichts.
- **Die eingebaute Spiele-Bibliothek ist kuratiert, nicht Crowd-sourced.** Ihre Spielstand-Pfade
  werden von Hand geprüft und mit der App ausgeliefert — kein Konto, keine Cloud, kein
  Wiki-Rauschen. Der Preis der Kuratierung: Es gibt keine Online-Aktualisierung, Einträge
  veralten also, sobald Spiele aktualisiert werden oder ihren Spielstand-Ordner verschieben, und
  Scan findet ausschließlich Spiele aus der Bibliothek; alles andere müssen Sie von Hand
  hinzufügen. Wenn Scan den falschen Store-Pfad wählt oder ein Spiel seinen Speicherort geändert
  hat, nutzen Sie [Add a game](../how-to/add-a-game.md) oder **Change save folder** in der Zeile
  dieses Spiels.
- **Greift nicht auf die Windows-Registrierung zu.** Nichts in SavegameBunker liest Installationsorte
  oder Deinstallationsbefehle — jede Aktion funktioniert unabhängig davon, ob ein Spiel auf diesem
  PC überhaupt installiert ist.
