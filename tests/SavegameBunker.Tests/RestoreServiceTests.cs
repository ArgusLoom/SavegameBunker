using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Proves the restore ordering end to end against a real filesystem: a backup written by
/// <see cref="BackupService.Backup(Game, string)"/> is restored back by
/// <see cref="RestoreService.Restore"/>, so the round trip proves the backup format itself
/// rather than a hand-built fixture. Nothing here may reorder or short-circuit the
/// safety-backup → extract → verify → swap sequence.
/// </summary>
public sealed class RestoreServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly BackupService _backupService = new();
    private readonly RestoreService _service;

    public RestoreServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _service = new RestoreService(_backupService);
    }

    public void Dispose()
    {
        try
        {
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

    private string SaveDirIn(string dir) => Path.Combine(dir, "saves");

    // Relative-path-to-length snapshot of a directory tree, so a failure test can assert the save
    // directory is byte-for-byte unchanged rather than merely "still exists".
    private static Dictionary<string, long> Snapshot(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .ToDictionary(
                f => Path.GetRelativePath(dir, f).Replace(Path.DirectorySeparatorChar, '/'),
                f => new FileInfo(f).Length);

    [Fact]
    public void Round_trip_restores_a_nested_save_tree_backed_up_by_BackupService()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        Directory.Delete(saveDir, recursive: true);
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "mutated.dat"), "mutated");

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.False(File.Exists(Path.Combine(saveDir, "mutated.dat")));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
        Assert.Equal("nested-payload", File.ReadAllText(Path.Combine(saveDir, "nested", "inner.dat")));
    }

    [Fact]
    public void Restore_writes_a_pre_restore_safety_backup_before_swapping_in_the_restored_contents()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "current-before-restore");

        var result = _service.Restore(game, backupRoot, archive, _ => true);
        Assert.True(result.Success, result.Message);

        var preRestoreArchive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));
        using var zip = ZipFile.OpenRead(preRestoreArchive);
        var entry = Assert.Single(zip.Entries);
        using var reader = new StreamReader(entry.Open());
        Assert.Equal("current-before-restore", reader.ReadToEnd());
    }

    [Fact]
    public void A_save_dir_containing_only_a_symlinked_file_still_restores_without_a_doomed_safety_backup()
    {
        // HasAnyFile must not read the symlinked file as "something to protect" — BackupService's
        // own safety-backup write skips that exact file and would otherwise refuse it as empty,
        // aborting the restore before extraction even runs.
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.Delete(Path.Combine(saveDir, "save.dat"));

        var target = Path.Combine(_dir, "linked-target.dat");
        File.WriteAllText(target, "linked-payload");
        if (!TestFileSystem.TryCreateFileSymlink(Path.Combine(saveDir, "save.dat"), target))
            return; // Environment cannot create file symlinks — see TestFileSystem.TryCreateFileSymlink.

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
        Assert.Empty(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));
    }

    [Fact]
    public void Confirm_is_invoked_once_with_a_safety_backup_already_on_disk_and_save_dir_still_pre_restore()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "still-here-when-confirm-runs");

        var confirmCallCount = 0;
        RestorePrompt? capturedPrompt = null;
        string? saveDirContentAtConfirmTime = null;

        var result = _service.Restore(game, backupRoot, archive, prompt =>
        {
            confirmCallCount++;
            capturedPrompt = prompt;
            saveDirContentAtConfirmTime = File.ReadAllText(Path.Combine(saveDir, "save.dat"));
            return true;
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, confirmCallCount);
        Assert.Equal(saveDir, capturedPrompt!.SaveDir);
        Assert.NotNull(capturedPrompt.SafetyBackupPath);
        Assert.True(File.Exists(capturedPrompt.SafetyBackupPath));
        Assert.Equal("still-here-when-confirm-runs", saveDirContentAtConfirmTime);
    }

    [Fact]
    public void Declining_confirmation_leaves_the_save_directory_unchanged_and_no_restore_sibling_behind()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "unchanged-content");

        var result = _service.Restore(game, backupRoot, archive, _ => false);

        Assert.False(result.Success);
        // Declining is not a failure (SPEC: "Declining changes nothing") — Cancelled is what tells
        // the view model's Report() to show only the status line, not an error dialog.
        Assert.True(result.Cancelled);
        Assert.Contains("cancelled", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("unchanged-content", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
        // The safety backup covered a replacement that never happened, so it is removed again —
        // a restore that was only looked at must not leave an archive behind each time.
        Assert.Empty(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-*"));
    }

    [Fact]
    public void Restore_is_refused_when_the_backup_folder_sits_inside_the_save_folder()
    {
        var game = GameWithSaves();
        var saveDir = SaveDirIn(_dir);
        var backupRoot = Path.Combine(saveDir, "Backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var confirmCalls = 0;
        var result = _service.Restore(game, backupRoot, archive, _ => { confirmCalls++; return true; });

        Assert.False(result.Success);
        Assert.False(result.Cancelled);
        Assert.Contains("inside", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, confirmCalls);
        Assert.True(File.Exists(archive));
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
    }

    [Fact]
    public void Safety_backup_failure_aborts_before_confirm_and_leaves_the_save_directory_untouched()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "must-survive");

        // A file, not a directory, occupies the path the safety backup's EnsureWritable needs to
        // create — mirrors BackupServiceTests's own unavailable-backup-folder fixture.
        var unwritableBackupRoot = Path.Combine(_dir, "unwritable-backups");
        Directory.CreateDirectory(unwritableBackupRoot);
        File.WriteAllText(Path.Combine(unwritableBackupRoot, game.Id), "not a directory");

        var confirmInvoked = false;
        var result = _service.Restore(game, unwritableBackupRoot, archive, _ => { confirmInvoked = true; return true; });

        Assert.False(result.Success);
        Assert.False(confirmInvoked);
        Assert.Equal("must-survive", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
    }

    [Fact]
    public void Restoring_into_a_missing_save_directory_skips_the_safety_backup_and_completes()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        Directory.Delete(saveDir, recursive: true);
        Assert.False(Directory.Exists(saveDir));

        RestorePrompt? capturedPrompt = null;
        var result = _service.Restore(game, backupRoot, archive, prompt => { capturedPrompt = prompt; return true; });

        Assert.True(result.Success, result.Message);
        Assert.Null(capturedPrompt!.SafetyBackupPath);
        Assert.Empty(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));
        Assert.True(File.Exists(Path.Combine(saveDir, "save.dat")));
        Assert.True(File.Exists(Path.Combine(saveDir, "nested", "inner.dat")));
    }

    [Fact]
    public void Restoring_into_an_existing_empty_save_directory_skips_the_safety_backup_and_completes()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        Directory.Delete(saveDir, recursive: true);
        Directory.CreateDirectory(saveDir);

        RestorePrompt? capturedPrompt = null;
        var result = _service.Restore(game, backupRoot, archive, prompt => { capturedPrompt = prompt; return true; });

        Assert.True(result.Success, result.Message);
        Assert.Null(capturedPrompt!.SafetyBackupPath);
        Assert.Empty(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));
        Assert.True(File.Exists(Path.Combine(saveDir, "save.dat")));
    }

    [Fact]
    public void An_archive_containing_a_zero_byte_file_round_trips_and_verifies()
    {
        var saveDir = SaveDirIn(_dir);
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "empty.dat"), []);
        var game = new Game
        {
            Id = "test-game",
            DisplayName = "Test Game",
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };

        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        File.Delete(Path.Combine(saveDir, "empty.dat"));
        File.WriteAllText(Path.Combine(saveDir, "mutated.dat"), "changed");

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(saveDir, "empty.dat")));
        Assert.Equal(0, new FileInfo(Path.Combine(saveDir, "empty.dat")).Length);
        Assert.False(File.Exists(Path.Combine(saveDir, "mutated.dat")));
    }

    [Fact]
    public void Restoring_the_same_archive_twice_succeeds_twice_with_two_distinct_safety_backups_and_identical_final_contents()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);

        var first = _service.Restore(game, backupRoot, archive, _ => true);
        Assert.True(first.Success, first.Message);
        var firstPreRestore = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip"));

        // Forces the second safety backup's timestamp into a different second so the two archives
        // never collide on the same file name (BackupService.Backup composes names to the second).
        Thread.Sleep(1100);

        var second = _service.Restore(game, backupRoot, archive, _ => true);
        Assert.True(second.Success, second.Message);
        var secondPreRestoreAll = Directory.GetFiles(BackupDirFor(backupRoot, game), "*-pre-restore.zip");
        Assert.Equal(2, secondPreRestoreAll.Length);
        Assert.Contains(firstPreRestore, secondPreRestoreAll);

        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
        Assert.Equal("nested-payload", File.ReadAllText(Path.Combine(saveDir, "nested", "inner.dat")));
    }

    [Fact]
    public void A_truncated_archive_fails_extraction_leaves_the_save_dir_unchanged_and_removes_the_temp_dir()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var bytes = File.ReadAllBytes(archive);
        File.WriteAllBytes(archive, bytes[..(bytes.Length / 2)]);

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.False(result.Success);
        Assert.StartsWith($"Restore failed for {game.DisplayName}:", result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*"));
    }

    [Fact]
    public void A_plain_text_file_renamed_to_zip_fails_extraction_and_leaves_the_save_dir_unchanged()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var gameBackupDir = Directory.CreateDirectory(BackupDirFor(backupRoot, game));
        var notAnArchive = Path.Combine(gameBackupDir.FullName, "fake.zip");
        File.WriteAllText(notAnArchive, "not a zip file at all");

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        var result = _service.Restore(game, backupRoot, notAnArchive, _ => true);

        Assert.False(result.Success);
        Assert.StartsWith($"Restore failed for {game.DisplayName}:", result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*"));
    }

    [Fact]
    public void A_zero_byte_archive_file_fails_extraction_and_leaves_the_save_dir_unchanged()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var gameBackupDir = Directory.CreateDirectory(BackupDirFor(backupRoot, game));
        var zeroByteArchive = Path.Combine(gameBackupDir.FullName, "empty.zip");
        File.WriteAllBytes(zeroByteArchive, []);

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        var result = _service.Restore(game, backupRoot, zeroByteArchive, _ => true);

        Assert.False(result.Success);
        Assert.StartsWith($"Restore failed for {game.DisplayName}:", result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*"));
    }

    [Fact]
    public void A_missing_archive_path_fails_extraction_and_leaves_the_save_dir_unchanged()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var missingArchive = Path.Combine(BackupDirFor(backupRoot, game), "does-not-exist.zip");

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        var result = _service.Restore(game, backupRoot, missingArchive, _ => true);

        Assert.False(result.Success);
        Assert.StartsWith($"Restore failed for {game.DisplayName}:", result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*"));
    }

    [Fact]
    public void A_verification_mismatch_names_the_archive_path_and_detail_and_removes_the_temp_dir()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");

        // A structurally valid archive whose one entry name uses a backslash. On Windows, extraction
        // treats the backslash as a path separator and lands the file at "sub/file.dat" on disk, but
        // DescribeVerificationFailure's archive-side dictionary key is the entry's raw, unnormalized
        // FullName ("sub\file.dat") — the two disagree, deterministically producing a mismatch
        // without any binary corruption of the archive. The archive lives in the game's own backup
        // folder — the provenance guard refuses archives from anywhere else before
        // verification ever sees them.
        Directory.CreateDirectory(BackupDirFor(backupRoot, game));
        var craftedArchive = Path.Combine(BackupDirFor(backupRoot, game), "crafted.zip");
        using (var zip = ZipFile.Open(craftedArchive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("sub\\file.dat");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("nested-content");
        }

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        var result = _service.Restore(game, backupRoot, craftedArchive, _ => true);

        Assert.False(result.Success);
        Assert.Equal(
            $"Restore verification failed for {craftedArchive}: \"sub/file.dat\" is missing from the archive.",
            result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*"));
    }

    [Fact]
    public void Repeating_a_failed_restore_produces_the_identical_error_twice_with_no_growing_debris()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var bytes = File.ReadAllBytes(archive);
        File.WriteAllBytes(archive, bytes[..(bytes.Length / 2)]);

        var first = _service.Restore(game, backupRoot, archive, _ => true);
        Assert.False(first.Success);
        var siblingsAfterFirst = Directory.GetDirectories(_dir, "saves.savegamebunker-restore-*");

        // Forces the second attempt's own safety-backup timestamp into a different second, so the
        // two safety backups never collide on the same file name (BackupService.Backup composes
        // names to the second) — a collision would fail the *safety backup*, not repeat the
        // truncated-archive error this test is pinning.
        Thread.Sleep(1100);

        var second = _service.Restore(game, backupRoot, archive, _ => true);
        Assert.False(second.Success);
        var siblingsAfterSecond = Directory.GetDirectories(_dir, "saves.savegamebunker-restore-*");

        Assert.Equal(first.Message, second.Message);
        Assert.Empty(siblingsAfterFirst);
        Assert.Equal(siblingsAfterFirst.Length, siblingsAfterSecond.Length);
    }

    [Fact]
    public void A_locked_save_folder_fails_the_remove_old_step_leaves_the_save_dir_intact_and_preserves_the_verified_temp_dir()
    {
        var game = GameWithNestedSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        var snapshot = Snapshot(saveDir);

        // A held read handle on a file inside the save directory blocks the ancestor
        // Directory.Move the same way a running game holding its save file open would — proven
        // empirically to block regardless of share mode, while ReadWrite|Delete still permits the
        // concurrent reads the safety backup and extraction stages need. TestFileSystem's
        // ACL-deny helper was tried first and does not reproduce this: an ACE denying
        // List/Read on a subdirectory or on the save directory itself does not stop
        // Directory.Move, which needs no read access into the tree it renames.
        var lockedFile = Path.Combine(saveDir, "nested", "inner.dat");
        using var handle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.False(result.Success);
        Assert.StartsWith($"Restore failed for {game.DisplayName}: the save folder is in use (", result.Message);
        Assert.Equal(snapshot, Snapshot(saveDir));

        // The already-verified temp dir is deliberately left in place rather than deleted.
        var tmpSiblings = Directory.GetDirectories(_dir, "saves.savegamebunker-restore-tmp-*");
        Assert.Single(tmpSiblings);
    }

    [Fact]
    public void CleanStaleRestoreTemp_clears_leftover_tmp_siblings_but_never_old_siblings()
    {
        // A "-old-" sibling is the live save data a previous run's final swap failed to put back
        // (see the swap-failure test below) — the one copy of the user's data that still exists.
        // The stale-temp sweep must never delete it, only ever the disposable "-tmp-" extraction
        // siblings.
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        var staleTmp = $"{saveDir}.savegamebunker-restore-tmp-{Guid.NewGuid():N}";
        var staleOld = $"{saveDir}.savegamebunker-restore-old-{Guid.NewGuid():N}";
        Directory.CreateDirectory(staleTmp);
        Directory.CreateDirectory(staleOld);
        File.WriteAllText(Path.Combine(staleTmp, "leftover.dat"), "debris");

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.False(Directory.Exists(staleTmp));
        Assert.True(Directory.Exists(staleOld));
    }

    [Fact]
    public void CleanStaleRestoreTemp_is_scoped_to_this_games_own_leftover_siblings()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        // Its leaf name is "other-game", not "saves" — the sweep's own leaf-name prefix must not
        // match it.
        var unrelatedSibling = Path.Combine(_dir, $"other-game.savegamebunker-restore-tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(unrelatedSibling);
        File.WriteAllText(Path.Combine(unrelatedSibling, "keep.dat"), "keep-me");

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.True(Directory.Exists(unrelatedSibling));
    }

    [Fact]
    public void A_successful_restores_message_never_mentions_the_stale_temp_sweep()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        var staleTmp = $"{saveDir}.savegamebunker-restore-tmp-{Guid.NewGuid():N}";
        Directory.CreateDirectory(staleTmp);

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.Contains(game.DisplayName, result.Message);
        Assert.DoesNotContain(archive, result.Message);   // SHIP: no stored path in a success message
    }

    [Fact]
    public void CleanStaleRestoreTemp_is_best_effort_and_a_restore_still_succeeds_with_an_undeletable_leftover()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        var undeletableStale = $"{saveDir}.savegamebunker-restore-tmp-{Guid.NewGuid():N}";
        Directory.CreateDirectory(undeletableStale);
        File.WriteAllText(Path.Combine(undeletableStale, "debris.dat"), "debris");

        if (!TestFileSystem.TryDenyAccess(undeletableStale))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
    }

    // --- The three restore progress steps -------------------------------------------
    //
    // The step boundaries are only observable from inside RestoreService: the line between
    // extraction and swap is not visible to a caller, so reporting them from the view model
    // would be fabricated progress. These pin the contract.

    [Fact]
    public void A_restore_reports_its_three_steps_in_order_and_exactly_once_each()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var steps = new List<int>();

        var result = _service.Restore(game, backupRoot, archive, _ => true, new SynchronousProgress(steps.Add));

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { 1, 2, 3 }, steps);
    }

    [Fact]
    public void A_restore_cancelled_at_the_confirmation_reports_step_one_and_no_further_step()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var steps = new List<int>();

        var result = _service.Restore(game, backupRoot, archive, _ => false, new SynchronousProgress(steps.Add));

        Assert.False(result.Success);
        Assert.Equal(new[] { 1 }, steps);
    }

    [Fact]
    public void A_restore_whose_safety_backup_fails_reports_step_one_and_no_further_step()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        // Make the safety backup fail by denying access to the destination it writes into.
        var gameBackupDir = BackupDirFor(backupRoot, game);
        if (!TestFileSystem.TryDenyAccess(gameBackupDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var steps = new List<int>();

        var result = _service.Restore(game, backupRoot, archive, _ => true, new SynchronousProgress(steps.Add));

        Assert.False(result.Success);
        Assert.Equal(new[] { 1 }, steps);
    }

    [Fact]
    public void A_restore_that_needs_no_safety_backup_still_reports_all_three_steps()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        // An empty save folder owes no safety backup, but the stage is still walked.
        var saveDir = SaveDirIn(_dir);
        Directory.Delete(saveDir, recursive: true);
        Directory.CreateDirectory(saveDir);

        var steps = new List<int>();

        var result = _service.Restore(game, backupRoot, archive, _ => true, new SynchronousProgress(steps.Add));

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { 1, 2, 3 }, steps);
    }

    [Fact]
    public void A_restore_given_no_progress_object_behaves_exactly_as_before()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var saveDir = SaveDirIn(_dir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "mutated");

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
    }

    [Fact]
    public void ExceedsArchiveLimits_is_false_for_a_normal_backup()
    {
        var game = GameWithSaves();
        var backupRoot = Path.Combine(_dir, "backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        Assert.False(RestoreService.ExceedsArchiveLimits(archive));
    }

    [Fact]
    public void ExceedsArchiveLimits_trips_when_entry_count_exceeds_the_cap()
    {
        var archive = Path.Combine(_dir, "many.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            zip.CreateEntry("a.dat");
            zip.CreateEntry("b.dat");
            zip.CreateEntry("c.dat");
        }

        Assert.True(RestoreService.ExceedsArchiveLimits(archive, maxEntries: 2));
        Assert.False(RestoreService.ExceedsArchiveLimits(archive, maxEntries: 3));
    }

    [Fact]
    public void ExceedsArchiveLimits_trips_when_uncompressed_size_exceeds_the_cap()
    {
        var archive = Path.Combine(_dir, "fat.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("a.dat");
            using var stream = entry.Open();
            stream.Write(new byte[10]);
        }

        Assert.True(RestoreService.ExceedsArchiveLimits(archive, maxUncompressedBytes: 9));
        Assert.False(RestoreService.ExceedsArchiveLimits(archive, maxUncompressedBytes: 10));
    }

    // D-02: every layout restore refuses is pinned by a test that checks nothing was deleted —
    // the archive, the other archives and the save files all survive a refused restore.

    [Fact]
    public void A_refused_restore_with_the_backup_folder_inside_the_save_folder_deletes_nothing()
    {
        var game = GameWithSaves();
        var saveDir = SaveDirIn(_dir);
        var backupRoot = Path.Combine(saveDir, "Backups");
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.False(result.Success);
        Assert.True(File.Exists(archive));
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
        Assert.True(Directory.Exists(backupRoot));
        Assert.Empty(Directory.GetDirectories(_dir, "saves.savegamebunker-restore-*"));
    }

    [Fact]
    public void Restore_is_refused_when_the_backup_folder_is_the_save_folder_and_every_archive_survives()
    {
        var game = GameWithSaves();
        var saveDir = SaveDirIn(_dir);
        var backupRoot = saveDir;
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archives = Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip");
        Assert.Single(archives);

        var confirmCalls = 0;
        var result = _service.Restore(game, backupRoot, archives[0], _ => { confirmCalls++; return true; });

        Assert.False(result.Success);
        Assert.False(result.Cancelled);
        Assert.Contains("inside", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, confirmCalls);
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "save.dat")));
    }

    // D-11: a save folder equal to the game's own archive folder (<backupRoot>/<id>) would be
    // replaced — with every archive of the game inside it — by the rename-aside-and-delete restore
    // performs. Backup in that layout stays supported; replacing the folder is refused before the
    // first write, so the safety backup this method is about to take never happens either.
    [Fact]
    public void Restore_is_refused_when_the_save_folder_is_the_games_own_archive_folder_and_every_archive_survives()
    {
        var saveDir = Path.Combine(_dir, "b2", "x");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "a.dat"), "payload");
        var game = new Game { Id = "x", DisplayName = "X", SaveDirTemplate = saveDir.Replace('\\', '/') };
        var backupRoot = Path.Combine(_dir, "b2");

        // The staging file is a root-level file the walk may report; do not assert the message.
        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archives = Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip");
        Assert.Single(archives);

        var confirmCalls = 0;
        var result = _service.Restore(game, backupRoot, archives[0], _ => { confirmCalls++; return true; });

        Assert.False(result.Success);
        Assert.False(result.Cancelled);
        Assert.Contains("inside", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, confirmCalls);
        Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "a.dat")));
    }

    // The two layouts item 0 opened up round-trip: every subfolder file comes back through restore.

    [Fact]
    public void A_save_folder_inside_the_backup_folder_round_trips_its_subfolders_through_restore()
    {
        var saveDir = Path.Combine(_dir, "Documents", "My Games", "X");
        Directory.CreateDirectory(Path.Combine(saveDir, "profiles"));
        File.WriteAllText(Path.Combine(saveDir, "top.dat"), "payload");
        File.WriteAllText(Path.Combine(saveDir, "profiles", "p1.dat"), "profile-payload");
        var game = new Game { Id = "x-game", DisplayName = "X Game", SaveDirTemplate = saveDir.Replace('\\', '/') };
        var backupRoot = Path.Combine(_dir, "Documents");

        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var archive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        Directory.Delete(Path.Combine(saveDir, "profiles"), recursive: true);
        var result = _service.Restore(game, backupRoot, archive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.Equal("profile-payload", File.ReadAllText(Path.Combine(saveDir, "profiles", "p1.dat")));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "top.dat")));
    }

    [Fact]
    public void A_save_folder_inside_its_own_archive_folder_round_trips_its_subfolders_through_restore()
    {
        var saveDir = Path.Combine(_dir, "b3", "x", "saves");
        Directory.CreateDirectory(Path.Combine(saveDir, "sub"));
        File.WriteAllText(Path.Combine(saveDir, "a.dat"), "payload");
        File.WriteAllText(Path.Combine(saveDir, "sub", "b.dat"), "sub-payload");
        var game = new Game { Id = "x", DisplayName = "X", SaveDirTemplate = saveDir.Replace('\\', '/') };
        var backupRoot = Path.Combine(_dir, "b3");

        var backupResult = _backupService.Backup(game, backupRoot);
        Assert.True(backupResult.Success, backupResult.Message);
        var originalArchive = Assert.Single(Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip"));

        Directory.Delete(Path.Combine(saveDir, "sub"), recursive: true);
        var result = _service.Restore(game, backupRoot, originalArchive, _ => true);

        Assert.True(result.Success, result.Message);
        Assert.Equal("sub-payload", File.ReadAllText(Path.Combine(saveDir, "sub", "b.dat")));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(saveDir, "a.dat")));
        Assert.Equal(2, Directory.GetFiles(BackupDirFor(backupRoot, game), "*.zip").Length);
    }

    /// <summary>
    /// Records progress on the calling thread. The real <see cref="Progress{T}"/> posts to a
    /// synchronization context, which a plain xUnit host does not have, so its callbacks would
    /// arrive after the assertions.
    /// </summary>
    private sealed class SynchronousProgress(Action<int> onReport) : IProgress<int>
    {
        public void Report(int value) => onReport(value);
    }
}
