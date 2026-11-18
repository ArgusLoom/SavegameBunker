using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Backs the Add Game dialog: id derivation, inline validation, collision refusal, the
/// resolve-and-report line and the confirmed result. It references no WPF or picker type — only
/// <see cref="IDialogService"/> — so it is constructible and testable without a running window.
/// </summary>
/// <remarks>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// <para>
/// <see cref="Id"/> stays a hand-written, get-only property, not a partial-property
/// <c>[ObservableProperty]</c> — <see cref="SetDerivedId"/> needs to write the same backing
/// storage and raise <see cref="Id"/>'s change notification, and the compiler-synthesized backing
/// field a partial property generates has no name any other member can reach. There is no public
/// setter: the id is a database key, never a field the player edits, and
/// deriving it is the only way into <see cref="_id"/> from outside this class.
/// </para>
/// </remarks>
public sealed partial class AddGameDialogViewModel : CommunityToolkitObservableObject
{
    private static readonly string FolderMissingStatus = Strings.Get("str_common_folderNotFound");

    /// <summary>
    /// Every <c>saveDirTemplate</c> held by more than one entry in <see cref="KnownGamesCatalog.All"/>,
    /// computed once at type initialisation rather than per pick: <see cref="KnownGamesCatalog.All"/>
    /// is itself a <c>static readonly</c> list loaded once from an embedded resource and cannot change
    /// while the process runs, so recomputing it on every suggestion would regroup the whole table
    /// again for a guaranteed-identical answer. Calls <see cref="KnownGamesScanner.SharedTemplates"/> rather
    /// than grouping the table here — a second copy of the rule would be free to drift from the one
    /// the scan enforces, and the dialog and the scan must agree about which folders are shared.
    /// </summary>
    private static readonly IReadOnlySet<string> SharedTemplateSet = KnownGamesScanner.SharedTemplates(KnownGamesCatalog.All);

    private readonly IReadOnlyCollection<string> _existingIds;
    private readonly IDialogService _dialog;

    private string _id = "";
    private bool _applyingSuggestion;

    /// <summary>The save folder the last applied suggestion filled in, or null when none has —
    /// what lets <see cref="TryApplyExactKnownGame"/> tell its own earlier fill from the user's.</summary>
    private string? _suggestedSaveFolder;
    private int _resolveToken;

    public AddGameDialogViewModel(IReadOnlyCollection<string> existingIds, IDialogService dialog)
    {
        _existingIds = existingIds;
        _dialog = dialog;
    }

    /// <summary>
    /// Matches from the bundled known-games table for the current <see cref="DisplayName"/>,
    /// refilled on every change to it. A plain collection — the popup's open state is driven from
    /// <see cref="HasSuggestions"/> in XAML, so this view model still names no WPF type.
    /// </summary>
    public ObservableCollection<KnownGame> Suggestions { get; } = [];

    /// <summary>Whether the autocomplete popup should be open.</summary>
    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>
    /// The suggestion currently highlighted in the popup, navigated by
    /// <see cref="SelectNextSuggestion"/>/<see cref="SelectPreviousSuggestion"/> and applied by
    /// <see cref="CommitSuggestion"/>. The one property in this view model with a public
    /// setter, because the suggestions list's two-way binding writes it directly on a mouse click.
    /// </summary>
    [ObservableProperty]
    public partial KnownGame? SelectedSuggestion { get; set; }

    /// <summary>The ids already in the catalog. Read by 02-03's collision check.</summary>
    public IReadOnlyCollection<string> ExistingIds => _existingIds;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenWikiSearchCommand))]
    public partial string DisplayName { get; set; } = "";

    partial void OnDisplayNameChanged(string value)
    {
        // The shared-template sentence describes a chosen table entry and stops being
        // true the moment the fields stop matching it. This hook only runs when the value
        // actually changed — ApplySuggestion assigning the same suggestion's fields a second
        // time is a no-op set and leaves a still-true message alone, matching a
        // SetProperty-guarded property.
        SharedTemplateMessage = "";
        SetDerivedId(IdSlug.Derive(value));
        RefreshSuggestions();
        Revalidate();
        TryApplyExactKnownGame(whileTyping: true);
    }

    /// <summary>
    /// Derived, never user-edited — re-derives from <see cref="DisplayName"/>
    /// on every change to it, except right after <see cref="ApplySuggestion"/> overwrites it with
    /// the picked entry's own stored id: a later name edit still re-derives from there, since
    /// nothing here remembers that the current value came from a pick rather than a derivation.
    /// </summary>
    public string Id => _id;

    [ObservableProperty]
    public partial string SaveFolder { get; set; } = "";

    partial void OnSaveFolderChanged(string value)
    {
        // See DisplayName's setter: the same clear-on-no-op-guarded reasoning applies here —
        // this hook only runs when the value actually changed.
        SharedTemplateMessage = "";

        Revalidate();
        StartResolve(value);
    }

    /// <summary>
    /// Always present in the view, even when empty (an empty string, never a null/collapsed
    /// state), so the button row beneath it never moves when a message appears.
    /// </summary>
    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = "";

    /// <summary>
    /// What the entered <see cref="SaveFolder"/> template resolves to — a file count and
    /// size, "Checking…" while the walk is in flight, or why it could not be resolved. Never gates
    /// <see cref="CanAdd"/>: a game may be installed later and the entry is still correct.
    /// </summary>
    [ObservableProperty]
    public partial string ResolvedStatus { get; private set; } = "";

    /// <summary>
    /// States, in one sentence, that a picked suggestion's <see cref="SaveFolder"/> is also used
    /// by other known games — it never blocks <see cref="CanAdd"/>, since the folder does
    /// hold this game's save too. Always present, even when empty (an empty string, never a
    /// null/collapsed state), so the row beneath it never moves when the sentence appears — the
    /// same rule <see cref="ValidationMessage"/> already follows.
    /// </summary>
    [ObservableProperty]
    public partial string SharedTemplateMessage { get; private set; } = "";

    [ObservableProperty]
    public partial bool CanAdd { get; private set; }

    /// <summary>
    /// Opens a PCGamingWiki search for the typed <see cref="DisplayName"/>.
    /// Disabled while the name is blank. Goes through
    /// <see cref="IDialogService.OpenHttps"/> rather than <see cref="ShellOpen"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenWikiSearch))]
    private void OpenWikiSearch()
    {
        if (TryApplyExactKnownGame())
            return;
        _dialog.OpenHttps(WikiUrl.Search(DisplayName));
    }

    /// <summary>
    /// Applies the unique exact display-name or alias hit, if any. Used both while typing and
    /// from the wiki button so a 100% table hit fills name + save folder instead of opening a
    /// browser. Returns false when nothing exact matched so the caller can fall through to wiki.
    /// </summary>
    /// <param name="whileTyping">
    /// True on the keystroke path, which nobody asked to fill anything: there it stands aside when
    /// the save folder already holds something the user supplied themselves — a dropped folder, a
    /// browsed one, a typed one — because a name that merely passes through a table entry on its
    /// way to a longer one ("Doom" towards "Doom Eternal") must not replace it. A folder this
    /// view model filled from an earlier suggestion is not the user's own and is still replaced.
    /// The wiki button and a picked suggestion are explicit requests and always apply.
    /// </param>
    private bool TryApplyExactKnownGame(bool whileTyping = false)
    {
        if (_applyingSuggestion)
            return false;
        if (whileTyping && SaveFolder.Length > 0 && SaveFolder != _suggestedSaveFolder)
            return false;
        if (!KnownGamesCatalog.TryExactMatch(DisplayName, out var game) || game is null)
            return false;
        ApplySuggestion(game);
        return true;
    }

    private bool CanOpenWikiSearch() => !string.IsNullOrWhiteSpace(DisplayName);

    [RelayCommand]
    private Task BrowseAsync()
    {
        var picked = _dialog.PickFolder(Strings.Get("str_common_browseFolderTitle"), null);
        if (picked is not null)
            SaveFolder = PathResolver.ToTemplate(picked);

        return Task.CompletedTask;
    }

    /// <summary>Writes <see cref="_id"/> directly and raises <see cref="Id"/>'s change notification.</summary>
    private void SetDerivedId(string value) =>
        SetProperty(ref _id, value, nameof(Id));

    /// <summary>
    /// Refills <see cref="Suggestions"/> from <see cref="KnownGamesCatalog.Find"/>. Suppressed
    /// while <see cref="ApplySuggestion"/> is setting <see cref="DisplayName"/> itself, so picking
    /// a match closes the popup instead of immediately reopening it against its own text.
    /// </summary>
    private void RefreshSuggestions()
    {
        Suggestions.Clear();
        if (!_applyingSuggestion)
        {
            foreach (var match in KnownGamesCatalog.Find(DisplayName, 8))
                Suggestions.Add(match);
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>
    /// Fills the display name, the table's stored id (unless the user already edited it) and the
    /// save folder from a picked suggestion. Setting <see cref="SaveFolder"/> starts
    /// the same resolve-and-report the Browse path starts, so a stale table entry shows up
    /// immediately. Every filled field stays editable afterward.
    /// </summary>
    public void ApplySuggestion(KnownGame game)
    {
        _applyingSuggestion = true;
        try
        {
            DisplayName = game.DisplayName;
        }
        finally
        {
            _applyingSuggestion = false;
        }

        // DisplayName's setter already cleared Suggestions unless the typed text was already an
        // exact match (a no-op set raises no change) — dismiss unconditionally so the popup always
        // closes on commit.
        DismissSuggestions();
        _suggestedSaveFolder = game.SaveDirTemplate;
        SaveFolder = game.SaveDirTemplate;

        // A picked suggestion contributes its own stored id verbatim, not one freshly
        // derived from its display name, so the scan and the dialog can never file the same game
        // under two different ids — until the player edits the name again, at which point
        // OnDisplayNameChanged's unconditional derivation takes back over (there is no longer a
        // way to mark this value as "don't re-derive me", since the id has no public setter to mark
        // it with). A blank stored id (a table entry not yet backfilled) falls back to
        // IdSlug.Derive rather than leaving the field blank, since IdSlug.IsSafe("") is false and
        // would disable Add with no visible cause.
        SetDerivedId(game.Id.Length > 0 ? game.Id : IdSlug.Derive(game.DisplayName));

        // SetDerivedId only writes the field and raises change notification; DisplayName's
        // setter already called Revalidate() before this id was overwritten, so it must run
        // again here. This is the only gate between a malformed or colliding table id and
        // games.json through this door.
        Revalidate();

        // The dialog states the fact in one sentence and does not block — the folder does
        // hold this game's save too, unlike the scan, which refuses a shared template outright.
        // DisplayName and SaveFolder are both already set above, each clearing this
        // message through its own setter, so this assignment is what makes it stick.
        if (SharedTemplateSet.Contains(game.SaveDirTemplate))
        {
            // ReferenceEquals excludes the entry itself so the count means "other entries" — every
            // real suggestion came out of KnownGamesCatalog.All via KnownGamesCatalog.Find, and a
            // synthetic entry built by reference in a test is excluded the same way.
            var otherCount = KnownGamesCatalog.All.Count(g =>
                !ReferenceEquals(g, game)
                && string.Equals(g.SaveDirTemplate, game.SaveDirTemplate, StringComparison.OrdinalIgnoreCase));
            SharedTemplateMessage = SharedTemplateSentence(otherCount);
        }
        else
        {
            SharedTemplateMessage = "";
        }
    }

    /// <summary>
    /// The singular/plural wording for <see cref="SharedTemplateMessage"/>, kept as a separate pure
    /// function (rather than inline at the assignment site) so both branches can be pinned
    /// deterministically: the shipped table carries exactly one collision group, and both the
    /// singular and plural wording need pinning regardless of how many entries that group happens
    /// to hold at any given time. Wording lives in <c>Strings.en.xaml</c> like every other authored
    /// sentence. <c>internal</c> is the project's existing test seam
    /// (<see cref="KnownGamesCatalog.LoadFrom"/> is <c>internal</c> for the same reason) —
    /// <c>SavegameBunker.csproj</c> already carries <c>InternalsVisibleTo</c> for the test project. A
    /// non-positive count returns "" rather than throwing, keeping the function total even though
    /// <see cref="ApplySuggestion"/> never calls it with one.
    /// </summary>
    internal static string SharedTemplateSentence(int otherCount) => otherCount switch
    {
        <= 0 => "",
        1 => Strings.Get("str_addGame_sharedTemplateOne"),
        _ => Strings.Format("str_addGame_sharedTemplateMany", otherCount),
    };

    /// <summary>Closes the popup without changing anything else — the view's Escape command.</summary>
    [RelayCommand(CanExecute = nameof(HasSuggestions))]
    public void DismissSuggestions()
    {
        if (Suggestions.Count == 0)
            return;

        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>
    /// Applies the highlighted suggestion — the view's Enter command when the popup is
    /// open. Does nothing when nothing is highlighted, so a stray Enter with the popup already
    /// closing mid-selection cannot apply a suggestion that no longer exists.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCommitSuggestion))]
    private void CommitSuggestion()
    {
        if (SelectedSuggestion is { } suggestion)
            ApplySuggestion(suggestion);
    }

    private bool CanCommitSuggestion() => HasSuggestions && SelectedSuggestion is not null;

    /// <summary>
    /// Moves the highlight to the next suggestion — the view's Down-arrow command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSuggestions))]
    private void SelectNextSuggestion() => MoveHighlight(+1);

    /// <summary>
    /// Moves the highlight to the previous suggestion — the view's Up-arrow command.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSuggestions))]
    private void SelectPreviousSuggestion() => MoveHighlight(-1);

    /// <summary>
    /// Shared arithmetic for both navigation commands, matching the pre-move
    /// <c>ListBox.SelectedIndex</c> clamp exactly: from no highlight, both directions land on the
    /// first entry. Returns immediately on an empty collection rather than assuming the
    /// can-execute gate ran, since clamping against inverted bounds would throw.
    /// </summary>
    private void MoveHighlight(int delta)
    {
        if (Suggestions.Count == 0)
            return;

        var currentIndex = SelectedSuggestion is null ? -1 : Suggestions.IndexOf(SelectedSuggestion);
        var nextIndex = Math.Clamp(currentIndex + delta, 0, Suggestions.Count - 1);
        SelectedSuggestion = Suggestions[nextIndex];
    }

    /// <summary>
    /// Runs on every change to <see cref="DisplayName"/> or <see cref="SaveFolder"/>.
    /// A blank <see cref="DisplayName"/> leaves the message empty — the field is simply not filled
    /// in yet, not an error. Both messages are now stated about the name, not the id — the id is no
    /// longer visible. <see cref="CanAdd"/> is true only when all three
    /// fields are non-blank and the derived id is both well-formed and free.
    /// </summary>
    private void Revalidate()
    {
        ValidationMessage = DisplayName.Length == 0
            ? ""
            : Id.Length == 0 || !IdSlug.IsSafe(Id)
                ? Strings.Get("str_addGame_unusableName")
                : _existingIds.Any(existing => string.Equals(existing, Id, StringComparison.OrdinalIgnoreCase))
                    ? Strings.Get("str_addGame_similarName")
                    : "";

        CanAdd = !string.IsNullOrWhiteSpace(DisplayName)
            && !string.IsNullOrWhiteSpace(Id)
            && !string.IsNullOrWhiteSpace(SaveFolder)
            && ValidationMessage.Length == 0;
    }

    /// <summary>
    /// Kicks off the resolve-and-report walk for <paramref name="folder"/> off the UI thread. A
    /// monotonically increasing token guards against a stale resolve (started for a folder the
    /// user has since changed) overwriting a newer one's result.
    /// </summary>
    private void StartResolve(string folder)
    {
        var token = ++_resolveToken;
        ResolvedStatus = Strings.Get("str_common_checking");

        _ = ReportResolveAsync(folder, token);
    }

    private async Task ReportResolveAsync(string folder, int token)
    {
        var result = await Task.Run(() => ResolveFolder(folder));

        if (token == _resolveToken)
            ResolvedStatus = result;
    }

    private static string ResolveFolder(string folder)
    {
        if (!PathResolver.TryResolve(folder, null, out var dir, out var error))
            return error!;

        if (!Directory.Exists(dir))
            return FolderMissingStatus;

        var (count, bytes) = DirectoryWalk.CountFiles(dir);
        return count == 1
            ? Strings.Format("str_common_fileCountOne", SizeFormat.Describe(bytes))
            : Strings.Format("str_common_fileCount", count, SizeFormat.Describe(bytes));
    }

    public AddGameResult BuildResult() => new(Id, DisplayName, SaveFolder);
}
