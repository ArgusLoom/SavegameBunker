using System.Text.Json.Serialization;

namespace SavegameBunker.Models;

/// <summary>
/// One entry from the catalog file. Its properties are init-only, so an entry is never edited in
/// place; a change replaces the entry in <see cref="Catalog.Games"/>, which is itself mutable.
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
