using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Backs the Find Games window: the checked/unchecked state of every hit, the confirmed selection,
/// the two skipped-entry counts and the shared-template titles <c>[Details]</c> expands. Unlike
/// <see cref="AddGameDialogViewModel"/> it names no <see cref="IDialogService"/> — this window shows
/// no nested dialog of its own, so taking one would be a dependency it never calls.
/// </summary>
/// <remarks>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// </remarks>
public sealed partial class ScanWindowViewModel : CommunityToolkitObservableObject
{
    private readonly int _sharedSkippedCount;

    public ScanWindowViewModel(ScanResult result)
    {
        foreach (var hit in result.Hits)
        {
            var row = new ScanHitRowViewModel(hit);
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ScanHitRowViewModel.IsSelected))
                    return;

                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(AddLabel));
                OnPropertyChanged(nameof(CanAdd));
            };
            Rows.Add(row);
        }

        HeaderText = result.Hits.Count switch
        {
            // 0 of the table's templates resolve on the development machine, so the empty case is the
            // *only* case anyone here will see — a bare empty list would be indistinguishable
            // from a window that failed to load.
            0 => Strings.Get("str_scan_headerZero"),
            1 => Strings.Get("str_scan_headerOne"),
            var n => Strings.Format("str_scan_headerMany", n),
        };

        _sharedSkippedCount = result.SkippedShared.Count;
        SkippedAlreadyKnown = result.SkippedAlreadyKnown;
        SharedTitles = result.SkippedShared.Select(g => g.DisplayName).ToList();
    }

    public ObservableCollection<ScanHitRowViewModel> Rows { get; } = new();

    public string HeaderText { get; }

    /// <summary>
    /// Whether the scan produced any hits. Computed from <see cref="Rows"/>, which is filled once
    /// in the constructor and never changes afterward — a scan that found nothing renders
    /// <see cref="HeaderText"/>'s sentence instead of an empty row list.
    /// </summary>
    public bool HasHits => Rows.Count > 0;

    public int SelectedCount => Rows.Count(row => row.IsSelected);

    public string AddLabel => Strings.Format("str_scan_addLabel", SelectedCount);

    public bool CanAdd => SelectedCount > 0;

    /// <summary>Whether any table entries were excluded for sharing a <c>saveDirTemplate</c>.</summary>
    public bool HasSharedSkipped => _sharedSkippedCount > 0;

    /// <summary>
    /// Always present, even when empty (an empty string, never a collapsed state) — matching
    /// <see cref="AddGameDialogViewModel.ValidationMessage"/>'s rule, so the button row beneath it
    /// never moves as the count changes.
    /// </summary>
    public string SharedSkippedText => _sharedSkippedCount switch
    {
        0 => "",
        1 => Strings.Get("str_scan_sharedSkippedOne"),
        var n => Strings.Format("str_scan_sharedSkippedMany", n),
    };

    /// <summary>
    /// How many entries were excluded because their id or resolved path was already in the catalog.
    /// A plain count: <see cref="ScanResult.SkippedAlreadyKnown"/> carries no backing list, so
    /// there is nothing for a <c>[Details]</c> expansion to show for this line.
    /// </summary>
    public int SkippedAlreadyKnown { get; }

    public bool HasAlreadyKnownSkipped => SkippedAlreadyKnown > 0;

    public string AlreadyKnownSkippedText =>
        SkippedAlreadyKnown == 0 ? "" : Strings.Format("str_scan_alreadyKnownSkipped", SkippedAlreadyKnown);

    /// <summary>
    /// Display names of <see cref="ScanResult.SkippedShared"/>, in the order the scanner returned
    /// them — the shared line's only expansion, since <see cref="SkippedAlreadyKnown"/> is a plain
    /// int with no backing list to show.
    /// </summary>
    public IReadOnlyList<string> SharedTitles { get; }

    /// <summary>Whether the shared-titles expansion is open. Starts collapsed.</summary>
    [ObservableProperty]
    public partial bool ShowDetails { get; private set; }

    // Plain [RelayCommand] (source-generates a synchronous RelayCommand, not an async one): all
    // three mutate in-memory bools and touch no disk, so the async command type — which exists for
    // off-UI-thread filesystem work per CLAUDE.md's ZipFile.CreateFromDirectory reasoning — would
    // be a false signal here.
    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void SelectAll() => SetAllSelected(true);

    [RelayCommand]
    private void SelectNone() => SetAllSelected(false);

    private void SetAllSelected(bool selected)
    {
        foreach (var row in Rows)
            row.IsSelected = selected;
    }

    /// <summary>The checked rows' games, in list order — computed on confirm, not tracked live.</summary>
    public IReadOnlyList<KnownGame> BuildSelection() =>
        Rows.Where(row => row.IsSelected).Select(row => row.Hit.Game).ToList();
}
