using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Root view model and composition root: owns the bound state and the commands, and delegates
/// the catalog record to <see cref="CatalogSession"/>, per-game service calls to
/// <see cref="GameActions"/>, the bulk runs to <see cref="BulkBackupCoordinator"/>, the status
/// sweep and the free-space advisory's I/O to <see cref="StatusSweepCoordinator"/>, and the
/// per-row Back up / Restore / Open save folder flows to <see cref="RowActionsCoordinator"/>
/// through <see cref="IRowActionHost"/>.
/// </summary>
/// <remarks>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// </remarks>
public sealed partial class MainViewModel : CommunityToolkitObservableObject, IRowActionHost
{
    private readonly IDialogService _dialog;
    private readonly BackupService _backup = new();
    private readonly GameActions _gameActions;
    private readonly BulkBackupCoordinator _bulk;
    private readonly StatusSweepCoordinator _sweep;
    private readonly RowActionsCoordinator _rowActions;
    private readonly CatalogSession _session;
    private readonly KnownGamesScanner _knownGamesScanner = new();



    /// <summary>
    /// The durable store for per-row selection — keyed by <see cref="Game.Id"/>,
    /// not by row-object reference, because <see cref="RefreshAsync"/> and
    /// <see cref="ChangeSaveFolderAsync"/> both construct brand-new <see cref="GameRowViewModel"/>
    /// instances. <see cref="Games"/>/each row's own <see cref="GameRowViewModel.IsSelected"/> is
    /// the display, always seeded from and written back to this set, never the other way around.
    /// </summary>
    private readonly HashSet<string> _selectedIds = [];

    /// <summary>
    /// Passed to <see cref="System.Windows.Data.BindingOperations.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>
    /// so <see cref="GamesView"/> tolerates <see cref="Games"/> changing on another thread. In the
    /// running app every change already lands on the UI thread, but an xUnit host has no
    /// <c>SynchronizationContext</c>, so a command's post-<c>await</c> mutation can arrive on a pool
    /// thread — which an unsynchronized <c>CollectionView</c> answers by crashing the process.
    /// </summary>
    private readonly object _gamesSyncLock = new();

    /// <summary>
    /// Debounces <see cref="SearchText"/> keystrokes ~150ms before re-filtering <see cref="GamesView"/>.
    /// Never fires in a plain xUnit host — no <see cref="System.Windows.Threading.Dispatcher"/>
    /// message loop runs there — so the debounce timing itself is a manual-only check;
    /// unit tests drive <see cref="MatchesSearch"/>/<see cref="GamesView"/>'s
    /// <c>Filter</c>/<c>Refresh()</c> directly and synchronously instead.
    /// </summary>
    private readonly System.Windows.Threading.DispatcherTimer _filterDebounce = new()
    {
        Interval = TimeSpan.FromMilliseconds(150),
    };

    // Bumped at the start of every RecomputeSpaceAdvisoryAsync call and captured by that call's
    // own closure — the same capture-and-compare shape StartStatusScan's per-row token uses, so an
    // in-flight recompute for an older selection/changed-set cannot overwrite a newer one's
    // result.
    private int _spaceAdvisoryGeneration;

    /// <summary>
    /// Armed once by <c>App.OnStartup</c> (via <see cref="RequestStartupUnsecuredPrompt"/>) so the
    /// startup prompt can only ever be offered around application start, never by an ordinary
    /// mid-session Refresh's sweep completion. Arming evaluates the prompt at once when the first
    /// full sweep has already settled (see <see cref="_firstFullSweepSettled"/>) and no row is on
    /// Checking; otherwise the moment both hold decides it. Either way at most once per session.
    /// </summary>
    private bool _startupPromptPending;

    /// <summary>Set the moment the startup prompt has been evaluated, whatever it decided.</summary>
    private bool _startupPromptShown;

    /// <summary>
    /// Guards the arm/settle/prompt trio. In the app all three run on the UI thread, but the view
    /// model must stay thread-agnostic: in a host without a dispatcher <see cref="OnUi"/> runs
    /// inline on pool threads, and without this lock the arming and settling pair could lose the
    /// prompt or show it twice.
    /// </summary>
    private readonly object _startupPromptLock = new();

    /// <summary>
    /// Set once the first full sweep has laid a verdict on every row — or found no rows at all —
    /// and never cleared afterwards.
    /// </summary>
    private bool _firstFullSweepSettled;

    /// <summary>
    /// Debounces a burst of selection/sweep-completion events before recomputing the free-space
    /// advisory — the identical <see cref="System.Windows.Threading.DispatcherTimer"/>
    /// shape <see cref="_filterDebounce"/> uses, including its own caveat: this never fires in a
    /// plain xUnit host, so the debounce timing itself is a manual-only check; unit tests
    /// drive <see cref="RecomputeSpaceAdvisoryAsync"/> directly and synchronously instead.
    /// </summary>
    private readonly System.Windows.Threading.DispatcherTimer _spaceAdvisoryDebounce = new()
    {
        Interval = TimeSpan.FromMilliseconds(150),
    };

    private readonly Func<string?> _suggestBackupStartFolder;

    /// <summary>
    /// Production construction: the start-folder probe is the real drive enumeration
    /// (<see cref="DriveHints.SuggestBackupStartFolder"/>).
    /// </summary>
    public MainViewModel(CatalogLoad load, IDialogService dialog)
        : this(load, dialog, DriveHints.SuggestBackupStartFolder)
    {
    }

    /// <summary>
    /// The start-folder probe is the picker path's only touch of the machine's real drives;
    /// it is injected so tests can pass a fixed folder instead of walking actual volumes.
    /// Production always uses <see cref="DriveHints.SuggestBackupStartFolder"/> via the public
    /// constructor.
    /// </summary>
    internal MainViewModel(CatalogLoad load, IDialogService dialog, Func<string?> suggestBackupStartFolder)
    {
        _suggestBackupStartFolder = suggestBackupStartFolder;
        _session = new CatalogSession(load);
        _dialog = dialog;
        // In dependency order: GameActions first, because the bulk and row coordinators take it.
        // All four must exist before the constructor's closing StartStatusScan().
        _gameActions = new GameActions(_backup, _dialog);
        _bulk = new BulkBackupCoordinator(_backup, _gameActions, OnUi);
        _sweep = new StatusSweepCoordinator(_backup, OnUi);
        _rowActions = new RowActionsCoordinator(_backup, _gameActions, _dialog, this);
        StatusMessage = Strings.Get("str_common_ready");

        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Games, _gamesSyncLock);

        // Created BEFORE the CollectionChanged subscription below: GetDefaultView() subscribes its
        // own sync handler, and handlers fire in subscription order. Subscribed the other way round,
        // HasNoVisibleGames would be read off a view not yet re-synced — "No games match" stuck on
        // after adding the first game to an empty catalog.
        GamesView = System.Windows.Data.CollectionViewSource.GetDefaultView(Games);
        GamesView.Filter = MatchesSearch;

        Games.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoGames));
            OnPropertyChanged(nameof(HasBackupTargets));
            OnPropertyChanged(nameof(IsAnyRowChecking));
            OnPropertyChanged(nameof(HasNoVisibleGames));
            OnPropertyChanged(nameof(HasAnySelected));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(BackupSelectedLabel));
            OnPropertyChanged(nameof(RestoreSelectedLabel));
            BackupAllChangedCommand.NotifyCanExecuteChanged();
            BackupSelectedCommand.NotifyCanExecuteChanged();
            RestoreSelectedCommand.NotifyCanExecuteChanged();
        };

        foreach (var game in _session.Games)
            Games.Add(RegisterRow(game));

        _filterDebounce.Tick += (_, _) =>
        {
            _filterDebounce.Stop();
            GamesView.Refresh();
            OnPropertyChanged(nameof(HeaderCheckState));
            OnPropertyChanged(nameof(HasNoVisibleGames));
        };

        _spaceAdvisoryDebounce.Tick += (_, _) =>
        {
            _spaceAdvisoryDebounce.Stop();
            _ = RecomputeSpaceAdvisoryAsync();
        };

        // IsCatalogCommandRunning is part of IsAnyOperationRunning, so the gates reading it must
        // be re-queried when one of those commands starts or finishes.
        foreach (var command in new IAsyncRelayCommand[]
                 { RefreshCommand, ChangeBackupRootCommand, AddGameCommand, ChangeSaveFolderCommand, RemoveGameCommand })
        {
            command.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IAsyncRelayCommand.IsRunning))
                    RequeryOperationGates();
            };
        }

        StartStatusScan();
    }

    public ObservableCollection<GameRowViewModel> Games { get; } = [];

    /// <summary>
    /// The DataGrid's actual <c>ItemsSource</c> — a filtered view over <see cref="Games"/>
    /// that never mutates the source collection itself. Every safety gate
    /// (<see cref="HasBackupTargets"/>, <see cref="IsAnyRowChecking"/>, <see cref="IsAnyRowBusy"/>,
    /// <see cref="CanRefreshOrChangeBackupRoot"/>) keeps reading <see cref="Games"/> directly —
    /// <see cref="HeaderCheckState"/>/<see cref="ToggleHeaderSelectionCommand"/> are the one
    /// deliberate exception that reads this property instead.
    /// </summary>
    public System.ComponentModel.ICollectionView GamesView { get; }

    /// <summary>
    /// The one list of commands whose <c>CanExecute</c> reads an operation flag
    /// (<see cref="BulkBackupTotal"/>, <see cref="IsBulkPreparing"/>, <see cref="IsBackupRunning"/>,
    /// <see cref="IsRestoreRunning"/>) or a row's busy state — kept in one place because the four
    /// attribute lists it replaces had already drifted apart (7, 5, 5 and 1 entries, missing
    /// <see cref="BackupCommand"/>/<see cref="RestoreCommand"/> from the row-IsBusy branch
    /// entirely). <see cref="IsScanRunning"/>'s own <see cref="FindGamesCommand"/> notification and
    /// the narrower <see cref="GameRowViewModel.ComparisonKind"/>/<see cref="Games"/>'s
    /// <c>CollectionChanged</c>/<see cref="GameRowViewModel.IsSelected"/> subsets stay separate —
    /// they deliberately notify fewer commands.
    /// </summary>
    private void RequeryOperationGates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ChangeBackupRootCommand.NotifyCanExecuteChanged();
        BackupAllChangedCommand.NotifyCanExecuteChanged();
        BackupSelectedCommand.NotifyCanExecuteChanged();
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        BackupCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        AddGameCommand.NotifyCanExecuteChanged();
        FindGamesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Constructs a row for <paramref name="game"/> — the single place every row is created (the
    /// constructor, <see cref="AddGameAsync"/>, <see cref="FindGamesAsync"/>,
    /// <see cref="RefreshAsync"/> and the repair route's replacement in
    /// <see cref="ChangeSaveFolderAsync"/>) — and subscribes to its comparison-kind and busy-state
    /// changes so <see cref="HasBackupTargets"/>/<see cref="IsAnyRowChecking"/>/
    /// <see cref="IsAnyRowBusy"/> and the toolbar commands' enablement update the moment a row
    /// settles mid-sweep or goes busy, with no second completion hook. Without this, a row's
    /// <c>IsBusy</c> going true during restore preflight changes what <see cref="IsAnyRowBusy"/>
    /// reads, but nothing tells the toolbar commands their <c>CanExecute</c> answer just changed —
    /// they would only catch up whenever WPF's next unrelated command requery happened to run, not
    /// the moment preflight actually starts.
    /// </summary>
    private GameRowViewModel RegisterRow(Game game)
    {
        var row = new GameRowViewModel(game)
        {
            IsSelected = _selectedIds.Contains(game.Id),
        };
        row.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(GameRowViewModel.ComparisonKind):
                    OnPropertyChanged(nameof(HasBackupTargets));
                    OnPropertyChanged(nameof(IsAnyRowChecking));
                    BackupAllChangedCommand.NotifyCanExecuteChanged();
                    BackupSelectedCommand.NotifyCanExecuteChanged();
                    RestoreSelectedCommand.NotifyCanExecuteChanged();
                    // A prompt armed while some sweep still had rows on Checking was deferred;
                    // the row that settles last decides it (see PromptUnsecuredGamesOnce).
                    if (row.ComparisonKind != BackupComparisonKind.Checking)
                        PromptUnsecuredGamesOnce();
                    break;
                case nameof(GameRowViewModel.IsBusy):
                    OnPropertyChanged(nameof(IsAnyRowBusy));
                    // Also requeries BackupCommand/RestoreCommand (an enablement-only change): CanBackupRow/CanRestoreRow read row.IsBusy too, so
                    // a row going busy must re-query its own Backup/Restore, not only the toolbar.
                    RequeryOperationGates();
                    break;
                case nameof(GameRowViewModel.IsSelected):
                    if (row.IsSelected)
                        _selectedIds.Add(row.Game.Id);
                    else
                        _selectedIds.Remove(row.Game.Id);
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(BackupSelectedLabel));
                    OnPropertyChanged(nameof(RestoreSelectedLabel));
                    OnPropertyChanged(nameof(HasAnySelected));
                    OnPropertyChanged(nameof(HeaderCheckState));
                    BackupSelectedCommand.NotifyCanExecuteChanged();
                    RestoreSelectedCommand.NotifyCanExecuteChanged();
                    ScheduleSpaceAdvisoryRecompute();
                    break;
            }
        };
        return row;
    }

    /// <summary>
    /// Constructs a row through <see cref="RegisterRow"/> and inserts it into <see cref="Games"/>
    /// at the first position whose existing display name compares greater than the new one.
    /// Leaves an already-unsorted list as it is and only places the new arrival
    /// correctly among it — a full resort of rows the user never asked to touch is not what
    /// sorted insert asks for, and load keeps the file's order alone. Scanning forward while the
    /// existing name compares less than or equal to the new one places a tie after its existing
    /// partner, matching the stable write ordering.
    /// </summary>
    private GameRowViewModel InsertSorted(Game game)
    {
        var row = RegisterRow(game);
        var i = 0;
        while (i < Games.Count
            && string.Compare(Games[i].DisplayName, game.DisplayName, StringComparison.OrdinalIgnoreCase) <= 0)
        {
            i++;
        }

        Games.Insert(i, row);
        return row;
    }

    /// <summary>
    /// Drives the bulk-backup toolbar button's <c>Visibility</c> — hidden, not
    /// disabled, when nothing qualifies, so an empty-handed click is impossible. The two-verdict
    /// rule itself lives in <see cref="BulkRunRules.NeedsBackup"/> so every bulk path shares it.
    /// </summary>
    public bool HasBackupTargets => Games.Any(row => BulkRunRules.NeedsBackup(row.ComparisonKind));

    /// <summary>
    /// Drives the bulk-backup toolbar button's enablement — a half-finished status sweep can never
    /// define the selection, so the action is disabled (not hidden) while any row's comparison is
    /// still <see cref="BackupComparisonKind.Checking"/>.
    /// </summary>
    /// <remarks>
    /// The reset to <see cref="BackupComparisonKind.Checking"/> itself lives where the writes
    /// happen: <see cref="StatusSweepCoordinator.Reset"/> for every sweep and
    /// <see cref="BulkBackupCoordinator"/>'s claim for every bulk run. This aggregate stays here —
    /// it reads the rows, it never writes them.
    /// </remarks>
    public bool IsAnyRowChecking => Games.Any(row => row.ComparisonKind == BackupComparisonKind.Checking);

    /// <summary>
    /// Whether any row is mid-flight on its own backup or restore — including a restore still in
    /// its preflight (precondition check, folder picker), before <see cref="IsRestoreRunning"/> is
    /// raised. <see cref="RowActionsCoordinator.RestoreAsync"/>'s doc comment explains why that flag
    /// stays unset until a restore plan is in hand; <c>row.IsBusy</c> is the one signal set for the whole window
    /// instead, so this is what closing the window and starting a bulk run must both gate on.
    /// </summary>
    public bool IsAnyRowBusy => Games.Any(row => row.IsBusy);

    /// <summary>
    /// Whether anything that writes to disk is in flight: a row's own action (preflight
    /// included), a single backup or restore, a bulk run, the bulk run's preparation (folder
    /// probe, picker, catalog write — <see cref="IsBulkPreparing"/>), or a command that reloads
    /// or rewrites the catalog (<see cref="IsCatalogCommandRunning"/>). The one gate for closing
    /// the window, refreshing, changing the backup folder and starting another bulk run.
    /// </summary>
    public bool IsAnyOperationRunning => IsAnyBackupOrRestoreRunning || IsCatalogCommandRunning;

    /// <summary>
    /// The backup/restore part of <see cref="IsAnyOperationRunning"/> — what closing the window
    /// gates on. Closing is refused only for these, whose interruption can leave a save folder
    /// half-swapped; a catalog command may merely be waiting on its confirmation, and the catalog
    /// write itself is a single file save.
    /// </summary>
    public bool IsAnyBackupOrRestoreRunning =>
        IsAnyRowBusy || IsBackupRunning || IsRestoreRunning || IsBulkBackupRunning || IsBulkPreparing;

    /// <summary>
    /// Refresh, Change backup folder, Add Game, Change save folder or Remove game still in flight.
    /// Each reloads or rewrites the catalog, or rebuilds or replaces rows, after an await; a backup
    /// started meanwhile could claim rows that are about to be dropped or replaced, or resolve the
    /// backup folder mid-change. Find Games is left out on purpose: its scan can run long and only
    /// adds rows, so Refresh and backups stay available while it runs.
    /// </summary>
    private bool IsCatalogCommandRunning =>
        RefreshCommand.IsRunning || ChangeBackupRootCommand.IsRunning || AddGameCommand.IsRunning
        || ChangeSaveFolderCommand.IsRunning || RemoveGameCommand.IsRunning;

    /// <summary>
    /// Something other than a catalog edit itself is reloading or writing the catalog: a backup
    /// choosing its first backup folder (bulk or single-row), Refresh, or Change backup folder. A
    /// catalog edit started then would race that write's changed-on-disk check, or be replaced by
    /// Refresh's rebuild. The edit commands' own bodies check this (not
    /// <see cref="IsCatalogCommandRunning"/>, which includes the command running the check).
    /// </summary>
    private bool IsCatalogWriteInFlight =>
        IsBulkPreparing || IsChoosingBackupRoot || RefreshCommand.IsRunning || ChangeBackupRootCommand.IsRunning;

    /// <summary>
    /// True while <see cref="EnsureBackupRootAsync"/> is asking for and saving a first backup
    /// folder — the catalog write a single-row Backup makes, which IsBulkPreparing does not cover.
    /// </summary>
    [ObservableProperty]
    public partial bool IsChoosingBackupRoot { get; private set; }

    partial void OnIsChoosingBackupRootChanged(bool value) => RequeryOperationGates();

    /// <summary>How many rows are currently checked — the durable count the bulk actions gate on.</summary>
    public int SelectedCount => Games.Count(row => row.IsSelected);

    /// <summary>
    /// The two bulk buttons' labels, carrying how many games are ticked. The count includes rows
    /// the search currently hides — the run does too — so the number on the button is the
    /// one place a selection the user can no longer see stays visible before they act on it.
    /// </summary>
    public string BackupSelectedLabel => $"{Strings.Get("str_toolbar_backUpSelected_access")} ({SelectedCount})";

    /// <inheritdoc cref="BackupSelectedLabel"/>
    public string RestoreSelectedLabel => $"{Strings.Get("str_toolbar_restoreSelected_access")} ({SelectedCount})";

    /// <summary>Whether any row is currently checked.</summary>
    public bool HasAnySelected => Games.Any(row => row.IsSelected);

    /// <summary>
    /// The header checkbox's tri-state value: <c>false</c> when the currently
    /// visible set is empty or none of it is selected, <c>true</c> when every visible row is
    /// selected, <c>null</c> (Indeterminate) for a partial selection. Deliberately reads
    /// <see cref="GamesView"/>, not <see cref="Games"/> — the one exception to this file's
    /// rule that every other aggregate reads the unfiltered source collection.
    /// </summary>
    public bool? HeaderCheckState
    {
        get
        {
            var visible = GamesView.Cast<GameRowViewModel>().ToList();
            if (visible.Count == 0)
                return false;
            var selectedCount = visible.Count(r => r.IsSelected);
            if (selectedCount == 0)
                return false;
            if (selectedCount == visible.Count)
                return true;
            return null;
        }
    }

    /// <summary>
    /// Selects every currently visible row when <see cref="HeaderCheckState"/> is Unchecked or
    /// Indeterminate, clears every currently visible row when it is Checked. Acts
    /// only on <see cref="GamesView"/>'s current items, never a row hidden by the active filter.
    /// </summary>
    [RelayCommand]
    private void ToggleHeaderSelection()
    {
        var visible = GamesView.Cast<GameRowViewModel>().ToList();
        var target = !(HeaderCheckState == true);
        foreach (var row in visible)
            row.IsSelected = target;
    }

    /// <summary>Games completed so far in a running bulk backup — 0 outside one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkBackupProgressText))]
    public partial int BulkBackupCurrent { get; private set; }

    /// <summary>
    /// The backup destination's free space, named against the backup folder
    /// — <c>null</c> whenever there is nothing to show: no backup root configured, or the root's
    /// free space could not be read (e.g. a UNC destination or an unmounted drive
    /// letter). Absence is never an error — the segment simply does not render. Recomputed only by
    /// <see cref="RecomputeSpaceAdvisoryAsync"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFreeSpaceAdvisory))]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial string? FreeSpaceAdvisory { get; private set; }

    /// <summary>Null-check companion to <see cref="FreeSpaceAdvisory"/> that the free-space tests read.</summary>
    public bool HasFreeSpaceAdvisory => FreeSpaceAdvisory is not null;

    /// <summary>
    /// What "Back up selected" would write right now, named against that action.
    /// <c>null</c> when nothing is selected — shown only while <c>BackupSelectedButton</c> itself is
    /// shown (<see cref="HasAnySelected"/>), so the two segments and their buttons always appear and
    /// disappear together.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNeedsAdvisory))]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial string? SelectedNeedsAdvisory { get; private set; }

    /// <summary>Null-check companion to <see cref="SelectedNeedsAdvisory"/> that the free-space tests read.</summary>
    public bool HasSelectedNeedsAdvisory => SelectedNeedsAdvisory is not null;

    /// <summary>
    /// What "Back up all changed" would write right now, named against that action.
    /// <c>null</c> when nothing qualifies — shown only while <c>BackupAllChangedButton</c> itself
    /// is shown (<see cref="HasBackupTargets"/>).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChangedNeedsAdvisory))]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial string? ChangedNeedsAdvisory { get; private set; }

    /// <summary>Null-check companion to <see cref="ChangedNeedsAdvisory"/> that the free-space tests read.</summary>
    public bool HasChangedNeedsAdvisory => ChangedNeedsAdvisory is not null;

    /// <summary>
    /// Stops and restarts <see cref="_spaceAdvisoryDebounce"/> — always paired, never a
    /// bare <c>Start()</c>, so a burst of selection/sweep events collapses into one recompute
    /// instead of piling one up per event. Called wherever the selection, the changed set or the
    /// backup folder changes — <see cref="StartStatusScan"/>'s closing callback among them, so the
    /// advisory settles when the changed set does. Never calls <see cref="RecomputeSpaceAdvisoryAsync"/> directly — the debounce
    /// indirection is the mitigation itself.
    /// </summary>
    private void ScheduleSpaceAdvisoryRecompute()
    {
        _spaceAdvisoryDebounce.Stop();
        _spaceAdvisoryDebounce.Start();
    }

    /// <summary>
    /// Recomputes the three advisory segments above. <c>internal</c> so tests can drive it
    /// directly, since <see cref="_spaceAdvisoryDebounce"/> never fires in an xUnit host. Snapshots
    /// the selected and changed sets from <see cref="Games"/> on the UI thread, then leaves every
    /// filesystem read to <see cref="StatusSweepCoordinator.ComputeAdvisoryAsync"/>. A stale
    /// generation after the await means a newer recompute started, so this result is dropped.
    /// Reads <see cref="CatalogSession.TryGetConfiguredBackupRoot"/>, never
    /// <see cref="EnsureBackupRootAsync"/> — an unset root is an absent segment, not a prompt.
    /// Never reports, opens a dialog or affects any command's <c>CanExecute</c>.
    /// </summary>
    internal async Task RecomputeSpaceAdvisoryAsync()
    {
        var generation = ++_spaceAdvisoryGeneration;

        var hasRoot = _session.TryGetConfiguredBackupRoot(out var root);
        var selectedGames = Games.Where(row => row.IsSelected).Select(row => row.Game).ToList();
        var changedGames = Games
            .Where(row => BulkRunRules.NeedsBackup(row.ComparisonKind))
            .Select(row => row.Game).ToList();

        var (freeText, selectedText, changedText) =
            await _sweep.ComputeAdvisoryAsync(hasRoot, root, selectedGames, changedGames);

        // A newer recompute already started — this one's result is stale and must not overwrite
        // it, the identical generation check StartStatusScan's own sweep applies.
        if (generation != _spaceAdvisoryGeneration)
            return;

        FreeSpaceAdvisory = freeText;
        SelectedNeedsAdvisory = selectedText;
        ChangedNeedsAdvisory = changedText;
    }

    /// <summary>The selected count a running bulk backup is working through — 0 outside one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkBackupProgressText))]
    [NotifyPropertyChangedFor(nameof(IsBulkBackupRunning))]
    public partial int BulkBackupTotal { get; private set; }

    /// <summary>Re-queries every operation gate whenever a running bulk backup's count changes.</summary>
    partial void OnBulkBackupTotalChanged(int value) => RequeryOperationGates();

    /// <summary>
    /// True while a bulk backup is still resolving its backup folder — the start-folder probe, the
    /// picker and the catalog write — before the coordinator claims any row. Without it a row
    /// Backup could run on the same game beside the bulk loop, and each run's
    /// <c>CleanStaleTempFiles</c> could delete the other's staging file. A separate flag because
    /// <see cref="BulkBackupTotal"/> drives the visible progress panel, and marking rows busy
    /// early would move the coordinator's pinned claim order.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBulkPreparing { get; private set; }

    /// <summary>Re-queries every operation gate whenever the bulk preparation window opens or closes.</summary>
    partial void OnIsBulkPreparingChanged(bool value) => RequeryOperationGates();

    /// <summary>
    /// Drives the window-level progress panel's visibility. Derived from the selected count rather
    /// than a second flag, so the panel cannot outlive the state it displays — the bulk body
    /// clears the count before the outcome is reported.
    /// </summary>
    public bool IsBulkBackupRunning => BulkBackupTotal > 0;

    /// <summary>The game a running bulk backup is currently writing — null outside one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkBackupProgressText))]
    public partial string? BulkBackupCurrentGameName { get; private set; }

    /// <summary>
    /// When true, <see cref="BulkBackupProgressText"/> uses the restore sentence so a bulk restore
    /// does not read as a backup while still driving <see cref="IsBulkBackupRunning"/>
    /// from <see cref="BulkBackupTotal"/> (window-close gate).
    /// </summary>
    private bool _bulkProgressIsRestore;

    /// <summary>
    /// The composed bulk-progress sentence. Backup and restore share the counter surface; only
    /// the format key changes.
    /// </summary>
    public string BulkBackupProgressText =>
        Strings.Format(
            _bulkProgressIsRestore ? "str_bulkRestore_progress" : "str_bulkBackup_progress",
            BulkBackupCurrent, BulkBackupTotal, BulkBackupCurrentGameName);

    /// <summary>Drives the DataGrid's empty-state overlay (the zero-games case).</summary>
    public bool HasNoGames => Games.Count == 0;

    /// <summary>
    /// True while a valid single-folder FileDrop is over the main window.
    /// Code-behind writes this from DragOver/DragLeave/Drop; it is visual only.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDragFolderOver { get; set; }

    /// <summary>
    /// The real-time name search text — two-way bound to the search box.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoVisibleGamesHeading))]
    public partial string SearchText { get; set; } = "";

    /// <summary>
    /// Resets and restarts <see cref="_filterDebounce"/> on every keystroke — always
    /// paired, never a bare <c>Start()</c>, so a keystroke arriving before the interval elapses
    /// replaces the pending tick rather than piling up a second one.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _filterDebounce.Stop();
        _filterDebounce.Start();
    }

    /// <summary>
    /// <see cref="GamesView"/>'s filter predicate — a case-insensitive substring match
    /// against <see cref="GameRowViewModel.DisplayName"/>. Never touches <see cref="Games"/>
    /// itself: only which rows the DataGrid shows changes, not what any safety gate
    /// scans.
    /// </summary>
    private bool MatchesSearch(object obj) =>
        obj is not GameRowViewModel row ||
        string.IsNullOrEmpty(SearchText) ||
        row.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Drives the zero-match empty-state overlay — true only when the catalog itself is
    /// non-empty but the active search matches nothing, mutually exclusive with
    /// <see cref="HasNoGames"/> (which requires an empty catalog). Deliberately reads
    /// <see cref="GamesView"/>, the same exception <see cref="HeaderCheckState"/> uses.
    /// </summary>
    public bool HasNoVisibleGames => Games.Count > 0 && !GamesView.Cast<GameRowViewModel>().Any();

    /// <summary>The zero-match empty state's heading, naming the search text.</summary>
    public string NoVisibleGamesHeading => Strings.Format("str_search_noMatchesHeading", SearchText);

    /// <summary>
    /// The zero-match empty state's body. Deliberately generic guidance rather than naming
    /// the search text — the heading above it already reports that nothing matched.
    /// </summary>
    public string NoVisibleGamesBody => Strings.Get("str_search_noMatchesBody");

    /// <summary>
    /// The window and title-bar caption, carrying the running build's version so a user reporting
    /// a problem can say which one they are on without hunting through file properties.
    /// </summary>
    /// <remarks>
    /// Reads the informational version and trims any build metadata a CI stamp might append after
    /// a '+', which is noise on a title bar. Falls back to the plain product name if no version is
    /// readable rather than showing an empty or half-formed caption.
    /// </remarks>
    public static string WindowTitle
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            var version = informational?.Split('+')[0];

            return string.IsNullOrWhiteSpace(version)
                ? Strings.Get("str_app_title")
                : Strings.Format("str_app_titleWithVersion", version);
        }
    }

    /// <summary>Which catalog this window is driving — the full path, shown only in the header's ToolTip.</summary>
    public string CatalogPath => _session.Path;

    /// <summary>
    /// The header's first line — the games-list file name only, full path in the ToolTip
    /// (<see cref="CatalogPath"/>).
    /// </summary>
    public string CatalogDisplay => Strings.Format("str_header_gamesList", Path.GetFileName(CatalogPath));

    /// <summary>
    /// The header's second line's ToolTip — the full resolved backup folder, or <c>null</c> when
    /// none is configured (no tooltip to show).
    /// </summary>
    public string? BackupRootFullPath => _session.TryResolveBackupRoot(out var root) ? root : null;

    /// <summary>
    /// The header's second line — just the last folder segment (the full path forced a wider
    /// window to avoid wrapping; it is still one hover away via <see cref="BackupRootFullPath"/>). Falls back to the full trimmed root
    /// when it has no last segment to take — a drive root ("D:\") or a UNC share root
    /// ("\\server\share") is a valid backup destination under this project's volume-agnostic rule,
    /// and <see cref="Path.GetFileName(string)"/> returns "" for both, which would otherwise leave
    /// this line blank. Recomputed from <see cref="_session"/> on every read; callers that reload
    /// it must raise this property's change notification themselves.
    /// </summary>
    public string BackupRootDisplay =>
        _session.TryResolveBackupRoot(out var root)
            ? Strings.Format("str_header_backupFolder", FormatBackupRootDisplayName(root))
            : Strings.Get("str_header_backupFolderUnset");

    private static string FormatBackupRootDisplayName(string root)
    {
        var trimmed = root.TrimEnd('/', '\\');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial string StatusMessage { get; private set; }

    /// <summary>
    /// The status-bar line: the last <see cref="StatusMessage"/>, then the free-space advisory
    /// segments that currently have text, joined "needed / free" — what a pending backup
    /// needs comes before what is free. One string so the strip stays one fact on the left — the
    /// advisory is not a second column, and it is not written into <see cref="StatusMessage"/>
    /// itself because <see cref="Report"/> overwrites that.
    /// </summary>
    public string StatusLine
    {
        get
        {
            var advisory = new List<string>(3);
            if (SelectedNeedsAdvisory is not null)
                advisory.Add(SelectedNeedsAdvisory);
            if (ChangedNeedsAdvisory is not null)
                advisory.Add(ChangedNeedsAdvisory);
            if (FreeSpaceAdvisory is not null)
                advisory.Add(FreeSpaceAdvisory);

            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(StatusMessage))
                parts.Add(StatusMessage);
            if (advisory.Count > 0)
                parts.Add(string.Join(" / ", advisory));
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>
    /// True for the duration of a running backup write. Requeries every operation gate through
    /// <see cref="RequeryOperationGates"/> the moment this flag changes, rather than on WPF's
    /// next unrelated requery.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBackupRunning { get; private set; }

    /// <summary>Re-queries every operation gate whenever a single-row backup starts or ends.</summary>
    partial void OnIsBackupRunningChanged(bool value) => RequeryOperationGates();

    /// <summary>
    /// How many single-game backups are in flight. One command instance serves every row, so two
    /// rows can legitimately be backing up at once; a plain boolean would be cleared by whichever
    /// finished first, re-enabling Refresh and Change backup folder while the other was still
    /// writing. The flag above is this counter's "greater than zero".
    /// </summary>
    private int _backupsInFlight;

    /// <summary>The same counter for restores, for the same reason.</summary>
    private int _restoresInFlight;

    /// <summary>
    /// True for the whole Find games gesture — probe, dialog and write — so a second scan cannot
    /// start on top of the first. Unlike <see cref="IsBackupRunning"/>, this does not gate
    /// <see cref="RefreshCommand"/> or <see cref="ChangeBackupRootCommand"/>: a scan writes
    /// nothing until the user confirms it, and <see cref="FindGamesAsync"/>'s confirm-time
    /// id-collision recompute is what makes a catalog changed mid-scan safe, so disabling Refresh
    /// for the tens of seconds a scan can run against a stalled network share would take away the
    /// user's only way to see the catalog for no protection they actually need. Carries
    /// <see cref="global::CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedForAttribute"/>
    /// naming <see cref="FindGamesCommand"/> directly. <see cref="CanFindGames"/> reads this flag
    /// and <see cref="CanEditCatalog"/>; the latter's changes arrive through
    /// <see cref="RequeryOperationGates"/>, which also requeries <see cref="FindGamesCommand"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindGamesCommand))]
    public partial bool IsScanRunning { get; private set; }

    /// <summary>
    /// True for the duration of a running restore — the safety backup, the extract/verify/swap and
    /// the confirmation wait in between. A separate flag from <see cref="IsBackupRunning"/> rather
    /// than a reuse of it: a restore's first real step is calling <c>Backup</c> internally for the
    /// safety archive, but the two are still different user-visible operations with different
    /// status text, and folding them into one flag would make that flag true for two things at once
    /// with no way to tell which. Requeries every operation gate through
    /// <see cref="RequeryOperationGates"/> the moment this flag changes, for the same reason
    /// <see cref="IsBackupRunning"/> does.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRestoreRunning { get; private set; }

    /// <summary>Re-queries every operation gate whenever a single-row restore starts or ends.</summary>
    partial void OnIsRestoreRunningChanged(bool value) => RequeryOperationGates();

    /// <summary>
    /// Called from the window's Closing handler when it refuses to close because a backup, restore
    /// or bulk backup is in flight — otherwise the refusal is silent and reads as the window hanging.
    /// </summary>
    public void NotifyCloseBlockedWhileBusy() => _dialog.Info(Strings.Get("str_close_operationInProgress"));

    /// <summary>
    /// Surfaces whatever the catalog load found. Called once the window exists, so a problem with
    /// the file lands in front of a running application instead of replacing it with a dialog.
    /// After a clean load, also reports a configured but unreachable backup folder
    /// without shutting down or disabling the window. Seeded catalogs skip that check so a first-run
    /// template does not Error (the Seeded exception). Problems already have their own dialog,
    /// so health is skipped there too. An unset <c>backupRoot</c> is not unreachable.
    /// </summary>
    public async Task ReportLoad()
    {
        if (_session.FatalError is not null)
        {
            StatusMessage = _session.FatalError;
            _dialog.Error($"{_session.FatalError}\n\n{Strings.Get("str_load_fatalErrorSuffix")}");
            return;
        }

        if (_session.Seeded)
        {
            StatusMessage = Strings.Format("str_load_seeded", _session.Path);
            return;
        }

        if (_session.Problems.Count > 0)
        {
            StatusMessage = Strings.Format("str_load_problemsStatus", Games.Count, _session.Problems.Count);
            _dialog.Error(
                Strings.Format("str_load_problemsHeader", _session.Path) + "\n\n- " +
                string.Join("\n- ", _session.Problems));
            return;
        }

        StatusMessage = Games.Count == 0
            ? Strings.Get("str_load_empty")
            : Games.Count == 1
                ? Strings.Get("str_load_loadedOne")
                : Strings.Format("str_load_loaded", Games.Count);

        if (_session.TryGetConfiguredBackupRoot(out var root)
            && !await Task.Run(() => _backup.IsBackupRootReachable(root)))
        {
            StatusMessage = Strings.Format("str_health_backupRootUnreachable", FormatBackupRootDisplayName(root));
            _dialog.Error(Strings.Format("str_health_backupRootUnreachableDetail", root));
        }
    }

    /// <summary>
    /// Arms the startup prompt for unsecured games. Called once from <c>App.OnStartup</c> after
    /// <see cref="ReportLoad"/>. When the first full sweep has already settled (ReportLoad's
    /// reachability probe can outlast it), the prompt is evaluated at once; otherwise the first
    /// full sweep that settles afterwards consumes it (see <see cref="PromptUnsecuredGamesOnce"/>).
    /// An ordinary mid-session Refresh — whose sweep is also full-scope — never produces the
    /// dialog on its own, and unit tests can drive the decision directly.
    /// </summary>
    public void RequestStartupUnsecuredPrompt()
    {
        bool evaluate;
        lock (_startupPromptLock)
        {
            _startupPromptPending = true;
            evaluate = _firstFullSweepSettled;
        }

        if (evaluate)
            PromptUnsecuredGamesOnce();
    }

    /// <summary>
    /// Visible to tests so they can arm after the first sweep settled, the way App does when
    /// ReportLoad's probe outlasts the sweep.
    /// </summary>
    internal bool IsFirstFullSweepSettled
    {
        get
        {
            lock (_startupPromptLock)
                return _firstFullSweepSettled;
        }
    }

    /// <summary>
    /// Records that the first full sweep has settled and evaluates an armed prompt at once —
    /// arming may have happened while the sweep was still running, so settling alone must decide
    /// the prompt.
    /// </summary>
    private void MarkFirstFullSweepSettled()
    {
        lock (_startupPromptLock)
        {
            _firstFullSweepSettled = true;
        }

        PromptUnsecuredGamesOnce();
    }

    /// <summary>
    /// The rows whose save data is not covered by a backup: saves changed since the last archive,
    /// or no archive at all — exactly the two verdicts the bulk-backup toolbar button acts on.
    /// Internal so the test project can assert the count behind the startup prompt.
    /// </summary>
    internal IReadOnlyList<GameRowViewModel> GetUnsecuredRows() => Games
        .Where(row => BulkRunRules.NeedsBackup(row.ComparisonKind))
        .ToList();

    /// <summary>
    /// The startup prompt itself, run at most once per application session. Called when the prompt
    /// is armed and a full sweep has already settled, and by the first full sweep that settles an
    /// armed prompt — the first evaluation wins. When N games carry an
    /// unsecured verdict, shows one Confirm dialog; Yes re-enters the existing bulk backup flow
    /// (<see cref="BackupAllChangedCommand"/> — the same path the toolbar button takes, which
    /// re-derives its own target list and honours the unset-backup-root picker), No does nothing,
    /// and N = 0 never reaches a dialog at all. Nothing is persisted and nothing runs in the
    /// background — no settings file, no timer, no second prompt.
    /// </summary>
    /// <remarks>
    /// Runs on the UI thread (the sweep completion callback arrives through <see cref="OnUi"/>),
    /// which is why <see cref="IDialogService.Confirm"/> is called under <see cref="Task.Run(Action)"/>:
    /// DialogService blocks its caller until the user answers, and a blocking wait for a dialog
    /// whose pump lives on the very thread being blocked would deadlock. The Yes branch hops back
    /// onto the UI thread before starting the bulk run, because the run's first steps read row
    /// state that has WPF thread affinity.
    /// </remarks>
    internal void PromptUnsecuredGamesOnce()
    {
        // The shown/pending check and the two consumption writes sit inside the same lock as
        // arming and settling, so the arm/settle pair can neither lose nor double the prompt.
        // Everything below — the unsecured count, the message, the Task.Run with Confirm —
        // stays outside it.
        //
        // Not consumed while any row is on Checking: a Refresh clicked during ReportLoad's
        // reachability probe puts every row back to Checking after the first sweep settled, and
        // counting then would find zero unsecured games and spend the one shot. The row that
        // settles last calls back in (RegisterRow's ComparisonKind case).
        lock (_startupPromptLock)
        {
            if (_startupPromptShown || !_startupPromptPending || !_firstFullSweepSettled || IsAnyRowChecking)
                return;
            _startupPromptPending = false;
            _startupPromptShown = true;
        }

        var unsecured = GetUnsecuredRows();
        if (unsecured.Count == 0)
            return;

        var message = unsecured.Count == 1
            ? Strings.Format("str_startup_unsecuredPromptOne", unsecured[0].DisplayName)
            : Strings.Format("str_startup_unsecuredPrompt", unsecured.Count);

        _ = Task.Run(() =>
        {
            try
            {
                if (!_dialog.Confirm(message, Strings.Get("str_startup_unsecuredPromptButton")))
                    return;

                // Routed through the command, not the method: CanBackupAllChanged is the same gate
                // the toolbar button is subject to (sweep still checking, a row busy, another
                // backup/restore in flight), and the prompt's Yes must not bypass it. A disabled
                // command is left unexecuted — the same no-op a disabled toolbar button is.
                OnUi(() =>
                {
                    if (BackupAllChangedCommand.CanExecute(null))
                        BackupAllChangedCommand.Execute(null);
                });
            }
            catch (Exception ex)
            {
                // A Confirm that throws must not die as an unobserved task exception — the one-shot
                // flags are already consumed above, so the prompt is lost either way, but the user
                // still has to hear about it through the app's normal failure surface. The shot
                // stays consumed: a retry would show a second prompt, not the lost first one.
                OnUi(() => Report(OperationResult.Fail(
                    Strings.Format("str_startup_unsecuredPromptFailed", ex.Message))));
            }
        });
    }

    /// <summary>
    /// Shows the Add Game dialog and, on confirmation, appends and saves the new entry. A
    /// cancelled dialog writes nothing and is not a failure. A failed save removes the entry
    /// from memory again so it matches what is actually on disk.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddGame))]
    private Task AddGameAsync(string? initialSaveFolder) => AddGameCoreAsync(initialSaveFolder);

    /// <summary>The toolbar passes no folder, the drop route passes the dropped one; same gate.</summary>
    private bool CanAddGame(string? initialSaveFolder) => CanEditCatalog();

    /// <summary>Why a row edit was refused: the row's own backup/restore, or a catalog update.</summary>
    private static string BusyMessage(GameRowViewModel row) => row.IsBusy
        ? Strings.Format("str_row_busy", row.DisplayName)
        : Strings.Get("str_catalog_busy");

    /// <summary>
    /// Add Game and Find Games write the catalog; while another catalog write or reload is in
    /// flight (<see cref="IsCatalogWriteInFlight"/>, or another edit command) two writers would
    /// race the catalog's changed-on-disk check.
    /// </summary>
    private bool CanEditCatalog() => !IsCatalogWriteInFlight && !IsCatalogCommandRunning;

    /// <summary>
    /// Opens Add Game with <paramref name="folder"/> already in the save-folder field.
    /// Display name stays empty — this method never derives one from the path. Called from
    /// MainWindow drop handling after <see cref="DropFolder.TrySingleExistingDirectory"/> accepted
    /// the payload.
    /// </summary>
    public Task AddGameFromDroppedFolderAsync(string folder)
    {
        // Routed through AddGameCommand, so its IsRunning gates Refresh and backups exactly as
        // the toolbar route does.
        if (!AddGameCommand.CanExecute(folder))
        {
            StatusMessage = Strings.Get("str_catalog_busy");
            return Task.CompletedTask;
        }

        return AddGameCommand.ExecuteAsync(folder);
    }

    private async Task AddGameCoreAsync(string? initialSaveFolder)
    {
        var existingIds = _session.Games.Select(g => g.Id).ToList();
        var result = _dialog.ShowAddGameDialog(existingIds, initialSaveFolder);
        if (result is null)
        {
            StatusMessage = Strings.Get("str_addGame_cancelled");
            return;
        }

        var game = new Game
        {
            Id = result.Id,
            DisplayName = result.DisplayName,
            SaveDirTemplate = result.SaveDirTemplate,
        };

        var saveResult = await _session.AddGamesAsync([game]);
        if (!saveResult.Success)
        {
            Report(saveResult);
            return;
        }

        var added = InsertSorted(game);
        StatusMessage = Strings.Format("str_common_added", game.DisplayName);
        StartStatusScan(only: [added]);
    }

    /// <summary>
    /// Scans the bundled known-games table against this machine and, on confirmation, appends the
    /// picked entries to the catalog in one write — a row-by-row loop that fails halfway
    /// would leave a half-filled catalog. Before writing, every pick is checked against the
    /// catalog's current ids, recomputed here rather than before the probe: Refresh and Add game
    /// are both still usable while the scan runs, and either can change which ids
    /// are taken while this window is open. A colliding pick is refused by name rather than
    /// renamed, suffixed or overwritten — the id names the folder holding the user's
    /// backups, so silently changing it would move them; this is the same rule
    /// <see cref="AddGameDialogViewModel"/>'s collision check enforces for a single entry. A
    /// cancelled dialog, an empty selection, or a selection in which every pick collides all write
    /// nothing. A failed save removes the appended entries from memory again so the window
    /// matches what is actually on disk, the same rollback <see cref="AddGameAsync"/> performs for
    /// a single entry.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFindGames))]
    private async Task FindGamesAsync()
    {
        IsScanRunning = true;
        try
        {
            StatusMessage = Strings.Get("str_findGames_scanning");
            // A snapshot taken here on the UI thread: Remove/Change save folder/Add game may
            // change the live list while the scan enumerates it on a pool thread.
            var currentGames = _session.Games.ToList();
            var result = await Task.Run(() => _knownGamesScanner.Scan(KnownGamesCatalog.All, currentGames));

            var picked = _dialog.ShowFindGamesDialog(result);
            if (picked is null)
            {
                StatusMessage = Strings.Get("str_findGames_cancelled");
                return;
            }

            if (picked.Count == 0)
            {
                StatusMessage = Strings.Get("str_findGames_nothingSelected");
                return;
            }

            // Existing ids are recomputed here, not before the probe, so a catalog changed while
            // this scan ran (Refresh, Add game) is still respected.
            var (accepted, refused) = _session.PartitionNewGames(picked);

            if (accepted.Count == 0)
            {
                StatusMessage = Strings.Format("str_findGames_allRefused", string.Join(", ", refused));
                return;
            }

            // The scan can run long and Find Games is deliberately not a gating command, so another
            // catalog write may have started meanwhile; writing now would race it.
            if (IsCatalogWriteInFlight)
            {
                StatusMessage = Strings.Get("str_catalog_busy");
                return;
            }

            var saveResult = await _session.AddGamesAsync(accepted);
            if (!saveResult.Success)
            {
                Report(saveResult);
                return;
            }

            var added = accepted.Select(InsertSorted).ToList();

            var addedMessage = accepted.Count == 1
                ? Strings.Format("str_common_added", accepted[0].DisplayName)
                : Strings.Format("str_findGames_addedMany", accepted.Count);

            StatusMessage = refused.Count == 0
                ? addedMessage
                : Strings.Format("str_findGames_someRefused", addedMessage, string.Join(", ", refused));

            StartStatusScan(only: added);
        }
        finally
        {
            IsScanRunning = false;
        }
    }

    private bool CanFindGames() => !IsScanRunning && CanEditCatalog();

    /// <summary>
    /// Opens the loaded catalog file in its associated editor, falling back to Explorer when
    /// none is associated. Starting a process touches the shell, so it runs off the UI
    /// thread like every other filesystem-facing call.
    /// </summary>
    [RelayCommand]
    private async Task OpenCatalogAsync()
    {
        var result = await Task.Run(() => _dialog.OpenInEditor(_session.Path));
        Report(result);
    }

    /// <summary>
    /// Opens the About window. Synchronous and fire-and-forget like the other dialog-opening
    /// commands — <see cref="IDialogService.ShowAboutDialog"/> owns the user-guide link that
    /// replaced this window's old Help button.
    /// </summary>
    [RelayCommand]
    private void About() => _dialog.ShowAboutDialog();

    /// <summary>
    /// Re-reads the catalog file from disk and re-scans status — one button, one meaning.
    /// A malformed file is reported exactly as at startup (via <see cref="ReportLoad"/>) and
    /// the previously loaded rows stay on screen instead of being emptied: taking away the only
    /// working view the user has while they fix the file would make the mistake worse, not better.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshOrChangeBackupRoot))]
    private async Task RefreshAsync()
    {
        StatusMessage = Strings.Get("str_reload_reloading");
        await _session.ReloadAsync();
        OnPropertyChanged(nameof(BackupRootDisplay));
        OnPropertyChanged(nameof(BackupRootFullPath));

        if (_session.FatalError is not null)
        {
            await ReportLoad();
            return;
        }

        Games.Clear();
        foreach (var game in _session.Games)
            Games.Add(RegisterRow(game));

        await ReportLoad();
        StartStatusScan(announceProgress: true);
    }

    /// <summary>
    /// Prompts for a new backup folder and writes it back into the catalog through the same
    /// atomic write add-game uses. A cancelled picker writes nothing and shows no
    /// message — a cancelled action is not a failure. Nothing already backed up is moved, and the
    /// chosen folder's volume is never inspected: the folder the user picked is the folder that is
    /// stored, whatever disk it sits on.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshOrChangeBackupRoot))]
    private async Task ChangeBackupRootAsync()
    {
        var hadOldRoot = _session.TryResolveBackupRoot(out var oldRoot);
        var picked = _dialog.PickFolder(
            Strings.Get("str_common_pickBackupFolderTitle"),
            hadOldRoot ? oldRoot : null);
        if (picked is null)
            return;

        var saveResult = await _session.SetBackupRootAsync(picked);
        if (!saveResult.Success)
        {
            Report(saveResult);
            return;
        }

        OnPropertyChanged(nameof(BackupRootDisplay));
        OnPropertyChanged(nameof(BackupRootFullPath));
        Report(OperationResult.Ok(hadOldRoot
            ? Strings.Format("str_backupRoot_updated", oldRoot.TrimEnd('/', '\\'))
            : Strings.Format("str_backupRoot_setTo", picked.TrimEnd('/', '\\'))));

        // The free-space segment names the folder just replaced — stale the instant this succeeds.
        ScheduleSpaceAdvisoryRecompute();

        // Every row's save-vs-backup verdict was computed against the folder just replaced — stale
        // the instant this succeeds, the same way a successful backup or restore already re-sweeps.
        StartStatusScan();
    }

    /// <summary>
    /// Shared gate for <see cref="RefreshCommand"/> and <see cref="ChangeBackupRootCommand"/>.
    /// <see cref="IsAnyRowBusy"/> is what actually catches a restore still in its preflight
    /// (precondition check, folder picker), since <see cref="IsRestoreRunning"/> alone is not
    /// raised until a plan is in hand — refreshing mid-preflight would clear <see cref="Games"/>
    /// out from under the row the preflight still holds a reference to, and changing the backup
    /// folder mid-preflight would leave its safety backup written to a folder the picker no longer
    /// points at.
    /// </summary>
    private bool CanRefreshOrChangeBackupRoot() => !IsAnyOperationRunning;

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread when there is one, and inline when there is
    /// not (a plain xUnit host constructs no <see cref="System.Windows.Application"/>, which is
    /// what keeps this view model testable without a dispatcher).
    /// </summary>
    /// <remarks>
    /// The background work in this class is deliberately off the UI thread, but the moment a
    /// result is written onto a row it becomes a UI mutation: the generated setters raise
    /// PropertyChanged, and the bindings behind those have thread affinity.
    /// </remarks>
    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    /// <param name="announceProgress">
    /// When true, the status line says the sweep is running and returns to whatever it said before
    /// once it finishes. Refresh passes true: without it the line reads "Loaded N games." while the
    /// sweep is still working, which announces completion before the work has started. The backup
    /// and restore callers pass false, because their own outcome message owns the line by then
    /// (progress and outcome never share a surface).
    /// </param>
    /// <param name="only">
    /// The rows to scan, or null for every row. A backup, a restore, an added game or a changed
    /// save folder alters what is true about its own rows only, so those callers pass just those:
    /// re-walking every save folder — and flipping the whole list to "Checking…" and back — after
    /// each single backup is work and flicker that tells the user nothing new. Refresh, startup
    /// and a changed backup folder alter every row's answer and scan them all.
    /// </param>
    private void StartStatusScan(bool announceProgress = false, IReadOnlyList<GameRowViewModel>? only = null)
    {
        var isFullSweep = only is null;
        var snapshot = _sweep.Reset((IEnumerable<GameRowViewModel>?)only ?? Games);

        if (snapshot.Count == 0)
        {
            // An empty catalog settles its first full sweep with nothing to check: the prompt's
            // moment is now, so the armed prompt is consumed here instead of being saved up for
            // a later Refresh's sweep to fire mid-session.
            if (isFullSweep)
                MarkFirstFullSweepSettled();
            return;
        }

        var root = "";
        var hasRoot = _session.TryGetConfiguredBackupRoot(out var resolvedRoot);
        if (hasRoot)
            root = resolvedRoot;

        var messageBeforeSweep = StatusMessage;
        var sweepMessage = Strings.Get("str_status_checkingSaveFolders");
        if (announceProgress)
            StatusMessage = sweepMessage;

        _ = Task.Run(async () =>
        {
            var becameReachable = await _sweep.WalkAsync(snapshot, hasRoot, root);

            // The folder just came back while this was a narrowed scan: rows outside `only` are
            // still sitting on whatever verdict the last sweep gave them, so one follow-up full
            // sweep settles them. becameReachable only ever fires on the flip (see the
            // coordinator), so this cannot re-trigger itself on the full sweep it starts.
            if (becameReachable && !isFullSweep)
                OnUi(() => StartStatusScan());

            // The advisory settles once every row's comparison has settled — every
            // StartStatusScan() call marks the changed set stale until this fires, not only a
            // caller that also asked to announce progress.
            OnUi(ScheduleSpaceAdvisoryRecompute);

            // The prompt can only be decided once a full sweep has laid a real verdict on every
            // row — a narrowed scan (only is not null) leaves most rows on Checking. The first
            // full sweep to settle marks the flag and evaluates an armed prompt; later full
            // sweeps find the shot consumed or never armed.
            if (isFullSweep)
                OnUi(MarkFirstFullSweepSettled);

            if (!announceProgress)
                return;

            // Only restore the previous line if nothing else claimed it meanwhile — an outcome
            // message, or a newer sweep's own announcement, that arrived during this sweep
            // outranks this. Checked here, on the UI thread inside the callback, rather than
            // against generation before scheduling it: the same TOCTOU OnUi's other callers guard
            // against above applies equally to this one.
            OnUi(() =>
            {
                if (StatusMessage == sweepMessage)
                    StatusMessage = messageBeforeSweep;
            });
        });
    }

    /// <summary>
    /// One-line forward: the flow lives in <see cref="RowActionsCoordinator.BackupAsync"/>,
    /// which receives the row's byte progress built here on the UI thread.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBackupRow))]
    private Task BackupAsync(GameRowViewModel? row) =>
        _rowActions.BackupAsync(row, row is null ? null : CreateByteProgress(row));

    /// <summary>
    /// Keeps a per-row backup from starting on a row a running bulk operation (backup-all-changed,
    /// Back up selected, or Restore selected — all three share <see cref="BulkBackupTotal"/>) did
    /// not itself claim, and while a bulk run is still preparing its backup folder
    /// (<see cref="IsBulkPreparing"/>) — the same-game overlap during the preparation window is the
    /// data-corrupting one, because each run's <c>CleanStaleTempFiles</c> could delete the other's
    /// staging file.
    /// </summary>
    private bool CanBackupRow(GameRowViewModel? row) =>
        row is not null && !row.IsBusy && !IsBulkBackupRunning && !IsBulkPreparing
        && !RefreshCommand.IsRunning && !ChangeBackupRootCommand.IsRunning && !IsChoosingBackupRoot;

    /// <summary>
    /// One-line forward: the flow lives in <see cref="RowActionsCoordinator.RestoreAsync"/>,
    /// which receives the row's step progress built here on the UI thread.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestoreRow))]
    private Task RestoreAsync(GameRowViewModel? row) =>
        _rowActions.RestoreAsync(row, row is null ? null : CreateStepProgress(row));

    /// <summary>
    /// Keeps a per-row restore from starting on a row a running bulk operation (backup-all-changed,
    /// Back up selected, or Restore selected — all three share <see cref="BulkBackupTotal"/>) did
    /// not itself claim, or while a bulk run is still preparing its backup folder
    /// (<see cref="IsBulkPreparing"/>) — the same gap <see cref="CanBackupRow"/> closes for backups.
    /// </summary>
    private bool CanRestoreRow(GameRowViewModel? row) =>
        row is not null && !row.IsBusy && !IsBulkBackupRunning && !IsBulkPreparing
        && !RefreshCommand.IsRunning && !ChangeBackupRootCommand.IsRunning && !IsChoosingBackupRoot;

    /// <summary>
    /// One-line forward: the flow lives in <see cref="RowActionsCoordinator.OpenSaveFolderAsync"/>.
    /// </summary>
    [RelayCommand]
    private Task OpenSaveFolderAsync(GameRowViewModel? row) => _rowActions.OpenSaveFolderAsync(row);

    /// <summary>
    /// The repair route for a broken save path: shows the Change Save Folder dialog pre-filled with the row's current
    /// template and, on confirmation, writes through <see cref="CatalogSession.SetSaveFolderAsync"/>
    /// — the only call this view model makes into that member, and the only write CatalogSession
    /// performs for a template change. This method does the view-side work only: on success it
    /// replaces the row at its index with a freshly built one carrying the updated
    /// <see cref="Game"/> (init-only, so <see cref="CatalogSession"/> already constructed the new
    /// instance) and re-runs the status sweep; on failure it reports and leaves the row exactly as
    /// it was — <see cref="CatalogSession.SetSaveFolderAsync"/> already rolled the in-memory entry
    /// back to its previous value, so nothing here needs to.
    /// </summary>
    [RelayCommand]
    private async Task ChangeSaveFolderAsync(GameRowViewModel? row)
    {
        // IsBulkPreparing: a bulk run has already captured its targets but not yet marked them
        // busy (it is still resolving the backup folder), so replacing this row now would let the
        // run back up the old Game with its old save path. The overflow menu calls Execute
        // directly, which skips CanExecute, so the guard lives here.
        if (row is null)
            return;
        if (row.IsBusy || IsCatalogWriteInFlight)
        {
            StatusMessage = BusyMessage(row);
            return;
        }

        var newTemplate = _dialog.ShowChangeSaveFolderDialog(row.DisplayName, row.Game.SaveDirTemplate);
        if (newTemplate is null)
            return;

        // The dialog is modal but pumps messages; a backup on this row or a catalog reload may
        // have started meanwhile (same re-check RemoveGameAsync makes after its confirmation).
        if (row.IsBusy || IsCatalogWriteInFlight)
        {
            StatusMessage = BusyMessage(row);
            return;
        }

        // Raised before the await — the same re-entrancy guard BackupAsync/RestoreAsync use —
        // so a concurrent Refresh (CanRefreshOrChangeBackupRoot gates on IsAnyRowBusy) or
        // RemoveGameAsync on this same row cannot rebuild/remove Games out from under the index
        // looked up below.
        row.IsBusy = true;
        try
        {
            var saveResult = await _session.SetSaveFolderAsync(row.Game, newTemplate);
            if (!saveResult.Success)
            {
                Report(saveResult);
                return;
            }

            // The row's position is looked up after the await, by reference, and never taken from
            // CatalogSession.Games: sorted insert makes Games order differ from the catalog's
            // append order. The repaired Game is matched by id. Leave the row untouched when
            // either lookup misses rather than indexing blindly.
            var rowIndex = Games.IndexOf(row);
            if (rowIndex >= 0)
            {
                var repaired = _session.Games.FirstOrDefault(g =>
                    string.Equals(g.Id, row.Game.Id, StringComparison.OrdinalIgnoreCase));
                if (repaired is not null)
                {
                    var replacement = RegisterRow(repaired);
                    Games[rowIndex] = replacement;
                    StartStatusScan(only: [replacement]);
                }
            }
            Report(OperationResult.Ok(Strings.Format("str_changeSaveFolder_outcome", row.DisplayName)));
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    /// <summary>
    /// Removes the row from the catalog after a confirmation. Saves and backup archives stay on
    /// disk — this only drops the list entry. Confirm runs off the UI thread because
    /// <see cref="IDialogService.Confirm"/> blocks on a dispatcher-marshalled dialog and would
    /// deadlock if waited on here (the same constraint RestoreService already honours). A declined
    /// confirmation writes nothing. A failed save puts the in-memory entry back, so the row stays.
    /// </summary>
    [RelayCommand]
    private async Task RemoveGameAsync(GameRowViewModel? row)
    {
        // IsBulkPreparing: same window as ChangeSaveFolderAsync — the bulk run's targets are
        // captured but not yet busy, so a removal now would still be backed up afterwards.
        if (row is null)
            return;
        if (row.IsBusy || IsCatalogWriteInFlight)
        {
            StatusMessage = BusyMessage(row);
            return;
        }

        var confirmed = await Task.Run(() =>
            _dialog.Confirm(
                Strings.Format("str_removeGame_confirm", row.DisplayName),
                Strings.Get("str_removeGame_confirmButton")));
        if (!confirmed)
        {
            StatusMessage = Strings.Get("str_removeGame_cancelled");
            return;
        }

        // The row's own Backup/Restore stay usable while the confirmation is open; a row that went
        // busy meanwhile must not be dropped under its running operation.
        if (row.IsBusy || IsCatalogWriteInFlight)
        {
            StatusMessage = BusyMessage(row);
            return;
        }

        // Busy across the write, the same guard ChangeSaveFolderAsync raises: the row's own
        // Backup/Restore must not start on a row that is about to be dropped from the list.
        OperationResult saveResult;
        row.IsBusy = true;
        try
        {
            saveResult = await _session.RemoveGameAsync(row.Game);
        }
        finally
        {
            row.IsBusy = false;
        }

        if (!saveResult.Success)
        {
            Report(saveResult);
            return;
        }

        _selectedIds.Remove(row.Game.Id);
        Games.Remove(row);

        // A startup prompt deferred while rows were Checking is otherwise re-evaluated only when
        // a row settles; removing the last Checking row settles nothing, so evaluate it here.
        PromptUnsecuredGamesOnce();

        // The removed game may have been part of the selected or changed totals the status line
        // still shows; no sweep follows a removal to settle them.
        ScheduleSpaceAdvisoryRecompute();
        Report(OperationResult.Ok(Strings.Format("str_removeGame_done", row.DisplayName)));
    }

    /// <summary>
    /// Backs up every row whose comparison is <see cref="BackupComparisonKind.SavesChangedSinceBackup"/>
    /// or <see cref="BackupComparisonKind.NeverBackedUp"/>. No confirmation prompt: backup is additive and non-destructive,
    /// and a modal in front of the one action that protects data is friction pointed the wrong way.
    /// A per-game failure is collected and the loop continues; progress and the closing outcome
    /// never share a surface — the progress state is cleared before <see cref="Report"/> runs.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBackupAllChanged))]
    private async Task BackupAllChangedAsync()
    {
        // The busy exclusion and its reasoning live in BulkRunRules.ChangedTargets.
        var targets = BulkRunRules.ChangedTargets(Games);
        if (targets.Count == 0)
            return;

        await RunBulkBackupAsync(targets, "str_bulkBackup_allSucceeded", "str_bulkBackup_partialFailure");
    }

    /// <summary>
    /// Raises the bulk counters for a starting bulk run — backup or restore, told apart by
    /// <paramref name="isRestore"/> so <see cref="BulkBackupProgressText"/> picks the right
    /// sentence. The claim order this participates in (reset to Checking, then these counters,
    /// then IsBusy) is the re-entrancy guard; see <see cref="BulkBackupCoordinator"/>.
    /// </summary>
    private void BeginBulkProgress(int total, string firstGameName, bool isRestore)
    {
        _bulkProgressIsRestore = isRestore;
        BulkBackupTotal = total;
        BulkBackupCurrent = 0;
        BulkBackupCurrentGameName = firstGameName;
    }

    /// <summary>Advances the bulk counters to the game a bulk run just reached.</summary>
    private void SetBulkProgress(int current, string gameName)
    {
        BulkBackupCurrent = current;
        BulkBackupCurrentGameName = gameName;
    }

    /// <summary>
    /// Clears the bulk counters at the end of a bulk run — the restore finally's order (flag
    /// first, then counters). For a backup the flag write is a no-op, because the flag is only
    /// ever true inside a restore run, which the bulk gates exclude.
    /// </summary>
    private void EndBulkProgress()
    {
        _bulkProgressIsRestore = false;
        BulkBackupTotal = 0;
        BulkBackupCurrent = 0;
        BulkBackupCurrentGameName = null;
    }

    /// <summary>
    /// The one per-row byte-reporter lambda, shared by the single-row backup and every bulk
    /// backup target. Constructed on the UI thread, so its callbacks marshal back onto the
    /// dispatcher rather than arriving on whatever thread runs the write.
    /// </summary>
    private static IProgress<long> CreateByteProgress(GameRowViewModel row) =>
        new Progress<long>(written => { if (row.BackupBytesTotal > 0) row.BackupBytesWritten = written; });

    /// <summary>
    /// The one per-row restore-step reporter lambda, shared by the single-row restore and every
    /// bulk-restore target. Constructed on the UI thread, for the same reason
    /// <see cref="CreateByteProgress"/> is.
    /// </summary>
    private static IProgress<int> CreateStepProgress(GameRowViewModel row) =>
        new Progress<int>(step =>
        {
            row.RestoreStep = step;
            row.RestoreStepText = Strings.Get($"str_restoreStep_{step}");
        });

    /// <summary>
    /// Delegates the bulk-backup loop — root resolution stays here, the claim, the write loop
    /// and the release live in <see cref="BulkBackupCoordinator.BackupAsync"/> — and reports the
    /// closing outcome.
    /// </summary>
    private async Task RunBulkBackupAsync(
        IReadOnlyList<GameRowViewModel> targets, string allSucceededKey, string partialFailureKey)
    {
        // The preparation guard is raised before the first await so nothing else that writes can
        // start while the folder is still unresolved. It ends before the coordinator claims, and
        // the hand-off has no gap: _bulk.BackupAsync resets, raises the counters and marks every
        // target busy synchronously before its first await (BulkBackupCoordinator's claim order).
        IsBulkPreparing = true;
        string? root;
        try
        {
            root = await EnsureBackupRootAsync(alsoSkip: targets);
        }
        finally
        {
            IsBulkPreparing = false;
        }

        if (root is null)
            return; // Either the user cancelled the picker or saving the choice failed — both routes already told the user what happened.

        var byteProgress = targets.ToDictionary(row => row, CreateByteProgress);

        var run = await _bulk.BackupAsync(
            targets,
            root,
            SetBulkProgress,
            byteProgress,
            (total, first) => BeginBulkProgress(total, first, isRestore: false),
            () =>
            {
                EndBulkProgress();
                StartStatusScan(only: targets);
            });

        if (run is null)
            return;

        Report(BulkRunRules.BackupOutcome(targets, run.Failed, allSucceededKey, partialFailureKey), run.FailureDetails);
    }

    /// <summary>
    /// Enabled only when the sweep has settled <em>and</em> no backup or restore is already
    /// writing. <see cref="IsAnyRowChecking"/> is the selection-is-defined gate; the busy flags
    /// stop a bulk run from starting on top of a single-row backup/restore (or a bulk run already
    /// in flight) — <see cref="IsAnyRowBusy"/> is what actually catches a restore still in its
    /// preflight, since <see cref="IsRestoreRunning"/> alone is not raised until a plan is in hand.
    /// The reverse — a single-row action on a row the bulk run has claimed — is already refused by
    /// that row's <c>IsBusy</c>.
    /// </summary>
    private bool CanBackupAllChanged() => !IsAnyRowChecking && !IsAnyOperationRunning;

    /// <summary>
    /// Backs up every checked row — the target set is the selection itself,
    /// read from <see cref="Games"/> (never <see cref="GamesView"/>) and unfiltered by
    /// <see cref="GameRowViewModel.ComparisonKind"/>: a checked game is an explicit
    /// per-game choice, unlike <see cref="BackupAllChangedAsync"/>'s automatic, comparison-driven
    /// selection. Otherwise clones that method's mechanics exactly — no confirmation
    /// prompt, the same busy-claim/progress-surface/re-sweep shape — and, like it, never clears
    /// the selection when the run finishes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBackupSelected))]
    private async Task BackupSelectedAsync()
    {
        // Mirrors BackupAllChangedAsync's busy exclusion — see BulkRunRules.SelectedTargets.
        var targets = BulkRunRules.SelectedTargets(Games);
        if (targets.Count == 0)
            return;

        await RunBulkBackupAsync(targets, "str_bulkBackupSelected_allSucceeded", "str_bulkBackupSelected_partialFailure");
    }

    /// <summary>
    /// <see cref="CanBackupAllChanged"/> plus a non-empty-selection check — the extra term that
    /// makes an empty-handed "Back up selected" run unreachable, since nothing
    /// else in this gate would otherwise stop it from running against zero targets.
    /// </summary>
    private bool CanBackupSelected() => HasAnySelected && CanBackupAllChanged();

    /// <summary>
    /// Restores every checked row from its newest ordinary backup, no picker. The sequential
    /// loop — on the command's own context, never one <c>Task.Run</c> around it, because Confirm
    /// needs the dispatcher — lives in <see cref="BulkBackupCoordinator.RestoreAsync"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestoreSelected))]
    private async Task RestoreSelectedAsync()
    {
        var targets = BulkRunRules.SelectedTargets(Games);
        if (targets.Count == 0)
            return;

        // An unset backup folder is a named error here, never a prompt for one — the same rule
        // RestoreAsync follows: picking a destination is a backup gesture, and a folder chosen
        // now could not hold anything to restore from anyway.
        if (!_session.TryGetConfiguredBackupRoot(out var root))
        {
            Report(OperationResult.Fail(Strings.Get("str_restoreSelected_noBackupFolder")));
            return;
        }

        // One step reporter per target, built up front on the UI thread like the bulk backup's
        // byte reporters, so every report marshals back onto the dispatcher.
        var stepProgress = targets.ToDictionary(row => row, CreateStepProgress);

        var run = await _bulk.RestoreAsync(
            targets,
            root,
            stepProgress,
            (total, first) => BeginBulkProgress(total, first, isRestore: true),
            SetBulkProgress,
            EndBulkProgress);

        if (run is null)
            return;

        StartStatusScan(only: targets);

        // The details list is safe to pass on every branch: it gains an entry exactly when failed
        // does, Report ignores an empty list, and it returns early for a Cancel result.
        Report(BulkRunRules.RestoreOutcome(targets, run.Restored, run.Failed, run.Declined), run.FailureDetails);
    }

    /// <summary>
    /// Same terms as <see cref="CanBackupSelected"/> — empty selection is unreachable.
    /// </summary>
    private bool CanRestoreSelected() => CanBackupSelected();

    /// <summary>
    /// Returns the configured backup folder, asking for one the first time it is needed. Backing
    /// up is the point at which the folder actually matters, so an unset folder is a question
    /// rather than an error — the user picks a folder and the backup continues in the same
    /// gesture. The picker opens on a detected external volume when one is present, via
    /// a suggestion-only start folder — whatever the user picks is what gets stored. Returns null
    /// when the user cancelled, which sets a visible <see cref="StatusMessage"/>, or when
    /// <see cref="CatalogSession.SetBackupRootAsync"/> failed, which this method reports itself.
    /// </summary>
    /// <param name="alsoSkip">
    /// Rows the caller is about to back up itself (a bulk run's targets, not yet marked busy):
    /// left out of the re-check that follows a newly chosen folder, since the backup's own
    /// closing rescan settles them and a sweep now would only walk their folders twice.
    /// </param>
    private async Task<string?> EnsureBackupRootAsync(IReadOnlyCollection<GameRowViewModel>? alsoSkip = null)
    {
        if (_session.TryGetConfiguredBackupRoot(out var existing))
        {
            return existing;
        }

        string? picked;
        IsChoosingBackupRoot = true;
        try
        {
            var start = await Task.Run(_suggestBackupStartFolder);
            picked = _dialog.PickFolder(Strings.Get("str_common_pickBackupFolderTitle"), start);
            if (picked is null)
            {
                StatusMessage = Strings.Get("str_backup_cancelledNoFolder");
                return null;
            }

            var saveResult = await _session.SetBackupRootAsync(picked);
            if (!saveResult.Success)
            {
                Report(saveResult);
                return null;
            }
        }
        finally
        {
            IsChoosingBackupRoot = false;
        }

        OnPropertyChanged(nameof(BackupRootDisplay));
        OnPropertyChanged(nameof(BackupRootFullPath));

        // Every other row's verdict was computed with no backup folder at all — stale now, the
        // same reason ChangeBackupRootAsync re-sweeps. The row(s) the calling backup is about to
        // write are left to that backup's own closing rescan; the sweep tokens keep an overlap
        // harmless anyway.
        ScheduleSpaceAdvisoryRecompute();
        var others = Games.Where(row => !row.IsBusy && alsoSkip?.Contains(row) != true).ToList();
        if (others.Count > 0)
            StartStatusScan(only: others);

        return picked;
    }

    // The IRowActionHost members below exist only for RowActionsCoordinator — implemented
    // explicitly so no public member is added; each routes one coordinator touch onto this view
    // model's own state.

    async Task<string?> IRowActionHost.EnsureBackupRootAsync() => await EnsureBackupRootAsync();
    bool IRowActionHost.TryGetConfiguredBackupRoot(out string root) => _session.TryGetConfiguredBackupRoot(out root);
    void IRowActionHost.SetStatus(string message) => StatusMessage = message;
    void IRowActionHost.Report(OperationResult result) => Report(result);
    void IRowActionHost.Rescan(GameRowViewModel row) => StartStatusScan(only: [row]);
    void IRowActionHost.BackupStarted() => IsBackupRunning = ++_backupsInFlight > 0;
    void IRowActionHost.BackupFinished() => IsBackupRunning = --_backupsInFlight > 0;
    void IRowActionHost.RestoreStarted() => IsRestoreRunning = ++_restoresInFlight > 0;
    void IRowActionHost.RestoreFinished() => IsRestoreRunning = --_restoresInFlight > 0;

    /// <summary>
    /// Routes an outcome to its message surface: <see cref="StatusMessage"/> is always set
    /// (it is the snackbar's source), then a success additionally reaches
    /// <see cref="IDialogService.Info"/> (transient snackbar) and a failure reaches
    /// <see cref="IDialogService.Error"/> (blocking <c>ContentDialog</c>).
    /// The calls and their meaning are the same on every surface; only the routing differs.
    /// </summary>
    /// <param name="failureDetails">
    /// One sentence per failed game of a bulk run, appended to the blocking dialog only. The status
    /// line stays the one-line summary; the dialog is where there is room for why each game failed,
    /// which is the part the user can act on.
    /// </param>
    private void Report(OperationResult result, IReadOnlyList<string>? failureDetails = null)
    {
        StatusMessage = result.Message;
        if (result.Cancelled)
            return;

        if (result.Success)
            _dialog.Info(result.Message);
        else if (failureDetails is { Count: > 0 })
            _dialog.Error(result.Message + "\n\n- " + string.Join("\n- ", failureDetails));
        else
            _dialog.Error(result.Message);
    }
}
