using SavegameBunker.Models;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Pins the Find Games row's file-count text: singular for one file, and read from the string
/// resource like every other user-facing sentence.
/// </summary>
public sealed class ScanHitRowViewModelTests
{
    private static ScanHitRowViewModel Row(int fileCount) =>
        new(new ScanHit(new KnownGame { Id = "game", DisplayName = "Game" }, "C:/saves", fileCount, 100));

    [Fact]
    public void FileCountText_is_singular_for_one_file() =>
        Assert.Equal("1 file", Row(1).FileCountText);

    [Fact]
    public void FileCountText_is_plural_for_several_files() =>
        Assert.Equal("3 files", Row(3).FileCountText);
}
