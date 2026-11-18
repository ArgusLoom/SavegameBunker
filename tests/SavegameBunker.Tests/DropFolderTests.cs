using System.IO;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Drives <see cref="DropFolder.TrySingleExistingDirectory"/> without WPF — the folder-drop
/// classifier that MainWindow's DragOver/Drop handlers call.
/// </summary>
public sealed class DropFolderTests : IDisposable
{
    private readonly string _dir;

    public DropFolderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* leftover must not fail the run */ }
    }

    [Fact]
    public void One_existing_directory_is_accepted()
    {
        Assert.True(DropFolder.TrySingleExistingDirectory([_dir], out var directory));
        Assert.Equal(_dir, directory);
    }

    [Fact]
    public void Two_directories_are_rejected()
    {
        var other = Path.Combine(_dir, "other");
        Directory.CreateDirectory(other);

        Assert.False(DropFolder.TrySingleExistingDirectory([_dir, other], out var directory));
        Assert.Equal("", directory);
    }

    [Fact]
    public void A_file_is_rejected()
    {
        var file = Path.Combine(_dir, "save.dat");
        File.WriteAllBytes(file, [1]);

        Assert.False(DropFolder.TrySingleExistingDirectory([file], out var directory));
        Assert.Equal("", directory);
    }

    [Fact]
    public void An_empty_array_is_rejected()
    {
        Assert.False(DropFolder.TrySingleExistingDirectory([], out var directory));
        Assert.Equal("", directory);
    }

    [Fact]
    public void A_null_array_is_rejected()
    {
        Assert.False(DropFolder.TrySingleExistingDirectory(null, out var directory));
        Assert.Equal("", directory);
    }

    [Fact]
    public void A_missing_path_is_rejected()
    {
        var missing = Path.Combine(_dir, "does-not-exist");

        Assert.False(DropFolder.TrySingleExistingDirectory([missing], out var directory));
        Assert.Equal("", directory);
    }
}
