namespace SavegameBunker.Models;

/// <summary>
/// Passed to the confirmation delegate <see cref="Services.RestoreService.Restore"/> invokes after
/// the pre-restore safety backup step and before anything about the live save directory changes.
/// </summary>
/// <param name="SaveDir">The resolved save directory the restore is about to replace.</param>
/// <param name="SafetyBackupPath">
/// Absolute path of the safety backup just written, or null when the save directory was missing or
/// held no files. A folder holding nothing has nothing to protect, so no archive is written for it
/// and the confirmation says so instead of naming a file that does not exist.
/// </param>
public sealed record RestorePrompt(string SaveDir, string? SafetyBackupPath);
