using System.Text.Json.Serialization;

namespace SavegameBunker.Models;

/// <summary>
/// The whole contents of a catalog file: where backups go, and which games exist.
/// </summary>
/// <remarks>
/// The backup destination lives here rather than in a separate settings file because it belongs
/// to the catalog, not to the machine — pointing the application at another catalog is meant to
/// bring that catalog's destination along with it.
/// </remarks>
public sealed class Catalog
{
    /// <summary>
    /// Destination folder for all backups. Absolute, or relative to the catalog file itself so
    /// that an executable and its catalog on a removable drive survive a changing drive letter.
    /// Null means "not configured yet" — the first backup asks for one.
    /// </summary>
    [JsonPropertyName("backupRoot")]
    public string? BackupRoot { get; init; }

    [JsonPropertyName("games")]
    public List<Game> Games { get; init; } = [];
}
