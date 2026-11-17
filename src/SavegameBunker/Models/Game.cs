using System.Text.Json.Serialization;

namespace SavegameBunker.Models;

/// <summary>
/// One entry from the catalog file. Immutable definition data.
/// Nothing here depends on whether the game is installed — save data outlives an
/// uninstall, so backup and restore must work either way.
/// </summary>
public sealed class Game
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("saveDirTemplate")]
    public string SaveDirTemplate { get; init; } = "";
}
