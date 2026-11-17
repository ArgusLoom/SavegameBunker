using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Each test gets its own temporary directory (same fixture as <see cref="GameCatalogTests"/>) so
/// the scan runs against a real filesystem tree rather than a mock.
/// </summary>
public sealed class SaveStatusScannerTests : IDisposable
{
    private readonly string _dir;

    public SaveStatusScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            // A denied-ACL subdirectory must be re-permitted level by level, before it is
            // possible to even list its own contents to recurse further. A junction must be
            // unlinked directly (non-recursive delete) rather than descended into, or recursive
            // delete tries to walk back into the tree it points at.
            TestFileSystem.RemoveDenyRules(_dir);
            TestFileSystem.RemoveJunctions(_dir);
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    private static Game GameFor(string template) =>
        new() { Id = "g", DisplayName = "G", SaveDirTemplate = template };

    private void WriteFile(string relativePath, int bytes)
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Fact]
    public void A_directory_holding_three_files_reports_found_with_their_total_size_and_newest_write_time()
    {
        WriteFile("a.dat", 10);
        WriteFile("b.dat", 20);
        Thread.Sleep(20);
        WriteFile("c.dat", 30);
        var newestWrite = File.GetLastWriteTime(Path.Combine(_dir, "c.dat"));

        var status = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(SaveStatusKind.Found, status.Kind);
        Assert.Equal(3, status.FileCount);
        Assert.Equal(60, status.TotalBytes);
        Assert.NotNull(status.LastModified);
        Assert.Equal(newestWrite, status.LastModified!.Value, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Files_in_nested_subdirectories_are_included_in_the_totals()
    {
        WriteFile("top.dat", 5);
        WriteFile(Path.Combine("nested", "deeper", "bottom.dat"), 7);

        var status = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(SaveStatusKind.Found, status.Kind);
        Assert.Equal(2, status.FileCount);
        Assert.Equal(12, status.TotalBytes);
    }

    [Fact]
    public void A_resolved_directory_that_does_not_exist_reports_no_saves_with_zero_counts()
    {
        var missing = Path.Combine(_dir, "does-not-exist");

        var status = SaveStatusScanner.Scan(GameFor(missing));

        Assert.Equal(SaveStatusKind.NoSaves, status.Kind);
        Assert.Equal(0, status.FileCount);
        Assert.Equal(0, status.TotalBytes);
        Assert.Null(status.LastModified);
    }

    [Fact]
    public void A_resolved_directory_that_exists_but_is_empty_reports_no_saves_with_zero_counts()
    {
        var status = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(SaveStatusKind.NoSaves, status.Kind);
        Assert.Equal(0, status.FileCount);
        Assert.Equal(0, status.TotalBytes);
        Assert.Null(status.LastModified);
    }

    [Fact]
    public void A_template_the_resolver_rejects_reports_a_path_error_and_never_no_saves()
    {
        var status = SaveStatusScanner.Scan(GameFor("%NOT_A_REAL_VARIABLE%/saves"));

        Assert.Equal(SaveStatusKind.PathError, status.Kind);
        Assert.NotNull(status.Error);
        Assert.NotEqual(SaveStatusKind.NoSaves, status.Kind);
    }

    [Fact]
    public void A_subdirectory_the_process_cannot_enumerate_still_reports_the_files_it_could_read()
    {
        WriteFile("readable.dat", 10);

        var lockedDir = Path.Combine(_dir, "locked");
        Directory.CreateDirectory(lockedDir);
        File.WriteAllBytes(Path.Combine(lockedDir, "hidden.dat"), new byte[10]);

        if (!TestFileSystem.TryDenyAccess(lockedDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var status = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(SaveStatusKind.Found, status.Kind);
        Assert.Equal(1, status.FileCount);
        Assert.Equal(10, status.TotalBytes);
    }

    [Fact]
    public void A_self_referencing_directory_junction_is_not_followed_so_the_walk_terminates()
    {
        var sub = Path.Combine(_dir, "sub");
        Directory.CreateDirectory(sub);
        WriteFile(Path.Combine("sub", "real.dat"), 10);

        var loop = Path.Combine(sub, "loop");
        if (!TestFileSystem.TryCreateJunction(loop, _dir))
            return; // Environment cannot create junctions — see TestFileSystem.TryCreateJunction.

        var status = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(SaveStatusKind.Found, status.Kind);
        Assert.Equal(1, status.FileCount);
    }

    [Theory]
    [InlineData(20_000, 0, false)]
    [InlineData(20_001, 0, true)]
    [InlineData(0, 5L * 1024 * 1024 * 1024, false)]
    [InlineData(0, 5L * 1024 * 1024 * 1024 + 1, true)]
    public void Oversized_thresholds_are_strictly_greater_than_not_greater_or_equal(long files, long bytes, bool expected)
    {
        Assert.Equal(expected, SaveStatusScanner.IsOversized(files, bytes));
    }

    [Fact]
    public void Scanning_the_same_unchanged_directory_twice_returns_equal_results()
    {
        WriteFile("a.dat", 10);
        WriteFile(Path.Combine("nested", "b.dat"), 20);

        var first = SaveStatusScanner.Scan(GameFor(_dir));
        var second = SaveStatusScanner.Scan(GameFor(_dir));

        Assert.Equal(first.FileCount, second.FileCount);
        Assert.Equal(first.TotalBytes, second.TotalBytes);
        Assert.Equal(first.LastModified, second.LastModified);
    }
}
