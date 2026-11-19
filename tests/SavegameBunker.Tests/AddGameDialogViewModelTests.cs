using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// A minimal <see cref="IDialogService"/> the view model tests never need to touch, beyond
/// <see cref="PickFolder"/> — every member throws so an unexpected call fails loudly.
/// </summary>
public sealed class ThrowingDialogService : IDialogService
{
    public string? FolderToReturn { get; set; }

    public void Info(string message) => throw new InvalidOperationException("Not expected to be called.");
    public void Error(string message) => throw new InvalidOperationException("Not expected to be called.");
    public bool Confirm(string message, string confirmButtonText) => throw new InvalidOperationException("Not expected to be called.");
    public string? PickFolder(string title, string? initialDirectory) => FolderToReturn;
    public AddGameResult? ShowAddGameDialog(IReadOnlyCollection<string> existingIds, string? initialSaveFolder = null) => throw new InvalidOperationException("Not expected to be called.");
    public IReadOnlyList<KnownGame>? ShowFindGamesDialog(ScanResult result) => throw new InvalidOperationException("Not expected to be called.");
    public OperationResult OpenInEditor(string path) => throw new InvalidOperationException("Not expected to be called.");
    public OperationResult OpenHttps(string url) => throw new InvalidOperationException("Not expected to be called.");
    public OperationResult OpenHelpGuide(string uiLanguage) => throw new InvalidOperationException("Not expected to be called.");
    public BackupArchive? ShowRestorePickerDialog(string gameDisplayName, IReadOnlyList<BackupArchive> backups,
        Func<string, OperationResult> deleteArchive) => throw new InvalidOperationException("Not expected to be called.");
    public string? ShowChangeSaveFolderDialog(string gameDisplayName, string currentTemplate) => throw new InvalidOperationException("Not expected to be called.");
    public void ShowAboutDialog() => throw new InvalidOperationException("Not expected to be called.");
}

/// <summary>
/// Exercises id derivation, inline validation, collision refusal and the resolve-and-report line
/// without a running window — a real temp directory backs the folders being resolved.
/// </summary>
public sealed class AddGameDialogViewModelTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;

    public AddGameDialogViewModelTests()
    {
        _dir = _temp.DirectoryPath;
    }

    public void Dispose() => _temp.Dispose();

    private static AddGameDialogViewModel CreateViewModel(params string[] existingIds) =>
        new(existingIds, new ThrowingDialogService());

    [Fact]
    public void Fresh_view_model_has_add_disabled()
    {
        var vm = CreateViewModel();

        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void Prefilling_save_folder_leaves_the_display_name_empty()
    {
        var vm = CreateViewModel();
        var folder = Path.Combine(_dir, "dropped-saves");
        Directory.CreateDirectory(folder);

        vm.SaveFolder = folder;

        Assert.Equal("", vm.DisplayName);
        Assert.Equal(folder, vm.SaveFolder);
    }

    [Fact]
    public void Wiki_search_is_disabled_when_the_name_is_blank()
    {
        var dialog = new RecordingDialogService();
        var vm = new AddGameDialogViewModel([], dialog);

        Assert.False(vm.OpenWikiSearchCommand.CanExecute(null));
    }

    [Fact]
    public void Wiki_search_opens_the_escaped_pcgw_search_url_for_the_typed_name()
    {
        var dialog = new RecordingDialogService();
        var vm = new AddGameDialogViewModel([], dialog)
        {
            DisplayName = "Celeste"
        };

        Assert.True(vm.OpenWikiSearchCommand.CanExecute(null));
        vm.OpenWikiSearchCommand.Execute(null);

        var url = Assert.Single(dialog.OpenHttpsUrls);
        Assert.Contains("https://www.pcgamingwiki.com/w/index.php?search=", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Celeste", url);
    }

    [Fact]
    public void Wiki_search_escapes_spaces_in_the_typed_name()
    {
        var dialog = new RecordingDialogService();
        var vm = new AddGameDialogViewModel([], dialog)
        {
            DisplayName = "Hollow Knight"
        };

        Assert.True(vm.OpenWikiSearchCommand.CanExecute(null));
        vm.OpenWikiSearchCommand.Execute(null);

        Assert.Equal(
            "https://www.pcgamingwiki.com/w/index.php?search=Hollow%20Knight",
            Assert.Single(dialog.OpenHttpsUrls));
    }

    [Fact]
    public void Typing_an_exact_alias_fills_the_official_name_and_save_folder()
    {
        var vm = CreateViewModel();

        vm.DisplayName = "Oblivion";

        Assert.Equal("The Elder Scrolls IV: Oblivion", vm.DisplayName);
        Assert.Equal("%DOCUMENTS%/My Games/Oblivion/Saves", vm.SaveFolder);
        Assert.Equal("the-elder-scrolls-iv-oblivion", vm.Id);
    }

    [Fact]
    public void Typing_an_exact_name_leaves_a_save_folder_the_user_supplied_alone()
    {
        var vm = CreateViewModel();
        vm.SaveFolder = "D:/Dropped/Saves";

        vm.DisplayName = "Oblivion";

        Assert.Equal("Oblivion", vm.DisplayName);
        Assert.Equal("D:/Dropped/Saves", vm.SaveFolder);
    }

    [Fact]
    public void Typing_a_second_exact_name_still_replaces_a_folder_an_earlier_suggestion_filled()
    {
        var vm = CreateViewModel();
        vm.DisplayName = "Oblivion";
        Assert.Equal("%DOCUMENTS%/My Games/Oblivion/Saves", vm.SaveFolder);

        vm.DisplayName = "Stardew Valley";

        Assert.Equal("%APPDATA%/StardewValley/Saves", vm.SaveFolder);
    }

    [Fact]
    public void Wiki_button_still_applies_an_exact_hit_over_a_user_supplied_folder()
    {
        var vm = CreateViewModel();
        vm.SaveFolder = "D:/Dropped/Saves";
        vm.DisplayName = "Oblivion";

        vm.OpenWikiSearchCommand.Execute(null);

        Assert.Equal("%DOCUMENTS%/My Games/Oblivion/Saves", vm.SaveFolder);
    }

    [Fact]
    public void Typing_a_substring_does_not_fill_the_save_folder()
    {
        var vm = CreateViewModel();

        vm.DisplayName = "witch";

        Assert.Equal("witch", vm.DisplayName);
        Assert.Equal("", vm.SaveFolder);
    }

    [Fact]
    public void Wiki_button_applies_an_exact_hit_instead_of_opening_the_browser()
    {
        var dialog = new RecordingDialogService();
        var vm = new AddGameDialogViewModel([], dialog)
        {
            DisplayName = "a game not in the table"
        };

        vm.OpenWikiSearchCommand.Execute(null);
        Assert.Single(dialog.OpenHttpsUrls);

        vm.DisplayName = "Oblivion";
        vm.OpenWikiSearchCommand.Execute(null);

        Assert.Single(dialog.OpenHttpsUrls);
        Assert.Equal("The Elder Scrolls IV: Oblivion", vm.DisplayName);
        Assert.Equal("%DOCUMENTS%/My Games/Oblivion/Saves", vm.SaveFolder);
    }

    [Fact]
    public void Setting_display_name_derives_the_id()
    {
        var vm = CreateViewModel();

        vm.DisplayName = "The Witcher 3";

        Assert.Equal("the-witcher-3", vm.Id);
    }

    [Fact]
    public void Picking_a_suggestion_uses_its_stored_id_verbatim_not_a_freshly_derived_one()
    {
        var vm = CreateViewModel();
        var suggestion = new KnownGame
        {
            Id = "tmnt-mutants-in-manhattan",
            DisplayName = "Teenage Mutant Ninja Turtles: Mutants in Manhattan",
            SaveDirTemplate = @"%APPDATA%\SyntheticTmnt",
        };

        vm.ApplySuggestion(suggestion);

        Assert.Equal("tmnt-mutants-in-manhattan", vm.Id);
        Assert.NotEqual(IdSlug.Derive(suggestion.DisplayName), vm.Id);
    }

    [Fact]
    public void A_later_display_name_edit_re_derives_the_id_after_a_verbatim_pick()
    {
        var vm = CreateViewModel();
        var suggestion = new KnownGame
        {
            Id = "some-other-id",
            DisplayName = "Some Other Game",
            SaveDirTemplate = @"%APPDATA%\SyntheticOther",
        };

        vm.ApplySuggestion(suggestion);
        Assert.Equal("some-other-id", vm.Id);

        vm.DisplayName = "Something Else Entirely";

        Assert.Equal(IdSlug.Derive("Something Else Entirely"), vm.Id);
    }

    [Fact]
    public void A_suggestion_with_a_blank_stored_id_falls_back_to_deriving_one_from_its_display_name()
    {
        var vm = CreateViewModel();
        var suggestion = new KnownGame
        {
            Id = "",
            DisplayName = "Some Backfill Pending Game",
            SaveDirTemplate = @"%APPDATA%\SyntheticPending",
        };

        vm.ApplySuggestion(suggestion);

        Assert.Equal(IdSlug.Derive(suggestion.DisplayName), vm.Id);
        Assert.NotEqual("", vm.Id);
    }

    [Fact]
    public void A_picked_suggestions_id_colliding_with_an_existing_one_case_insensitively_disables_add()
    {
        var vm = CreateViewModel("ELDEN-RING");
        vm.SaveFolder = @"C:\saves";
        var suggestion = new KnownGame
        {
            Id = "elden-ring",
            DisplayName = "Elden Ring",
            SaveDirTemplate = @"%APPDATA%\SyntheticEldenRing",
        };

        vm.ApplySuggestion(suggestion);

        Assert.Equal(
            "A game with a very similar name is already in your list. Try a slightly different name.",
            vm.ValidationMessage);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void A_picked_suggestions_id_containing_invalid_characters_disables_add()
    {
        var vm = CreateViewModel();
        vm.SaveFolder = @"C:\saves";
        var suggestion = new KnownGame
        {
            Id = "Elden Ring",
            DisplayName = "Elden Ring",
            SaveDirTemplate = @"%APPDATA%\SyntheticEldenRing2",
        };

        vm.ApplySuggestion(suggestion);

        Assert.Equal(
            "SavegameBunker couldn't turn that name into a save-folder name — add a letter or number to it.",
            vm.ValidationMessage);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void BuildResult_after_picking_a_suggestion_carries_the_tables_stored_id()
    {
        var vm = CreateViewModel();
        var suggestion = new KnownGame
        {
            Id = "tmnt-mutants-in-manhattan",
            DisplayName = "Teenage Mutant Ninja Turtles: Mutants in Manhattan",
            SaveDirTemplate = @"%APPDATA%\SyntheticTmnt2",
        };

        vm.ApplySuggestion(suggestion);

        Assert.Equal("tmnt-mutants-in-manhattan", vm.BuildResult().Id);
    }

    [Fact]
    public void Picking_a_suggestion_whose_template_is_shared_sets_a_message_naming_the_other_count()
    {
        var shared = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);
        // The shipped table carries exactly one collision group (Ubisoft launcher folder,
        // deliberately kept at 2 entries so this stays testable). A table edit
        // that removes every collision should send someone back to this test rather than let it
        // fail obscurely.
        Assert.NotEmpty(shared);

        var entry = KnownGamesCatalog.All.First(g => shared.Contains(g.SaveDirTemplate));
        var expectedOthers = KnownGamesCatalog.All.Count(g =>
            !ReferenceEquals(g, entry)
            && string.Equals(g.SaveDirTemplate, entry.SaveDirTemplate, StringComparison.OrdinalIgnoreCase));

        var vm = CreateViewModel();
        vm.ApplySuggestion(entry);

        Assert.NotEqual("", vm.SharedTemplateMessage);
        Assert.Equal(AddGameDialogViewModel.SharedTemplateSentence(expectedOthers), vm.SharedTemplateMessage);
        Assert.True(vm.CanAdd);
    }

    [Fact]
    public void Picking_a_suggestion_whose_template_is_not_shared_leaves_the_message_empty()
    {
        var shared = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);
        var entry = KnownGamesCatalog.All.First(g => !shared.Contains(g.SaveDirTemplate));

        var vm = CreateViewModel();
        vm.ApplySuggestion(entry);

        Assert.Equal("", vm.SharedTemplateMessage);
    }

    [Fact]
    public void SharedTemplateSentence_uses_the_singular_for_exactly_one_other_entry()
    {
        Assert.Equal(
            "This save folder is also used by 1 other known game — a backup of this entry will contain its saves too.",
            AddGameDialogViewModel.SharedTemplateSentence(1));
    }

    [Fact]
    public void SharedTemplateSentence_pluralises_for_more_than_one_other_entry()
    {
        Assert.Equal(
            "This save folder is also used by 13 other known games — a backup of this entry will contain their saves too.",
            AddGameDialogViewModel.SharedTemplateSentence(13));
    }

    [Fact]
    public void Editing_display_name_after_a_shared_pick_clears_the_message()
    {
        var shared = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);
        var entry = KnownGamesCatalog.All.First(g => shared.Contains(g.SaveDirTemplate));

        var vm = CreateViewModel();
        vm.ApplySuggestion(entry);
        Assert.NotEqual("", vm.SharedTemplateMessage);

        vm.DisplayName = "Something else entirely";

        Assert.Equal("", vm.SharedTemplateMessage);
    }

    [Fact]
    public void Editing_save_folder_after_a_shared_pick_clears_the_message()
    {
        var shared = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);
        var entry = KnownGamesCatalog.All.First(g => shared.Contains(g.SaveDirTemplate));

        var vm = CreateViewModel();
        vm.ApplySuggestion(entry);
        Assert.NotEqual("", vm.SharedTemplateMessage);

        vm.SaveFolder = @"C:\somewhere-else";

        Assert.Equal("", vm.SharedTemplateMessage);
    }

    [Fact]
    public void Picking_the_same_shared_suggestion_twice_in_a_row_leaves_the_message_set()
    {
        var shared = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);
        var entry = KnownGamesCatalog.All.First(g => shared.Contains(g.SaveDirTemplate));

        var vm = CreateViewModel();
        vm.ApplySuggestion(entry);
        vm.ApplySuggestion(entry);

        Assert.NotEqual("", vm.SharedTemplateMessage);
    }

    [Fact]
    public void Fresh_view_model_has_an_empty_shared_template_message()
    {
        var vm = CreateViewModel();

        Assert.Equal("", vm.SharedTemplateMessage);
    }

    [Fact]
    public void A_name_that_slugifies_to_nothing_shows_the_unusable_name_message_and_disables_add()
    {
        var vm = CreateViewModel();
        vm.SaveFolder = @"C:\saves";

        vm.DisplayName = "😀😀😀";

        Assert.Equal(
            "SavegameBunker couldn't turn that name into a save-folder name — add a letter or number to it.",
            vm.ValidationMessage);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void A_name_whose_derived_id_collides_shows_the_similar_name_message_and_disables_add()
    {
        var vm = CreateViewModel("witcher3");
        vm.SaveFolder = @"C:\saves";

        vm.DisplayName = "Witcher3";

        Assert.Equal(
            "A game with a very similar name is already in your list. Try a slightly different name.",
            vm.ValidationMessage);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void Collision_check_ignores_case_matching_the_loaders_duplicate_id_check()
    {
        var vm = CreateViewModel("WITCHER3");
        vm.SaveFolder = @"C:\saves";

        vm.DisplayName = "witcher3";

        Assert.Equal(
            "A game with a very similar name is already in your list. Try a slightly different name.",
            vm.ValidationMessage);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void Non_colliding_name_clears_the_message_and_enables_add_once_every_field_is_filled()
    {
        var vm = CreateViewModel("witcher3");
        vm.SaveFolder = @"C:\saves";

        vm.DisplayName = "Witcher 3 Wild Hunt";

        Assert.Equal("", vm.ValidationMessage);
        Assert.True(vm.CanAdd);
    }

    [Fact]
    public void Blank_display_name_leaves_the_message_empty_rather_than_showing_an_error()
    {
        var vm = CreateViewModel();

        Assert.Equal("", vm.ValidationMessage);
    }

    [Fact]
    public async Task Resolving_an_existing_folder_reports_file_count_and_size()
    {
        var folder = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(folder);
        for (var i = 0; i < 41; i++)
            File.WriteAllBytes(Path.Combine(folder, $"file{i}.dat"), []);
        File.WriteAllBytes(Path.Combine(folder, "big.dat"), new byte[3_250_586]);

        var vm = CreateViewModel();
        vm.SaveFolder = folder;

        await WaitForAsync(() => vm.ResolvedStatus == "42 files, 3.1 MB");
    }

    [Fact]
    public async Task Resolving_a_missing_folder_reports_it_does_not_exist_yet_and_add_stays_available()
    {
        var vm = CreateViewModel();
        vm.DisplayName = "Anything";
        vm.SaveFolder = Path.Combine(_dir, "does-not-exist-yet");

        await WaitForAsync(() => vm.ResolvedStatus == "Folder does not exist yet");

        Assert.True(vm.CanAdd);
    }

    [Fact]
    public async Task Resolving_a_template_the_resolver_rejects_reports_its_error_and_add_stays_available()
    {
        var vm = CreateViewModel();
        vm.DisplayName = "Anything";
        vm.SaveFolder = @"relative\path"; // not rooted, no base directory — PathResolver rejects it

        await WaitForAsync(() => vm.ResolvedStatus.Contains("not an absolute path"));

        Assert.True(vm.CanAdd);
    }

    [Fact]
    public void Resolve_shows_checking_while_in_flight()
    {
        var vm = CreateViewModel();

        vm.SaveFolder = Path.Combine(_dir, "saves");

        Assert.Equal("Checking…", vm.ResolvedStatus);
    }

    [Fact]
    public async Task A_stale_resolve_does_not_overwrite_a_newer_result()
    {
        var slowFolder = Path.Combine(_dir, "slow");
        Directory.CreateDirectory(slowFolder);
        for (var i = 0; i < 200; i++)
            File.WriteAllBytes(Path.Combine(slowFolder, $"file{i}.dat"), new byte[1024]);

        var missingFolder = Path.Combine(_dir, "does-not-exist-yet");

        var vm = CreateViewModel();
        vm.SaveFolder = slowFolder; // starts a real, slower directory walk
        vm.SaveFolder = missingFolder; // immediately superseded before the walk can finish

        await WaitForAsync(() => vm.ResolvedStatus == "Folder does not exist yet");

        // Give the superseded resolve time to finish in the background and confirm it never
        // clobbers the newer result once it completes.
        await Task.Delay(NegativeProofDelayMs);
        Assert.Equal("Folder does not exist yet", vm.ResolvedStatus);
    }

    [Fact]
    public void Browse_puts_the_picked_folder_in_as_a_template()
    {
        var dialog = new ThrowingDialogService { FolderToReturn = Path.Combine(_dir, "picked") };
        var vm = new AddGameDialogViewModel([], dialog);

        vm.BrowseCommand.Execute(null);

        Assert.Equal(PathResolver.ToTemplate(Path.Combine(_dir, "picked")), vm.SaveFolder);
    }
}
