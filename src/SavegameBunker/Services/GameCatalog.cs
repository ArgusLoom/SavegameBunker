using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Outcome of loading a catalog file. Loading never throws: startup has no fatal path, and a
/// problem with the file has to be visible in the window rather than kill the application.
/// </summary>
/// <param name="Path">Absolute path of the file that was loaded, seeded or rejected.</param>
/// <param name="Catalog">Valid contents. Empty when <paramref name="FatalError"/> is set.</param>
/// <param name="Problems">Per-entry complaints; the offending entries were dropped.</param>
/// <param name="Seeded">True when the file was absent and a template was written.</param>
/// <param name="FatalError">Set when the file exists but could not be read or parsed.</param>
/// <param name="LastWriteTimeUtc">
/// The file's own last-write time at the moment this content was read — <see cref="SaveAsync"/>'s
/// optimistic-concurrency stamp (see its own doc comment). Null when there is nothing on disk
/// yet to stamp (a fatal read, or a freshly seeded file this same call just wrote — the write is
/// followed by nothing that could race it before the seeded catalog is first saved for real).
/// </param>
public sealed record CatalogLoad(
    string Path,
    Catalog Catalog,
    IReadOnlyList<string> Problems,
    bool Seeded,
    string? FatalError,
    DateTime? LastWriteTimeUtc = null);

/// <summary>
/// Loads and writes the catalog file that drives the application.
/// </summary>
public static class GameCatalog
{
    public const string DefaultFileName = "games.json";

    /// <summary>
    /// Serialises catalog writes. The application edits one catalog, so a single process-wide gate
    /// is enough — it is not keyed by path. It guards against overlapping in-process saves only;
    /// another process editing the same file concurrently is outside what this can promise.
    /// </summary>
    private static readonly SemaphoreSlim SaveGate = new(1, 1);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The catalog used when none was named on the command line.</summary>
    public static string DefaultPath => System.IO.Path.Combine(AppContext.BaseDirectory, DefaultFileName);

    /// <summary>
    /// Decides which catalog to load: the path given on the command line, otherwise
    /// games.json next to the executable. Resolution is stateless — nothing is remembered
    /// between runs, so the same executable can drive any number of catalogs.
    /// </summary>
    public static string ResolvePath(IReadOnlyList<string> args) =>
        args.Count > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? System.IO.Path.GetFullPath(args[0])
            : DefaultPath;

    /// <summary>
    /// Reads a catalog. An absent file is seeded with a commented template; a malformed one is
    /// reported and left byte-for-byte alone, because overwriting it would destroy a catalog the
    /// user may have hand-edited.
    /// </summary>
    public static Task<CatalogLoad> LoadAsync(string path) => Task.Run(() => LoadCoreAsync(path));

    private static async Task<CatalogLoad> LoadCoreAsync(string path)
    {
        if (!File.Exists(path))
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(path, SeedTemplate);
            }
            catch (Exception ex)
            {
                return Fatal(path, $"No catalog at {path}, and it could not be created: {ex.Message}");
            }

            return new CatalogLoad(path, new Catalog(), [], Seeded: true, FatalError: null,
                LastWriteTimeUtc: File.GetLastWriteTimeUtc(path));
        }

        string json;
        DateTime lastWriteTimeUtc;
        try
        {
            json = await File.ReadAllTextAsync(path);
            // Read right after the content, not before: the stamp only has to be no newer than
            // what was actually read, so SaveAsync's later comparison never lets a write past that
            // it otherwise should have caught. Older than the content (a write landing in between)
            // just makes SaveAsync's check needlessly strict for that one save, not wrong.
            lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex)
        {
            return Fatal(path, $"{path} could not be read: {ex.Message}");
        }

        Catalog? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Catalog>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            return Fatal(path, $"{path} is not valid JSON: {ex.Message}");
        }

        if (raw is null)
            return Fatal(path, $"{path} is empty.");

        if (raw.Games is null)
            return Fatal(path, $"{path}: \"games\" must be a list, not null.");

        return Validate(path, raw, lastWriteTimeUtc);
    }

    /// <summary>
    /// Writes the catalog atomically: serialize to a sibling temp file, then swap it in with
    /// <see cref="File.Replace(string, string, string?)"/> so <paramref name="path"/> is never
    /// observed partially written, even if the process dies mid-write. This is the one write
    /// routine — every write path (add game, change backup folder) calls it rather than writing
    /// the catalog file directly. The temp file lives beside <paramref name="path"/>, never under
    /// a system temp folder, because <c>File.Replace</c> requires both files on the same volume.
    /// No optimistic-concurrency check — equivalent to <see cref="SaveAsync(string, Catalog, DateTime?)"/>
    /// with a null stamp, which unconditionally overwrites.
    /// </summary>
    public static Task<OperationResult> SaveAsync(string path, Catalog catalog) =>
        SaveAsync(path, catalog, expectedLastWriteTimeUtc: null);

    /// <summary>
    /// Same write as <see cref="SaveAsync(string, Catalog)"/>, guarded by an optimistic-concurrency
    /// check against <paramref name="expectedLastWriteTimeUtc"/> — the file's own last-write time at
    /// the moment the catalog being saved was loaded (<see cref="CatalogLoad.LastWriteTimeUtc"/>).
    /// <see cref="SaveGate"/> only serialises writers inside this one process; nothing stops a
    /// second <c>SavegameBunker.exe</c> pointed at the same catalog from writing between this process's
    /// load and its save. Without this check that write is silently lost — <c>File.Replace</c>
    /// neither knows nor cares that the file it is replacing changed after this process last read
    /// it. A mismatch fails without touching the file at all, rather than overwriting the other
    /// process's change with a catalog that was never aware of it.
    /// <para>
    /// Alphabetical ordering happens here, at the one write boundary, not at load: a load that
    /// reordered would mean writing a file the user did not ask to change. The games
    /// list is projected with a stable <c>OrderBy</c> on <see cref="Game.DisplayName"/>
    /// (<see cref="StringComparer.OrdinalIgnoreCase"/>) rather than sorted in place, because
    /// the caller hands in its own live catalog and an in-place reorder would invalidate
    /// <c>CatalogSession.SetSaveFolderAsync</c>'s pre-save index rollback. <c>OrderBy</c> is
    /// stable, so two equal names never swap and churn the file; <see cref="List{T}.Sort()"/>
    /// is not.
    /// </para>
    /// </summary>
    public static Task<OperationResult> SaveAsync(string path, Catalog catalog, DateTime? expectedLastWriteTimeUtc) =>
        Task.Run(() => SaveCoreAsync(path, catalog, expectedLastWriteTimeUtc));

    private static async Task<OperationResult> SaveCoreAsync(string path, Catalog catalog, DateTime? expectedLastWriteTimeUtc)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
            return OperationResult.Fail($"Could not save {path}: the path has no directory.");

        // The temp name carries a GUID because two concurrent saves sharing one fixed name would
        // write over each other's staging file: the first writer's Replace would publish the
        // second writer's bytes, and the second would then fail on a temp file that no longer
        // exists. Uniqueness makes each save's staging file its own.
        var tempPath = System.IO.Path.Combine(dir, $".{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        // Uniqueness alone still leaves two saves publishing in an undefined order, so the last
        // writer could win with a catalog that never contained the other's change. The gate
        // serialises the publish step; "Add game" and "Change backup folder" both write the whole
        // catalog, so an interleave loses an entry rather than merging one.
        await SaveGate.WaitAsync();
        try
        {
            // Checked inside the gate, immediately before writing: the narrowest possible window
            // between the check and the write this process controls. A second process's write can
            // still land in that window — no cross-process lock closes it entirely — but this turns
            // the common case (a save that arrives well after the other process's own) into a named
            // refusal instead of a silent loss.
            if (expectedLastWriteTimeUtc is { } expected && File.Exists(path)
                && File.GetLastWriteTimeUtc(path) != expected)
            {
                return OperationResult.Fail(Strings.Get("str_save_changedOnDisk"));
            }

            // Serialize an ordered projection, never mutate the caller's list.
            var ordered = new Catalog
            {
                BackupRoot = catalog.BackupRoot,
                Games = [.. catalog.Games.OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)],
            };
            var json = JsonSerializer.Serialize(ordered, WriteOptions);
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                catch (FileNotFoundException)
                {
                    // The existence check above raced an external delete between it and the
                    // Replace — the same case the Move branch handles below, so handle it the
                    // same way (audit A7) instead of failing a save that is still publishable.
                    File.Move(tempPath, path);
                }
            }
            else
                File.Move(tempPath, path); // The catalog was deleted externally since it was loaded.

            return OperationResult.Ok($"Catalog saved to {path}.");
        }
        catch (Exception ex)
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
            return OperationResult.Fail($"Could not save {path}: {ex.Message}");
        }
        finally
        {
            SaveGate.Release();
        }
    }

    /// <summary>
    /// Resolves the catalog's <c>backupRoot</c>. A relative value is resolved next to the catalog
    /// file, so an executable and its catalog on a removable drive keep pointing at their own
    /// backups whatever drive letter Windows hands out.
    /// </summary>
    public static bool TryResolveBackupRoot(CatalogLoad load, out string path, out string? error) =>
        PathResolver.TryResolve(
            load.Catalog.BackupRoot,
            System.IO.Path.GetDirectoryName(load.Path),
            out path,
            out error);

    private static CatalogLoad Fatal(string path, string message) =>
        new(path, new Catalog(), [], Seeded: false, FatalError: message);

    /// <summary>
    /// Drops entries the application could not act on later and says why now, at load time,
    /// instead of surfacing them as a mysterious failure when a button is pressed.
    /// </summary>
    private static CatalogLoad Validate(string path, Catalog raw, DateTime lastWriteTimeUtc)
    {
        var problems = new List<string>();
        var games = new List<Game>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(raw.BackupRoot)
            && !PathResolver.TryResolve(raw.BackupRoot, System.IO.Path.GetDirectoryName(path), out _, out var rootError))
        {
            problems.Add($"backupRoot: {rootError}");
        }

        for (var i = 0; i < raw.Games.Count; i++)
        {
            var game = raw.Games[i];
            if (game is null)
            {
                problems.Add($"entry {i + 1}: is null, not a game object.");
                continue;
            }

            var label = string.IsNullOrWhiteSpace(game.DisplayName)
                ? $"entry {i + 1}"
                : game.DisplayName;

            if (string.IsNullOrWhiteSpace(game.Id))
            {
                problems.Add($"{label}: \"id\" is missing.");
                continue;
            }

            if (!IdSlug.IsSafe(game.Id))
            {
                problems.Add($"{label}: id \"{game.Id}\" may only contain a-z, 0-9 and '-'.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(game.DisplayName))
            {
                problems.Add($"{game.Id}: \"displayName\" is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(game.SaveDirTemplate))
            {
                problems.Add($"{label}: \"saveDirTemplate\" is missing.");
                continue;
            }

            if (!seen.Add(game.Id))
            {
                problems.Add($"{label}: duplicate id \"{game.Id}\".");
                continue;
            }

            games.Add(game);
        }

        var catalog = new Catalog { BackupRoot = raw.BackupRoot, Games = games };
        return new CatalogLoad(path, catalog, problems, Seeded: false, FatalError: null, lastWriteTimeUtc);
    }

    internal const string SeedTemplate = """
        // SavegameBunker catalog.
        //
        // This file lists the games SavegameBunker knows about and where their backups go.
        // Comments and trailing commas are allowed — but note that adding a game from the
        // application rewrites this file, and comments do not survive that.

        {
          // Where backups are written. A relative path is resolved next to THIS file, so an
          // executable and its catalog on a USB stick keep working whatever drive letter
          // Windows assigns them. Leave it out and SavegameBunker asks on the first backup.
          //
          // "backupRoot": "SavegameBunkerBackups",

          "games": [
            // Forward slashes are fine and save you doubling backslashes.
            //
            // Path variables: %SAVEDGAMES%, %DOCUMENTS%, %APPDATA%, %LOCALAPPDATA%, %USERPROFILE%.
            // %SAVEDGAMES% and %DOCUMENTS% are preferred over a profile-relative path: Windows
            // lets the user move both folders (OneDrive Known Folder Move relocates Documents;
            // Saved Games can be moved the same way), and a path hardcoded under %USERPROFILE%
            // stays wrong on any machine where that happened.
            //
            // {
            //   "id": "witcher3",
            //   "displayName": "The Witcher 3: Wild Hunt",
            //   "saveDirTemplate": "%DOCUMENTS%/The Witcher 3/gamesaves"
            // }
          ]
        }

        """;
}
