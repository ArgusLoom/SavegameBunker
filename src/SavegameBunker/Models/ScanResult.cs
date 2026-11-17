namespace SavegameBunker.Models;

/// <summary>
/// One known-games table entry found to hold save data on this machine.
/// </summary>
/// <param name="Game">
/// The table entry that matched. Its stored <c>Id</c> is used verbatim if the hit is added to the
/// catalog — never re-derived from <c>DisplayName</c> (the rule for a picked suggestion applies
/// here too).
/// </param>
/// <param name="ResolvedPath">The absolute directory <see cref="Game"/>'s template resolved to.</param>
/// <param name="FileCount">
/// Files found recursively under <see cref="ResolvedPath"/>. <c>int</c> here rather than
/// <see cref="Models.SaveStatus"/>'s <c>long</c>: a table entry has always been probed exactly
/// once by the time this is built, so there is no accumulation across multiple scans to overflow.
/// </param>
/// <param name="TotalBytes">Total size of every file counted in <see cref="FileCount"/>.</param>
public sealed record ScanHit(KnownGame Game, string ResolvedPath, int FileCount, long TotalBytes);

/// <summary>
/// Outcome of one run of <see cref="Services.KnownGamesScanner.Scan"/>.
/// </summary>
/// <param name="Hits">
/// Every table entry that resolved to an existing, non-empty directory not already accounted for.
/// Sorted by display name, so the window's list order does not depend on the bounded-parallel
/// probe's completion order.
/// </param>
/// <param name="SkippedShared">
/// Table entries excluded because their <c>saveDirTemplate</c> is shared by more than one entry —
/// carried as a list (not just a count) because the window's <c>[Details]</c> disclosure names
/// them. Sorted by <c>DisplayName</c>, ties broken by <c>Id</c>, so the disclosure's order does
/// not depend on the table's own order. Excluded ahead of every other rule
/// <see cref="Services.KnownGamesScanner.Scan"/> applies.
/// </param>
/// <param name="SkippedAlreadyKnown">
/// How many table entries were excluded because their id or resolved path was already in the
/// catalog. A count, not a list: there is nothing actionable to show for an entry the catalog
/// already has, unlike <see cref="SkippedShared"/> which the window lets the user inspect. An
/// entry that is both shared and already known is counted only in <see cref="SkippedShared"/>,
/// since that exclusion runs first.
/// </param>
public sealed record ScanResult(
    IReadOnlyList<ScanHit> Hits,
    IReadOnlyList<KnownGame> SkippedShared,
    int SkippedAlreadyKnown);
