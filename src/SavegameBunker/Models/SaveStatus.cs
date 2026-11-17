namespace SavegameBunker.Models;

/// <summary>
/// Which of the four states a row's save directory scan landed in.
/// </summary>
public enum SaveStatusKind
{
    /// <summary>The background scan for this row has not reported yet.</summary>
    Checking,

    /// <summary>The directory exists and holds at least one file.</summary>
    Found,

    /// <summary>The directory is missing or exists but is empty.</summary>
    NoSaves,

    /// <summary>The catalog's <c>saveDirTemplate</c> could not be resolved to a path at all.</summary>
    PathError,
}

/// <summary>
/// Result of scanning one game's save directory.
/// </summary>
/// <remarks>
/// <see cref="SaveStatusKind.NoSaves"/> and <see cref="SaveStatusKind.PathError"/> are kept
/// deliberately distinct: a template that cannot be resolved at all — an unknown
/// %VARIABLE%, an empty string — is a configuration mistake the user needs to fix in the catalog
/// entry. A resolvable template whose directory is missing or empty just means no save data exists
/// there yet, which is a normal, unremarkable state. The two read as opposite next steps, so
/// collapsing them into one "no saves" bucket would hide the one case that actually needs action.
/// </remarks>
public sealed record SaveStatus(
    SaveStatusKind Kind,
    long FileCount,
    long TotalBytes,
    DateTime? LastModified,
    bool Oversized,
    string? Error)
{
    public static SaveStatus Checking() =>
        new(SaveStatusKind.Checking, 0, 0, null, false, null);

    public static SaveStatus Found(long fileCount, long totalBytes, DateTime? lastModified, bool oversized) =>
        new(SaveStatusKind.Found, fileCount, totalBytes, lastModified, oversized, null);

    public static SaveStatus NoSaves() =>
        new(SaveStatusKind.NoSaves, 0, 0, null, false, null);

    public static SaveStatus PathError(string error) =>
        new(SaveStatusKind.PathError, 0, 0, null, false, error);
}
