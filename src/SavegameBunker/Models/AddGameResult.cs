namespace SavegameBunker.Models;

/// <summary>
/// What the Add Game dialog hands back to <c>MainViewModel</c> when the user confirms.
/// Validation, id derivation and reverse-templating have already happened inside the dialog's
/// own view model — this is a plain data carrier.
/// </summary>
public sealed record AddGameResult(string Id, string DisplayName, string SaveDirTemplate);
