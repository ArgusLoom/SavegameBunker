using System.IO;
using System.Reflection;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// CLAUDE.md's never-throw error contract made executable: every public
/// <c>SavegameBunker.Services</c> method is called with caller-reachable hostile input and must
/// return its declared failure shape instead of letting an exception cross the boundary.
/// </summary>
/// <remarks>
/// The public surface is enumerated by reflection (never a hand list), so adding or removing a
/// public service method without a matching hostile-input case fails the surface fact below,
/// naming the method.
///
/// <para><b>Hostile input</b> means what the app's real callers can actually hand a method: any
/// string a hand-edited games.json can hold in a template, backupRoot, id or display name (empty,
/// whitespace, an unknown <c>%VARIABLE%</c>, invalid file-name characters); any filesystem state a
/// user's machine can be in (missing directory, missing file, a directory where a file is
/// expected, a file where a directory is expected); any value real arithmetic produces (zero,
/// negative from clock skew). Null only where the parameter is declared nullable. Values no caller
/// can produce (an empty path from a folder picker) are not hostile input, per CLAUDE.md's "no
/// error handling for impossible scenarios". Every path argument is rooted in this test's own temp
/// directory or is deliberately unresolvable, and <see cref="RestoreService.Restore"/> is always
/// called with a confirm delegate returning false — the pass must never be able to touch real
/// user data. No UNC or network path is used as hostile input, so a dead host cannot stall the
/// run.</para>
/// </remarks>
public sealed class ServiceContractTests : IDisposable
{
    /// <summary>
    /// The only members allowed to throw across the boundary: an unknown string key is a
    /// programmer error that must fail loudly, and that contract is pinned separately by
    /// <see cref="StringsTests.An_unknown_key_throws_loudly_with_the_key_name_in_the_message"/>.
    /// No further exemption is permitted without the owner's sign-off recorded in the plan's
    /// SUMMARY.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Exemptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strings.Get(String)"] =
                "An unknown key is a deliberate programmer-error throw, pinned by StringsTests.An_unknown_key_throws_loudly_with_the_key_name_in_the_message.",
            ["Strings.Format(String, Object[])"] =
                "Same unknown-key contract as Strings.Get(String) — a bad format key must throw, not render a placeholder sentence.",
        };

    private readonly TempDir _temp = new();
    private readonly string _dir;
    private readonly BackupService _service = new();
    private readonly Dictionary<string, List<Action>> _cases;

    public ServiceContractTests()
    {
        _dir = _temp.DirectoryPath;
        _cases = BuildCases();
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_public_service_surface_is_exactly_the_cases_plus_the_documented_exemptions()
    {
        var enumerated = EnumeratePublicServiceSurface();
        var expected = _cases.Keys.Concat(Exemptions.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();

        var missingCases = enumerated.Except(expected, StringComparer.Ordinal).ToList();
        var staleCases = expected.Except(enumerated, StringComparer.Ordinal).ToList();

        Assert.True(
            missingCases.Count == 0 && staleCases.Count == 0,
            $"Surface drift — add a hostile-input case (or an owner-approved exemption) for each missing key.\n"
            + $"Enumerated but not covered: {string.Join("; ", missingCases)}\n"
            + $"Covered but not enumerated: {string.Join("; ", staleCases)}");
    }

    [Fact]
    public void Every_public_service_method_returns_instead_of_throwing_on_hostile_input()
    {
        var offenders = new List<string>();

        foreach (var (key, invocations) in _cases)
        {
            foreach (var invoke in invocations)
            {
                try
                {
                    invoke();
                }
                catch (Exception ex)
                {
                    offenders.Add($"{key}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Services threw across their boundary on hostile input:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void A_failed_backup_call_leaves_the_same_service_instance_working()
    {
        var failing = _service.Backup(GameWithTemplate("%NOT_A_REAL_VARIABLE%/saves"), Root("unresolvable"));
        Assert.False(failing.Success);

        var succeeding = _service.Backup(GameWithRealSaves(), Root("after-failure"));
        Assert.True(succeeding.Success);

        Assert.Single(_service.ListBackups(Root("after-failure"), "contract-game"));
    }

    /// <summary>
    /// Enumerates the public <c>SavegameBunker.Services</c> surface by reflection. The key format
    /// is <c>Type.Name.Method(ParameterType.Name, ...)</c>, so the case table below keys against
    /// the same strings. Records (which declare a compiler-generated <c>&lt;Clone&gt;$</c>), the
    /// WPF-bound <see cref="DialogService"/>, interfaces and enums are outside the enumerated
    /// surface.
    /// </summary>
    private static List<string> EnumeratePublicServiceSurface()
    {
        var keys = new List<string>();

        foreach (var type in typeof(BackupService).Assembly.GetTypes())
        {
            if (!type.IsPublic || type.Namespace != "SavegameBunker.Services")
                continue;
            if (type.IsInterface || type.IsEnum)
                continue;
            // DialogService is the WPF-bound implementation of IDialogService — outside the
            // enumerated surface by the plan's own exemption (it marshals dialogs onto the UI
            // thread; a hostile-input call cannot reach a service contract).
            if (type.Name == nameof(DialogService))
                continue;

            var declared = type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

            // A record's compiler-generated <Clone>$ is the marker the plan pins: record types
            // (CatalogLoad) are data carriers, not service surface.
            if (declared.Any(m => m.Name == "<Clone>$"))
                continue;

            foreach (var method in declared)
            {
                if (method.IsSpecialName)
                    continue;
                if (method.GetCustomAttributesData().Any(a =>
                        a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
                    continue;

                keys.Add($"{type.Name}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
            }
        }

        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private Dictionary<string, List<Action>> BuildCases()
    {
        var restore = new RestoreService(_service);
        var existingDirectory = Root("catalog-dir");
        Directory.CreateDirectory(existingDirectory);
        var notADirectory = Root("file-not-dir");
        File.WriteAllText(notADirectory, "x");
        var dropA = Root("drop-a");
        var dropB = Root("drop-b");
        Directory.CreateDirectory(dropA);
        Directory.CreateDirectory(dropB);

        return new Dictionary<string, List<Action>>(StringComparer.Ordinal)
        {
            ["BackupComparer.Compare(SaveStatus, Boolean, IReadOnlyList`1, Nullable`1)"] =
            [
                () => _ = BackupComparer.Compare(SaveStatus.PathError("x"), true, [], null),
                () => _ = BackupComparer.Compare(SaveStatus.NoSaves(), false, [], null),
            ],
            ["BackupService.Backup(Game, String, IProgress`1)"] =
            [
                // A template naming a variable that does not exist cannot resolve — the backup
                // must fail with a message, never an exception.
                () => Assert.False(_service.Backup(GameWithTemplate("%NOT_A_REAL_VARIABLE%/saves"), Root("unresolvable")).Success),
                // A file where the backup root directory is expected.
                () => Assert.False(_service.Backup(GameWithRealSaves(), notADirectory).Success),
            ],
            ["BackupService.Backup(Game, String, String, String&, IProgress`1)"] =
            [
                // The suffix lands in the archive's file name, so invalid file-name characters
                // must fail the call and leave no archive path behind.
                () =>
                {
                    var result = _service.Backup(GameWithRealSaves(), Root("suffix"), "|<>", out var archivePath);
                    Assert.False(result.Success);
                    Assert.Null(archivePath);
                },
            ],
            ["BackupService.ComputeSaveDataSize(Game)"] =
            [
                () => _ = _service.ComputeSaveDataSize(GameWithTemplate("%NOT_A_REAL_VARIABLE%/saves")),
            ],
            ["BackupService.DeleteArchive(String)"] =
            [
                () => _ = _service.DeleteArchive(""),
                () => _ = _service.DeleteArchive(Missing("no-such-archive.zip")),
            ],
            ["BackupService.IsBackupRootReachable(String)"] =
            [
                () => _ = _service.IsBackupRootReachable(""),
                () => _ = _service.IsBackupRootReachable(Missing("gone")),
            ],
            ["BackupService.ListBackups(String, String)"] =
            [
                () => _ = _service.ListBackups(Missing("gone"), "contract-game"),
                () => _ = _service.ListBackups(notADirectory, "contract-game"),
            ],
            ["BackupService.TryGetAvailableFreeSpace(String, Int64&)"] =
            [
                () => _ = _service.TryGetAvailableFreeSpace("", out _),
                // A relative path is never a configured root; deliberately never a UNC path,
                // which could stall the suite on a dead host.
                () => _ = _service.TryGetAvailableFreeSpace("relative/dir", out _),
            ],
            ["BackupService.TryListBackups(String, String, IReadOnlyList`1&)"] =
            [
                () => _ = _service.TryListBackups(Missing("gone"), "contract-game", out _),
                () => _ = _service.TryListBackups(notADirectory, "contract-game", out _),
            ],
            ["CrashReport.Describe(Exception, String)"] =
            [
                () => _ = CrashReport.Describe(
                    new InvalidOperationException("outer", new InvalidCastException("inner")), ""),
            ],
            ["DropFolder.TrySingleExistingDirectory(String[], String&)"] =
            [
                () => _ = DropFolder.TrySingleExistingDirectory(null, out _),
                () => _ = DropFolder.TrySingleExistingDirectory([], out _),
                () => _ = DropFolder.TrySingleExistingDirectory([Missing("nope")], out _),
                () => _ = DropFolder.TrySingleExistingDirectory([dropA, dropB], out _),
            ],
            ["ElapsedFormat.Describe(TimeSpan)"] =
            [
                () => _ = ElapsedFormat.Describe(TimeSpan.Zero),
                () => _ = ElapsedFormat.Describe(TimeSpan.FromHours(-3)),
            ],
            ["GameCatalog.LoadAsync(String)"] =
            [
                // A directory where the catalog file is expected.
                () => _ = GameCatalog.LoadAsync(existingDirectory).GetAwaiter().GetResult(),
                // A malformed catalog is reported, and the file the user hand-edited is never
                // overwritten — its bytes must be identical afterwards.
                () =>
                {
                    var malformed = Root("malformed-games.json");
                    File.WriteAllText(malformed, "{");
                    var before = File.ReadAllBytes(malformed);
                    _ = GameCatalog.LoadAsync(malformed).GetAwaiter().GetResult();
                    Assert.Equal(before, File.ReadAllBytes(malformed));
                },
            ],
            ["GameCatalog.ResolvePath(IReadOnlyList`1)"] =
            [
                () => _ = GameCatalog.ResolvePath([]),
                () => _ = GameCatalog.ResolvePath([""]),
                () => _ = GameCatalog.ResolvePath(["   "]),
            ],
            ["GameCatalog.SaveAsync(String, Catalog)"] =
            [
                () => Assert.False(GameCatalog.SaveAsync(existingDirectory, new Catalog()).GetAwaiter().GetResult().Success),
            ],
            ["GameCatalog.SaveAsync(String, Catalog, Nullable`1)"] =
            [
                () => Assert.False(GameCatalog.SaveAsync(existingDirectory, new Catalog(), null).GetAwaiter().GetResult().Success),
                () => Assert.False(GameCatalog.SaveAsync(existingDirectory, new Catalog(), DateTime.MinValue).GetAwaiter().GetResult().Success),
            ],
            ["GameCatalog.TryResolveBackupRoot(CatalogLoad, String&, String&)"] =
            [
                () => _ = GameCatalog.TryResolveBackupRoot(LoadWithRoot("%NOT_A_REAL_VARIABLE%/b"), out _, out _),
                () => _ = GameCatalog.TryResolveBackupRoot(LoadWithRoot(""), out _, out _),
            ],
            ["HelpPath.LanguageFor(String)"] =
            [
                () => _ = HelpPath.LanguageFor(null),
                () => _ = HelpPath.LanguageFor(""),
                () => _ = HelpPath.LanguageFor("xx"),
            ],
            ["HelpPath.Resolve(String, String)"] =
            [
                () => _ = HelpPath.Resolve(Missing("help"), "en"),
            ],
            ["IdSlug.Derive(String)"] =
            [
                () => _ = IdSlug.Derive(""),
                () => _ = IdSlug.Derive("!!!"),
                () => _ = IdSlug.Derive("   "),
            ],
            ["IdSlug.IsSafe(String)"] =
            [
                () => _ = IdSlug.IsSafe(""),
                () => _ = IdSlug.IsSafe(".."),
                () => _ = IdSlug.IsSafe("con"),
                () => _ = IdSlug.IsSafe(new string('a', 300)),
            ],
            ["KnownGamesCatalog.Find(String, Int32)"] =
            [
                () => _ = KnownGamesCatalog.Find("", 5),
                () => _ = KnownGamesCatalog.Find("zzzz-no-such-game", 0),
            ],
            ["KnownGamesCatalog.TryExactMatch(String, KnownGame&)"] =
            [
                () => _ = KnownGamesCatalog.TryExactMatch("", out _),
            ],
            ["KnownGamesScanner.Scan(IReadOnlyList`1, IReadOnlyList`1)"] =
            [
                () => _ = new KnownGamesScanner().Scan(
                    [
                        new KnownGame
                        {
                            Id = "known-game",
                            DisplayName = "Known Game",
                            SaveDirTemplate = "%NOT_A_REAL_VARIABLE%/x",
                        },
                    ],
                    [GameWithTemplate("")]),
            ],
            ["KnownGamesScanner.SharedTemplates(IReadOnlyList`1)"] =
            [
                () => _ = KnownGamesScanner.SharedTemplates([]),
            ],
            ["PathResolver.ToTemplate(String)"] =
            [
                // What the folder picker and drag-drop actually hand it: a real directory path
                // and its drive root.
                () => _ = PathResolver.ToTemplate(_dir),
                () => _ = PathResolver.ToTemplate(Path.GetPathRoot(Path.GetFullPath(_dir))!),
            ],
            ["PathResolver.TryResolve(String, String, String&, String&)"] =
            [
                () => _ = PathResolver.TryResolve(null, null, out _, out _),
                () => _ = PathResolver.TryResolve("", null, out _, out _),
                () => _ = PathResolver.TryResolve("%NOT_A_REAL_VARIABLE%/x", null, out _, out _),
            ],
            ["RestoreService.Restore(Game, String, String, Func`2, IProgress`1)"] =
            [
                // A missing archive path and a confirm delegate that always returns false, so no
                // save folder can ever actually be swapped by this case.
                () => Assert.False(restore.Restore(
                    GameWithRealSaves(),
                    Root("restore-root"),
                    Missing("missing-archive.zip"),
                    _ => false).Success),
            ],
            ["SaveStatusScanner.Scan(Game)"] =
            [
                () => _ = SaveStatusScanner.Scan(GameWithTemplate("")),
                () => _ = SaveStatusScanner.Scan(GameWithTemplate("%NOT_A_REAL_VARIABLE%/x")),
            ],
            ["ShellOpen.Classify(String)"] =
            [
                () => _ = ShellOpen.Classify(null),
                () => _ = ShellOpen.Classify(""),
                () => _ = ShellOpen.Classify(Missing("nothing-here.txt")),
            ],
            ["SizeFormat.Describe(Int64)"] =
            [
                () => _ = SizeFormat.Describe(0),
                () => _ = SizeFormat.Describe(-1),
            ],
            ["Strings.Preload()"] =
            [
                () => Strings.Preload(),
            ],
            ["WikiUrl.IsAllowed(String)"] =
            [
                () => _ = WikiUrl.IsAllowed(null),
                () => _ = WikiUrl.IsAllowed(""),
                () => _ = WikiUrl.IsAllowed("javascript:alert(1)"),
                () => _ = WikiUrl.IsAllowed("http://example.com"),
            ],
            ["WikiUrl.Search(String)"] =
            [
                () => _ = WikiUrl.Search(""),
                () => _ = WikiUrl.Search("Ōkami & Co / 100%"),
            ],
        };
    }

    private string Root(string name) => Path.Combine(_dir, name).Replace('\\', '/');

    /// <summary>A path inside the fixture directory that was never created — a missing file/dir.</summary>
    private string Missing(string name) => Root(name);

    private static Game GameWithTemplate(string template) => new()
    {
        Id = "contract-game",
        DisplayName = "Contract Game",
        SaveDirTemplate = template,
    };

    private Game GameWithRealSaves()
    {
        var saveDir = Root("saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");
        return GameWithTemplate(saveDir);
    }

    private static CatalogLoad LoadWithRoot(string backupRoot) => new(
        "games.json",
        new Catalog { BackupRoot = backupRoot },
        [],
        Seeded: false,
        FatalError: null);
}
