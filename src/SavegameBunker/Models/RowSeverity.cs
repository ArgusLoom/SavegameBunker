namespace SavegameBunker.Models;

/// <summary>
/// The three states a game-list cell can carry — one value per cell rather than a set of
/// independent flags that could contradict each other. A row's Game cell and Status cell each
/// carry their own <see cref="RowSeverity"/>, since a row can be, for instance, oversized on the
/// save side while its backup comparison is perfectly fine.
/// </summary>
public enum RowSeverity
{
    /// <summary>Nothing to flag — the default state for both cells.</summary>
    Normal,

    /// <summary>A recoverable condition worth the player's attention, not a failure.</summary>
    Warning,

    /// <summary>A condition that blocks the action this cell reports on.</summary>
    Error,
}
