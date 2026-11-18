using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Pins <see cref="PathCompare"/> against the drive-root bug (ITEM-4): a normalized drive root
/// keeps its trailing separator, and <see cref="PathCompare.IsSameOrInside"/> must not append a
/// second one when testing containment — the exact mistake the old
/// <c>BackupService.IsSameOrInside</c> made (<c>root + '\\'</c> became <c>E:\\</c>, so nothing was
/// ever "inside" a drive root). Every row here is a pure string comparison; nothing touches disk.
/// </summary>
public sealed class PathCompareTests
{
    [Theory]
    [InlineData(@"E:\Saves\X", @"E:\")]
    [InlineData(@"E:\Saves", @"E:\")]
    [InlineData(@"E:\", @"E:\")]
    [InlineData(@"E:\Saves\X", @"E:\Saves")]
    [InlineData(@"E:\Saves\", @"E:\Saves")]
    [InlineData(@"e:\saves\x", @"E:\Saves")]
    [InlineData(@"E:/Saves/X", @"E:\Saves")]
    [InlineData(@"\\server\share\saves", @"\\server\share")]
    [InlineData(@"\\server\share\saves", @"\\server\share\")]
    public void IsSameOrInside_holds_for_a_folder_and_everything_below_it(string candidate, string parent)
    {
        Assert.True(PathCompare.IsSameOrInside(candidate, parent));
    }

    [Theory]
    [InlineData(@"E:\xy", @"E:\x")]
    [InlineData(@"E:\", @"E:\Saves")]
    [InlineData(@"F:\Saves", @"E:\")]
    [InlineData(@"E:\Saves", @"E:\Saves\X")]
    [InlineData(@"\\server\share2", @"\\server\share")]
    public void IsSameOrInside_rejects_siblings_parents_and_prefix_lookalikes(string candidate, string parent)
    {
        Assert.False(PathCompare.IsSameOrInside(candidate, parent));
    }

    [Theory]
    [InlineData(@"E:\", @"E:\")]
    [InlineData(@"E:\Saves\", @"E:\Saves")]
    [InlineData(@"E:/Saves", @"E:\Saves")]
    public void Normalize_keeps_a_drive_root_and_trims_any_other_trailing_separator(string path, string expected)
    {
        Assert.Equal(expected, PathCompare.Normalize(path));
    }
}
