using System.Text.Json.Serialization;

namespace SavegameBunker.Models;

/// <summary>
/// One row of the bundled known-games table: a display name, its aliases, and the
/// <c>saveDirTemplate</c> that fills the Add Game dialog when the user picks it.
/// Shape mirrors <see cref="Game"/> — same <c>init</c>-only, <c>JsonPropertyName</c> style.
/// The id is stored, not derived at runtime: it names the folder holding the user's backups, and
/// deriving it from an editable display string would change that folder's name if the table entry
/// were ever renamed.
/// </summary>
public sealed class KnownGame
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; init; } = new();

    [JsonPropertyName("saveDirTemplate")]
    public string SaveDirTemplate { get; init; } = "";
}
