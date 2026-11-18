using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Pins the destination rule: any folder the user chose is a valid backup target. A volume check
/// (removable drives only) would leave users unable to back up at all without a USB stick — and
/// would be wrong regardless, because external SSDs report as Fixed. Nothing here may introduce one.
/// </summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly BackupService _service = new();

    public BackupServiceTests()
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
        catch (UnauthorizedAccessException)
        {
            // A Deny ACE that could not be lifted must not fail the whole run.
        }
    }

    private Game GameWithSaves(string fileName = "save.dat", string content = "payload")
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, fileName), content);

        return new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    // An existing save folder with nothing in it — distinct from GameWithSaves, whose folder
    // always holds at least one file.
    private Game GameWithEmptySaveDir()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);

        return new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    // Seeds save.dat plus nested/inner.dat so tests can pin the archive's directory structure.
    private Game GameWithNestedSaves()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        var nestedDir = Path.Combine(saveDir, "nested");
        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(nestedDir, "inner.dat"), "nested-payload");

        return new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    private static string BackupDirFor(string backupRoot, Game game) => Path.Combine(backupRoot, game.Id);

    // Seeds an archive by name, without running a real Backup — ListBackups's ordering, exclusion
    // and parsing rules are pinned against file names directly.
    private static string SeedArchiveFile(string backupRoot, string gameId, string fileName, byte[]? bytes = null)
    {
        var dir = Path.Combine(backupRoot, gameId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, bytes ?? []);
        return path;
    }

    [Fact]
    public void Backup_writes_to_the_configured_folder_on_an_ordinary_fixed_volume()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);

        var archives = Directory.GetFiles(Path.Combine(backupRoot, game.Id), "*.zip");
        var archive = Assert.Single(archives);

        using var zip = ZipFile.OpenRead(archive);
        var entry = Assert.Single(zip.Entries);
        Assert.Equal("save.dat", entry.FullName);
    }

    // The archive name is the timestamp ListBackups parses back with InvariantCulture. Written in
    // the user's culture, a non-Gregorian default calendar (th-TH: Buddhist, year + 543) put the
    // backup centuries in the future, so BackupComparer reported "up to date" forever.
    [Theory]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void Backup_names_the_archive_with_a_gregorian_timestamp_in_any_culture(string culture)
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var before = DateTime.Now.AddSeconds(-1);
            var result = _service.Backup(game, backupRoot);
            var after = DateTime.Now.AddSeconds(1);

            Assert.True(result.Success, result.Message);
            var archive = Assert.Single(_service.ListBackups(backupRoot, game.Id));
            Assert.InRange(archive.TakenAt, before, after);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Backup_creates_the_destination_folder_when_it_does_not_exist_yet()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "not", "created", "yet");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        Assert.True(Directory.Exists(Path.Combine(backupRoot, game.Id)));
    }

    [Fact]
    public void Backup_accepts_a_destination_on_the_save_data_own_volume()
    {
        // Same volume, and in fact the same parent directory as the saves. A backup here survives
        // everything except losing the disk, which is the trade the user is entitled to make.
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "beside-the-saves");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, game.Id), "*.zip"));
    }

    [Fact]
    public void Backup_reports_a_missing_save_folder_rather_than_writing_an_empty_archive()
    {
        var game = new Game
        {
            Id = "gone",
            DisplayName = "Gone",
            SaveDirTemplate = Path.Combine(_dir, "does-not-exist").Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("Save folder not found", result.Message);
        Assert.False(Directory.Exists(Path.Combine(backupRoot, game.Id)));
    }

    [Fact]
    public void Backup_reports_an_unresolvable_template_without_throwing()
    {
        var game = new Game
        {
            Id = "bad-template",
            DisplayName = "Bad Template",
            SaveDirTemplate = "%NOT_A_REAL_VARIABLE%/saves",
        };

        var result = _service.Backup(game, Path.Combine(_dir, "backups"));

        Assert.False(result.Success);
        Assert.Contains("Bad Template", result.Message);
    }

    [Fact]
    public void Backup_writes_a_verified_archive_with_nested_files_and_leaves_no_tmp_behind()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);

        var targetDir = BackupDirFor(backupRoot, game);
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
        var archive = Assert.Single(Directory.GetFiles(targetDir, "*.zip"));

        using var zip = ZipFile.OpenRead(archive);
        Assert.Equal(2, zip.Entries.Count);

        var saveEntry = zip.Entries.Single(e => e.FullName == "save.dat");
        Assert.Equal("payload".Length, saveEntry.Length);

        var nestedEntry = zip.Entries.Single(e => e.FullName == "nested/inner.dat");
        Assert.Equal("nested-payload".Length, nestedEntry.Length);
    }

    [Fact]
    public void Backup_of_a_locked_source_file_succeeds_without_it_and_names_the_skipped_path()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var savePath = Path.Combine(_dir, "saves", "save.dat");

        using (new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = _service.Backup(game, backupRoot);

            // A locked file is skipped with a warning instead of aborting the whole backup: the archive exists, but the message says the locked path is NOT in it.
            Assert.True(result.Success, result.Message);
            Assert.Contains("NOT in this backup", result.Message);
            Assert.Contains("save.dat", result.Message);
        }

        var targetDir = BackupDirFor(backupRoot, game);
        Assert.NotEmpty(Directory.GetFiles(targetDir, "*.zip"));
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
    }

    [Fact]
    public void DescribeVerificationFailure_returns_null_when_the_archive_matches_its_source()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var result = _service.Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);

        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        var saveDir = Path.Combine(_dir, "saves");

        Assert.Null(BackupService.DescribeVerificationFailure(archive, saveDir));
    }

    [Fact]
    public void DescribeVerificationFailure_names_the_entry_count_difference_after_a_file_is_added()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var result = _service.Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);

        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        var saveDir = Path.Combine(_dir, "saves");
        File.WriteAllText(Path.Combine(saveDir, "extra.dat"), "more");

        var detail = BackupService.DescribeVerificationFailure(archive, saveDir);

        Assert.NotNull(detail);
        Assert.Contains("entries", detail);
    }

    [Fact]
    public void DescribeVerificationFailure_names_the_byte_counts_after_a_source_file_grows()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var result = _service.Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);

        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        var saveDir = Path.Combine(_dir, "saves");
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload-grown-considerably");

        // Recorded hashes (the backup mode) match the archive entry ("payload", unchanged), so
        // the content check passes and the comparison falls through to the byte-count mismatch —
        // the disk-hash mode would instead trip the content check first, since the source file on
        // disk has since grown past what the archive holds.
        var expectedHashes = new Dictionary<string, string>
        {
            ["save.dat"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("payload"))),
        };
        var detail = BackupService.DescribeVerificationFailure(archive, saveDir, expectedHashes: expectedHashes);

        Assert.NotNull(detail);
        Assert.Contains("bytes in the archive", detail);
        Assert.Contains("bytes in the save folder", detail);
    }

    [Fact]
    public void DescribeVerificationFailure_reports_an_unreadable_archive_without_throwing()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        var notAnArchive = Path.Combine(_dir, "not-a-real.zip");
        File.WriteAllBytes(notAnArchive, [1, 2, 3, 4, 5, 6, 7, 8]);

        var detail = BackupService.DescribeVerificationFailure(notAnArchive, saveDir);

        Assert.NotNull(detail);
        Assert.Contains("could not be read back", detail);
    }

    [Fact]
    public void Backup_of_a_save_file_with_non_ascii_characters_in_its_name_succeeds_and_verifies()
    {
        var game = GameWithSaves(fileName: "spielstand-übung-遊戲.dat");
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        using var zip = ZipFile.OpenRead(archive);
        var entry = Assert.Single(zip.Entries);
        Assert.Equal("spielstand-übung-遊戲.dat", entry.FullName);
    }

    [Fact]
    public void Two_consecutive_backups_of_the_same_game_never_lose_or_collide_on_the_first_archive()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var first = _service.Backup(game, backupRoot);
        Assert.True(first.Success, first.Message);
        var firstArchive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var second = _service.Backup(game, backupRoot);

        Assert.True(File.Exists(firstArchive), "the first backup's archive must survive a second backup");

        if (second.Success)
        {
            var secondArchive = Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip")
                .Single(a => a != firstArchive);
            Assert.NotEqual(firstArchive, secondArchive);
        }
    }

    [Fact]
    public void Backup_reports_an_empty_save_folder_and_writes_nothing()
    {
        var game = GameWithEmptySaveDir();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("Save folder is empty", result.Message);
        Assert.Contains(Path.Combine(_dir, "saves"), result.Message);
        Assert.False(Directory.Exists(BackupDirFor(backupRoot, game)));
    }

    [Fact]
    public void A_tree_of_empty_subfolders_counts_as_empty()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(Path.Combine(saveDir, "a", "b"));
        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("Save folder is empty", result.Message);
        Assert.Contains(saveDir, result.Message);
        Assert.False(Directory.Exists(BackupDirFor(backupRoot, game)));
    }

    [Fact]
    public void Backup_reports_a_save_folder_it_cannot_list_separately_from_an_empty_one()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        if (!TestFileSystem.TryDenyAccess(saveDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("Save folder could not be read", result.Message);
        Assert.DoesNotContain("is empty", result.Message);
    }

    [Fact]
    public void Backup_reports_an_unavailable_backup_folder_and_writes_nothing()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupRoot);
        // A file, not a directory, occupies the path EnsureWritable needs to create.
        File.WriteAllText(Path.Combine(backupRoot, game.Id), "not a directory");

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("Backup folder not available", result.Message);
        Assert.Contains(backupRoot, result.Message);
        Assert.Empty(Directory.GetFiles(backupRoot, "*.zip", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_stale_temp_file_from_a_killed_run_is_removed_by_the_next_backup()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var targetDir = BackupDirFor(backupRoot, game);
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "2020-01-01_000000.zip.tmp"), "leftover from a killed run");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Backed up", result.Message);
        Assert.DoesNotContain(".zip", result.Message);   // SHIP: a success message names the game, not a stored path
        Assert.DoesNotContain(".tmp", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporary", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
        Assert.Single(Directory.GetFiles(targetDir, "*.zip"));
    }

    [Fact]
    public void A_save_folder_containing_a_directory_junction_still_backs_up_successfully()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        var linkedDir = Path.Combine(_dir, "linked-target");
        Directory.CreateDirectory(linkedDir);
        File.WriteAllText(Path.Combine(linkedDir, "linked.dat"), "linked-payload");

        var junctionPath = Path.Combine(saveDir, "junction");
        if (!TestFileSystem.TryCreateJunction(junctionPath, linkedDir))
            return; // Environment cannot create junctions — see TestFileSystem.TryCreateJunction.

        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);

        using var zip = ZipFile.OpenRead(Directory.GetFiles(Path.Combine(backupRoot, game.Id), "*.zip").Single());
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("save.dat", names);
        Assert.DoesNotContain(names, name => name.Contains("linked.dat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.StartsWith("junction/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_save_folder_containing_only_a_symlinked_file_is_refused_as_empty_not_backed_up_empty()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);

        var target = Path.Combine(_dir, "linked-target.dat");
        File.WriteAllText(target, "linked-payload");

        var linkPath = Path.Combine(saveDir, "save.dat");
        if (!TestFileSystem.TryCreateFileSymlink(linkPath, target))
            return; // Environment cannot create file symlinks — see TestFileSystem.TryCreateFileSymlink.

        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        // A symlinked file must not read as "has files" when the archive walk is about to skip
        // it — otherwise this succeeds with an empty zip instead of refusing like a genuinely
        // empty save folder.
        Assert.False(result.Success, result.Message);
        // A refused backup may never create the game's folder at all, so "no zip" covers both.
        var gameDir = Path.Combine(backupRoot, game.Id);
        Assert.True(!Directory.Exists(gameDir) || Directory.GetFiles(gameDir, "*.zip").Length == 0);
    }

    [Fact]
    public void Repeated_aborts_against_an_empty_save_folder_leave_the_backup_root_untouched()
    {
        var game = GameWithEmptySaveDir();
        var backupRoot = Path.Combine(_dir, "backups");

        var first = _service.Backup(game, backupRoot);
        var second = _service.Backup(game, backupRoot);

        Assert.Equal(first.Message, second.Message);
        Assert.False(Directory.Exists(backupRoot));
    }

    [Fact]
    public void Backup_survives_a_final_rename_collision_by_suffixing_the_archive()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var targetDir = Path.Combine(backupRoot, game.Id);
        Directory.CreateDirectory(targetDir);

        // Pre-creates the exact file name Backup is about to produce, so the final File.Move
        // collides. The verified staging file is NOT cleaned up and the backup does NOT
        // fail: the archive is retried under a -1 suffix (the two-argument Move never
        // overwrites, so the colliding name's content survives untouched).
        var collidingPath = Path.Combine(targetDir, $"{DateTime.Now:yyyy-MM-dd_HHmmss}.zip");
        File.WriteAllBytes(collidingPath, [9]);

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        // The suffixed archive exists and carries the payload; no staging file is left behind.
        var archives = Directory.GetFiles(targetDir, "*.zip");
        Assert.Equal(2, archives.Length);
        Assert.Contains(archives, a => a.EndsWith("-1.zip") && File.Exists(Path.Combine(Path.GetDirectoryName(a)!, Path.GetFileName(a))));
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(collidingPath));
    }

    [Fact]
    public void Archive_timestamp_is_taken_after_writing_and_verifying_not_before()
    {
        // A timestamp generated right before ZipFile.CreateFromDirectory would let a long-running
        // zip produce an archive whose own name predates a save file written while it ran, reading
        // as "changed since backup" on a backup that just succeeded. The staging file's name
        // carries no timestamp at all (a GUID instead), so this proves
        // the only timestamp ever written — the final archive's — cannot be older than the moment
        // the write actually finished.
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var beforeBackupCall = DateTime.Now;
        var result = _service.Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);
        var afterBackupCall = DateTime.Now;

        var archive = Assert.Single(_service.ListBackups(backupRoot, game.Id));

        // Second-precision file name vs. sub-second DateTime.Now: allow the one second of
        // truncation the file-name format (yyyy-MM-dd_HHmmss) itself introduces.
        Assert.True(archive.TakenAt >= beforeBackupCall.AddSeconds(-1),
            $"TakenAt {archive.TakenAt:O} is more than a second before the call started ({beforeBackupCall:O}).");
        Assert.True(archive.TakenAt <= afterBackupCall.AddSeconds(1),
            $"TakenAt {archive.TakenAt:O} is more than a second after the call returned ({afterBackupCall:O}).");
    }

    [Fact]
    public void ListBackups_returns_archives_newest_first()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-20_100000.zip");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-22_090000.zip");

        var result = _service.ListBackups(backupRoot, "test-game");

        Assert.Equal(
            ["2026-07-25_143012.zip", "2026-07-22_090000.zip", "2026-07-20_100000.zip"],
            result.Select(b => Path.GetFileName(b.FilePath)));
    }

    [Fact]
    public void ListBackups_ignores_temp_files()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143013.zip.tmp");

        var result = _service.ListBackups(backupRoot, "test-game");

        var archive = Assert.Single(result);
        Assert.False(archive.FilePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListBackups_parses_the_timestamp_out_of_the_file_name()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip");

        var result = _service.ListBackups(backupRoot, "test-game");

        var archive = Assert.Single(result);
        Assert.Equal(new DateTime(2026, 7, 25, 14, 30, 12), archive.TakenAt);
    }

    [Fact]
    public void ListBackups_falls_back_to_the_write_time_for_a_name_it_cannot_parse()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var path = SeedArchiveFile(backupRoot, "test-game", "hand-renamed.zip");

        var result = _service.ListBackups(backupRoot, "test-game");

        var archive = Assert.Single(result);
        Assert.Equal(File.GetLastWriteTime(path), archive.TakenAt);
    }

    [Fact]
    public void ListBackups_reports_the_archive_size_on_disk()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var path = SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip", new byte[12345]);

        var result = _service.ListBackups(backupRoot, "test-game");

        var archive = Assert.Single(result);
        Assert.Equal(new FileInfo(path).Length, archive.SizeBytes);
    }

    [Fact]
    public void ListBackups_returns_empty_for_an_unreachable_backup_root()
    {
        var backupRoot = Path.Combine(_dir, "never-created");

        var result = _service.ListBackups(backupRoot, "test-game");

        Assert.Empty(result);
        Assert.False(Directory.Exists(backupRoot));
    }

    [Fact]
    public void ListBackups_returns_empty_for_a_game_that_has_never_been_backed_up()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupRoot);

        var result = _service.ListBackups(backupRoot, "test-game");

        Assert.Empty(result);
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "test-game")));
    }

    [Fact]
    public void ListBackups_returns_empty_rather_than_throwing_for_a_folder_it_cannot_read()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip");
        var targetDir = Path.Combine(backupRoot, "test-game");

        if (!TestFileSystem.TryDenyAccess(targetDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var result = _service.ListBackups(backupRoot, "test-game");

        Assert.Empty(result);
    }

    [Fact]
    public void TryListBackups_returns_true_for_a_game_that_has_never_been_backed_up()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupRoot);

        var listable = _service.TryListBackups(backupRoot, "test-game", out var result);

        Assert.True(listable);
        Assert.Empty(result);
    }

    [Fact]
    public void TryListBackups_returns_false_for_a_folder_it_cannot_read()
    {
        // ListBackups folds this into the same empty list as "never backed up" — TryListBackups is
        // what lets the restore pre-flight tell the two apart and report "folder not available"
        // instead of sending the user to make a first backup into a folder it just proved it can't
        // read.
        var backupRoot = Path.Combine(_dir, "backups");
        SeedArchiveFile(backupRoot, "test-game", "2026-07-25_143012.zip");
        var targetDir = Path.Combine(backupRoot, "test-game");

        if (!TestFileSystem.TryDenyAccess(targetDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var listable = _service.TryListBackups(backupRoot, "test-game", out var result);

        Assert.False(listable);
        Assert.Empty(result);
    }

    [Fact]
    public void Backup_with_a_suffix_writes_a_suffixed_archive_that_ListBackups_still_returns()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot, "-pre-restore", out var archivePath);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(archivePath);
        Assert.EndsWith("-pre-restore.zip", archivePath);
        Assert.True(File.Exists(archivePath));

        var listed = _service.ListBackups(backupRoot, game.Id);
        var archive = Assert.Single(listed);
        Assert.Equal(archivePath, archive.FilePath);
    }

    [Theory]
    [InlineData("Backups")]
    [InlineData("")]
    public void A_backup_folder_inside_or_equal_to_the_save_folder_is_honoured_and_never_archived(string relativeRoot)
    {
        var game = GameWithSaves();
        var saveDir = Path.Combine(_dir, "saves");
        var backupRoot = Path.Combine(saveDir, relativeRoot);

        var first = _service.Backup(game, backupRoot);
        Assert.True(first.Success, first.Message);

        // A second run is the one that would pack the first archive if the walk did not step over
        // the backup folder. The suffix only keeps the two names apart within one second.
        var second = _service.Backup(game, backupRoot, "-second", out var secondPath);
        Assert.True(second.Success, second.Message);

        using var zip = ZipFile.OpenRead(secondPath!);
        var entry = Assert.Single(zip.Entries);
        Assert.Equal("save.dat", entry.FullName);
    }

    // Degenerate config the restore side already refuses (IsSameOrInside): backupRoot pointed AT a
    // save folder shared by several games. An exact-match-only exclusion would walk into the other
    // games' archive directories — every backup packing every other game's archives, each new
    // archive amplifying the next — so this pins the containment rule behaviourally, on the
    // archive's contents rather than on any message.
    [Fact]
    public void A_backup_root_equal_to_a_shared_save_folder_packs_no_other_games_archive_dir()
    {
        var shared = Path.Combine(_dir, "shared-saves");
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "save.dat"), "payload");
        var gameA = new Game { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = shared.Replace('\\', '/') };
        var gameB = new Game { Id = "game-b", DisplayName = "Game B", SaveDirTemplate = shared.Replace('\\', '/') };
        var backupRoot = shared;

        var first = _service.Backup(gameA, backupRoot);
        Assert.True(first.Success, first.Message);

        // At this moment the shared folder holds game-a/<timestamp>.zip — exactly what a
        // non-pruned walk would pack into game B's archive.
        var second = _service.Backup(gameB, backupRoot);
        Assert.True(second.Success, second.Message);

        var archiveB = Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, gameB.Id), "*.zip"));
        using var zip = ZipFile.OpenRead(archiveB);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("save.dat", names);
        Assert.DoesNotContain(names, name => name.StartsWith("game-a/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    // ITEM-5: InspectSaveDir's empty check walks without the exclusions, so a save folder whose
    // only files sit in the backup folder passes it — the archive pass then excludes that same
    // folder and collects nothing, which a zero-entry archive would otherwise verify (0 equals 0)
    // and report as a successful backup.
    [Fact]
    public void A_save_folder_holding_only_the_backup_folder_fails_and_writes_no_archive()
    {
        var saveDir = Path.Combine(_dir, "s4");
        var backupRoot = Path.Combine(saveDir, "bk");
        Directory.CreateDirectory(backupRoot);
        var oldArchive = Path.Combine(backupRoot, "old.zip");
        File.WriteAllText(oldArchive, "not a real archive");

        var game = new Game
        {
            Id = "s4-game",
            DisplayName = "S4 Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.Contains("nothing but the backup folder", result.Message);

        var targetDir = BackupDirFor(backupRoot, game);
        Assert.Empty(Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "*.zip") : []);
        Assert.Empty(Directory.Exists(targetDir) ? Directory.GetFiles(targetDir, "*.zip.tmp") : []);
        Assert.True(File.Exists(oldArchive));
        Assert.Equal("not a real archive", File.ReadAllText(oldArchive));
    }

    // The documented supported case (CHANGELOG 1.3.0): backupRoot INSIDE a game's save folder keeps
    // working under the containment rule — walking the save folder prunes the subtree that equals
    // or contains the backup root, so the archive holds the save files and no archives, and a
    // second run still succeeds instead of packing (or colliding with) the first archive.
    [Fact]
    public void A_backup_root_inside_the_save_folder_still_succeeds_and_packs_no_archives()
    {
        var game = GameWithSaves();
        var saveDir = Path.Combine(_dir, "saves");
        var backupRoot = Path.Combine(saveDir, "backups");

        var first = _service.Backup(game, backupRoot);
        Assert.True(first.Success, first.Message);

        var second = _service.Backup(game, backupRoot, "-second", out var secondPath);
        Assert.True(second.Success, second.Message);

        using var zip = ZipFile.OpenRead(secondPath!);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("save.dat", names);
        Assert.DoesNotContain(names, name => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.StartsWith("backups/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Backup_two_argument_overload_still_writes_an_unsuffixed_archive()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        Assert.DoesNotContain("-pre-restore", archive);
    }

    [Fact]
    public void IsBackupRootReachable_reflects_existence_without_creating_anything()
    {
        var existing = Path.Combine(_dir, "existing-root");
        Directory.CreateDirectory(existing);
        var entriesBefore = Directory.GetFileSystemEntries(_dir).OrderBy(x => x).ToList();

        Assert.True(_service.IsBackupRootReachable(existing));

        var missing = Path.Combine(_dir, "never-created-root");
        Assert.False(_service.IsBackupRootReachable(missing));

        var malformed = "C:\\invalid\0path";
        Assert.False(_service.IsBackupRootReachable(malformed));

        var entriesAfter = Directory.GetFileSystemEntries(_dir).OrderBy(x => x).ToList();
        Assert.Equal(entriesBefore, entriesAfter);
    }

    [Fact]
    public void A_backup_written_by_this_service_is_listed_by_it()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var backupResult = _service.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archivePath = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var listed = _service.ListBackups(backupRoot, game.Id);

        var archive = Assert.Single(listed);
        Assert.Equal(archivePath, archive.FilePath);
        var expected = DateTime.ParseExact(
            Path.GetFileNameWithoutExtension(archivePath), "yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
        Assert.Equal(expected, archive.TakenAt);
    }

    // --- ComputeSaveDataSize and the optional byte-progress channel ---

    [Fact]
    public void ComputeSaveDataSize_sums_the_lengths_of_every_archived_file()
    {
        var game = GameWithNestedSaves();
        var expected = (long)("payload".Length + "nested-payload".Length);

        var size = _service.ComputeSaveDataSize(game);

        Assert.Equal(expected, size);
    }

    [Fact]
    public void ComputeSaveDataSize_returns_zero_and_does_not_throw_when_the_template_does_not_resolve()
    {
        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = "%NOT_A_REAL_VARIABLE%/saves",
        };

        var size = _service.ComputeSaveDataSize(game);

        Assert.Equal(0, size);
    }

    [Fact]
    public void ComputeSaveDataSize_returns_zero_and_does_not_throw_when_the_save_directory_does_not_exist()
    {
        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = Path.Combine(_dir, "never-created").Replace('\\', '/'),
        };

        var size = _service.ComputeSaveDataSize(game);

        Assert.Equal(0, size);
    }

    [Fact]
    public void ComputeSaveDataSize_skips_a_symlinked_file_and_a_junction_exactly_as_the_archive_does()
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        var linkedDir = Path.Combine(_dir, "linked-target");
        Directory.CreateDirectory(linkedDir);
        File.WriteAllText(Path.Combine(linkedDir, "linked.dat"), "linked-payload");
        var junctionCreated = TestFileSystem.TryCreateJunction(Path.Combine(saveDir, "junction"), linkedDir);

        var symlinkTarget = Path.Combine(_dir, "symlink-target.dat");
        File.WriteAllText(symlinkTarget, "symlinked-payload");
        var symlinkCreated = TestFileSystem.TryCreateFileSymlink(Path.Combine(saveDir, "symlink.dat"), symlinkTarget);

        if (!junctionCreated || !symlinkCreated)
            return; // Environment cannot create junctions/symlinks — see TestFileSystem's own callers.

        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");

        var computedSize = _service.ComputeSaveDataSize(game);

        var result = _service.Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);
        using var zip = ZipFile.OpenRead(Directory.GetFiles(Path.Combine(backupRoot, game.Id), "*.zip").Single());
        var archivedTotal = zip.Entries.Where(e => !e.FullName.EndsWith('/')).Sum(e => e.Length);

        Assert.Equal(archivedTotal, computedSize);
        Assert.Equal("payload".Length, computedSize);
    }

    [Fact]
    public void Backup_with_a_progress_sink_reports_cumulative_bytes_matching_ComputeSaveDataSize()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var expectedTotal = _service.ComputeSaveDataSize(game);
        var reports = new List<long>();
        var progress = new SynchronousProgress<long>(reports.Add);

        var result = _service.Backup(game, backupRoot, "", out _, progress);

        Assert.True(result.Success, result.Message);
        Assert.True(reports.Count >= 2, "expected at least one report per archived file (two files)");
        Assert.True(reports.SequenceEqual(reports.OrderBy(v => v)), "reports must be monotonically non-decreasing");
        Assert.Equal(expectedTotal, reports[^1]);
    }

    // --- Intra-file reporting, replacing the original once-per-file granularity ---

    [Fact]
    public void Backup_with_a_progress_sink_reports_multiple_times_during_a_single_large_file()
    {
        // A save dominated by one large file — such as a real-world DayZ save — is the
        // shape once-per-file reporting cannot make visible: once-per-file reporting
        // fires exactly once for this fixture, which this test pins as not enough.
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), new byte[5 * 1024 * 1024]);
        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
        var backupRoot = Path.Combine(_dir, "backups");
        var expectedTotal = _service.ComputeSaveDataSize(game);
        var reports = new List<long>();
        var progress = new SynchronousProgress<long>(reports.Add);

        var result = _service.Backup(game, backupRoot, "", out _, progress);

        Assert.True(result.Success, result.Message);
        Assert.True(reports.Count >= 4, $"expected at least 4 reports for a 5 MB single file, got {reports.Count}");
        Assert.True(reports.SequenceEqual(reports.OrderBy(v => v)), "reports must be monotonically non-decreasing");
        Assert.Equal(expectedTotal, reports[^1]);
    }

    [Fact]
    public void Backup_preserves_each_entrys_source_last_write_time()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var saveDir = Path.Combine(_dir, "saves");
        var sourceLastWrite = File.GetLastWriteTime(Path.Combine(saveDir, "save.dat"));

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        using var zip = ZipFile.OpenRead(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip").Single());
        var entry = zip.Entries.Single(e => e.FullName == "save.dat");

        // The zip format's own timestamp resolution is two seconds.
        Assert.True(Math.Abs((entry.LastWriteTime.LocalDateTime - sourceLastWrite).TotalSeconds) <= 2,
            $"expected an entry timestamp near {sourceLastWrite}, got {entry.LastWriteTime.LocalDateTime}");
    }

    [Fact]
    public void Backup_with_no_progress_sink_still_writes_a_verified_archive()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
    }

    [Fact]
    public void Backup_reports_no_progress_when_the_save_directory_check_fails()
    {
        var game = GameWithEmptySaveDir();
        var backupRoot = Path.Combine(_dir, "backups");
        var reports = new List<long>();
        var progress = new SynchronousProgress<long>(reports.Add);

        var result = _service.Backup(game, backupRoot, "", out _, progress);

        Assert.False(result.Success, result.Message);
        Assert.Empty(reports);
    }

    // --- TryGetAvailableFreeSpace, an advisory read that degrades to absent ---

    [Fact]
    public void TryGetAvailableFreeSpace_reports_a_positive_figure_for_a_fully_qualified_local_path()
    {
        var result = _service.TryGetAvailableFreeSpace(_dir, out var freeBytes);

        Assert.True(result);
        Assert.True(freeBytes > 0);
    }

    [Fact]
    public void TryGetAvailableFreeSpace_creates_nothing_on_disk()
    {
        var backupRoot = Path.Combine(_dir, "never-created");
        var result = _service.TryGetAvailableFreeSpace(backupRoot, out _);

        Assert.True(result);
        Assert.False(Directory.Exists(backupRoot));
    }

    [Fact]
    public void TryGetAvailableFreeSpace_returns_false_without_throwing_for_an_unmounted_drive_letter()
    {
        var used = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        var free = Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => (char)c).FirstOrDefault(c => !used.Contains(c));

        if (free == default)
            return; // Every letter is in use on this machine — nothing to assert.

        var result = _service.TryGetAvailableFreeSpace($"{free}:\\backups", out var freeBytes);

        Assert.False(result);
        Assert.Equal(0, freeBytes);
    }

    [Fact]
    public void TryGetAvailableFreeSpace_returns_false_without_throwing_for_a_unc_path()
    {
        var result = _service.TryGetAvailableFreeSpace(@"\\nonexistent-server\share\backups", out var freeBytes);

        Assert.False(result);
        Assert.Equal(0, freeBytes);
    }

    [Fact]
    public void TryGetAvailableFreeSpace_returns_false_without_throwing_for_an_empty_string()
    {
        var result = _service.TryGetAvailableFreeSpace("", out var freeBytes);

        Assert.False(result);
        Assert.Equal(0, freeBytes);
    }

    [Fact]
    public void TryGetAvailableFreeSpace_returns_false_without_throwing_for_a_string_with_no_drive_letter()
    {
        // Unlike Directory.Exists (which swallows an invalid full path and returns false
        // regardless of where the malformation sits), DriveInfo's constructor only inspects the
        // leading drive-letter form — an embedded null byte later in an otherwise valid "C:\..."
        // path does not reach it. A string with no drive-letter shape at all is what its own
        // ArgumentException actually guards against.
        var result = _service.TryGetAvailableFreeSpace("not-a-drive-path", out var freeBytes);

        Assert.False(result);
        Assert.Equal(0, freeBytes);
    }

    /// <summary>
    /// Records progress on the calling thread. The real <see cref="Progress{T}"/> posts to a
    /// synchronization context, which a plain xUnit host does not have, so its callbacks would
    /// arrive after the assertions — the identical problem
    /// <c>RestoreServiceTests.SynchronousProgress</c> solves for <see cref="IProgress{T}"/> of
    /// <see cref="int"/>. Duplicated locally rather than promoted to a shared helper: the two take
    /// different type arguments, and this codebase already mixes shared helpers
    /// (<see cref="TestFileSystem"/>) with file-local doubles for this exact pattern.
    /// </summary>
    private sealed class SynchronousProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    /// <summary>
    /// A game whose display name exercises the non-ASCII path of the failure message the rename
    /// cap produces — the sentence is composed inline, so it must survive a name outside ASCII.
    /// </summary>
    private Game NonAsciiGameWithSaves(string displayName)
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        return new Game
        {
            Id = "test-game",
            DisplayName = displayName,
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    /// <summary>
    /// Pre-creates every archive name a five-second window can produce, from the bare
    /// <c>yyyy-MM-dd_HHmmss.zip</c> through <c>-{maxSuffix}.zip</c>, so the final rename walks
    /// the entire suffix ladder for whichever second the backup actually stamps. Five seconds
    /// absorb a slow CI disk (the timestamp is taken after writing and verifying, not before).
    /// Returns the created paths with the bytes each must keep.
    /// </summary>
    private Dictionary<string, byte[]> OccupyNames(string targetDir, int maxSuffix)
    {
        Directory.CreateDirectory(targetDir);
        var occupied = new Dictionary<string, byte[]>();
        var baseSecond = DateTime.Now;

        for (var offset = -2; offset <= 2; offset++)
        {
            var timestamp = baseSecond.AddSeconds(offset).ToString("yyyy-MM-dd_HHmmss");
            // Suffix "" (the bare name) through -maxSuffix — maxSuffix + 1 names per second.
            for (var suffix = 0; suffix <= maxSuffix; suffix++)
            {
                var name = suffix == 0
                    ? $"{timestamp}.zip"
                    : $"{timestamp}-{suffix}.zip";
                var path = Path.Combine(targetDir, name);
                var bytes = new byte[] { (byte)(suffix % 251 + 1) };
                File.WriteAllBytes(path, bytes);
                occupied[path] = bytes;
            }
        }

        return occupied;
    }

    [Fact]
    public void Backup_one_name_below_the_rename_retry_limit_still_succeeds_as_the_last_suffix()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var targetDir = Path.Combine(backupRoot, game.Id);

        // 100 occupied names per second (the bare timestamp plus -1 … -99): one below the cap.
        // The retry ladder still has -100.zip free, so the backup succeeds unchanged.
        var occupied = OccupyNames(targetDir, maxSuffix: 99);

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        // Exactly one new archive, named with the last suffix of the ladder.
        var archives = Directory.GetFiles(targetDir, "*.zip").Except(occupied.Keys).ToList();
        Assert.Single(archives);
        Assert.EndsWith("-100.zip", archives[0], StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
        // Every pre-created file keeps its bytes — nothing was overwritten.
        Assert.All(occupied, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    [Fact]
    public void Backup_at_the_rename_retry_limit_reports_the_limit_not_a_generic_disk_error()
    {
        var game = NonAsciiGameWithSaves("Ōkami Test");
        var backupRoot = Path.Combine(_dir, "backups");
        var targetDir = Path.Combine(backupRoot, game.Id);

        // 101 occupied names per second (the bare timestamp plus -1 … -100): at the cap, the
        // suffix ladder is exhausted and the backup must fail naming the limit, not a disk error.
        var occupied = OccupyNames(targetDir, maxSuffix: 100);
        var zipCountBefore = Directory.GetFiles(targetDir, "*.zip").Length;

        var result = _service.Backup(game, backupRoot);

        Assert.False(result.Success);
        Assert.StartsWith("Backup failed for Ōkami Test:", result.Message, StringComparison.Ordinal);
        Assert.Contains("rename retry limit", result.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(targetDir, "*.zip.tmp"));
        Assert.Equal(zipCountBefore, Directory.GetFiles(targetDir, "*.zip").Length);
        // Every pre-existing archive keeps its bytes — no overwrite, no modification.
        Assert.All(occupied, entry => Assert.Equal(entry.Value, File.ReadAllBytes(entry.Key)));
    }

    // Item 0: a save folder that merely SITS INSIDE the backup root holds no archive of this app
    // below it, so nothing needs pruning — every subfolder is archived. The old either-direction
    // prune rule dropped every subfolder instead.
    [Fact]
    public void A_save_folder_inside_the_backup_folder_is_archived_with_all_its_subfolders()
    {
        var saveDir = Path.Combine(_dir, "Documents", "My Games", "X");
        Directory.CreateDirectory(Path.Combine(saveDir, "profiles"));
        File.WriteAllText(Path.Combine(saveDir, "top.dat"), "payload");
        File.WriteAllText(Path.Combine(saveDir, "profiles", "p1.dat"), "payload");
        var game = new Game { Id = "x-game", DisplayName = "X Game", SaveDirTemplate = saveDir.Replace('\\', '/') };
        var backupRoot = Path.Combine(_dir, "Documents");

        var result = _service.Backup(game, backupRoot, "", out var archivePath);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(archivePath);
        using var zip = ZipFile.OpenRead(archivePath);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Equal(["top.dat", "profiles/p1.dat"], names);
        Assert.DoesNotContain("Left out", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT in this backup", result.Message, StringComparison.Ordinal);
    }

    // Item 0, second layout: a save folder nested inside its own game's archive folder
    // (<backupRoot>/<id>/saves) is also archived in full — again no archive lives below it.
    [Fact]
    public void A_save_folder_inside_its_own_games_archive_folder_is_archived_in_full()
    {
        var saveDir = Path.Combine(_dir, "b3", "x", "saves");
        Directory.CreateDirectory(Path.Combine(saveDir, "sub"));
        File.WriteAllText(Path.Combine(saveDir, "a.dat"), "payload");
        File.WriteAllText(Path.Combine(saveDir, "sub", "b.dat"), "payload");
        var game = new Game { Id = "x", DisplayName = "X", SaveDirTemplate = saveDir.Replace('\\', '/') };
        var backupRoot = Path.Combine(_dir, "b3");

        var result = _service.Backup(game, backupRoot);

        Assert.True(result.Success, result.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        using var zip = ZipFile.OpenRead(archive);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Equal(["a.dat", "sub/b.dat"], names);
    }

    // D-01: a folder the walk leaves out on purpose (the backup folder inside the save folder) is
    // named once, in a plain note — never with the unreadable-path warning wording, which would
    // read as a fault in a layout the user chose on purpose.
    [Fact]
    public void A_backup_folder_inside_the_save_folder_is_named_as_left_out_not_as_unreadable()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "saves", "backups");

        Assert.True(_service.Backup(game, backupRoot).Success);
        var second = _service.Backup(game, backupRoot, "-second", out var secondPath);
        Assert.True(second.Success, second.Message);

        Assert.Single(Regex.Matches(second.Message, "Left out:"));
        Assert.Contains("Left out: backups (backup folder).", second.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT in this backup", second.Message, StringComparison.Ordinal);
        using var zip = ZipFile.OpenRead(secondPath!);
        Assert.Equal("save.dat", Assert.Single(zip.Entries).FullName);
    }

    // D-01, plural form: with the backup root AT a shared save folder, both games' archive folders
    // are named and the note ends "(backup folders).".
    [Fact]
    public void A_backup_root_equal_to_a_shared_save_folder_names_the_other_games_archive_folders_as_left_out()
    {
        var shared = Path.Combine(_dir, "shared-saves");
        Directory.CreateDirectory(shared);
        File.WriteAllText(Path.Combine(shared, "save.dat"), "payload");
        var gameA = new Game { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = shared.Replace('\\', '/') };
        var gameB = new Game { Id = "game-b", DisplayName = "Game B", SaveDirTemplate = shared.Replace('\\', '/') };
        var backupRoot = shared;

        Assert.True(_service.Backup(gameA, backupRoot).Success);
        var second = _service.Backup(gameB, backupRoot);
        Assert.True(second.Success, second.Message);

        Assert.Contains("game-a", second.Message, StringComparison.Ordinal);
        Assert.Contains("game-b", second.Message, StringComparison.Ordinal);
        Assert.Contains("(backup folders).", second.Message, StringComparison.Ordinal);
    }
}
