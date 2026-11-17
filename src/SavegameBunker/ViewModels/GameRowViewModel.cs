using CommunityToolkit.Mvvm.ComponentModel;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// One row in the game list. Carries no installation state: every action SavegameBunker offers
/// works on save data, which exists whether or not the game is currently installed.
/// </summary>
/// <remarks>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// </remarks>
public sealed partial class GameRowViewModel : CommunityToolkitObservableObject
{
    // Partial-property [ObservableProperty] form (not the classic field-attribute style): every
    // property here keeps a private setter, which the field-attribute style cannot reproduce — it
    // always widens to a public setter. Requires <LangVersion>14.0</LangVersion> in the csproj.
    [ObservableProperty] public partial string StatusText { get; private set; }
    [ObservableProperty] public partial string FileCountText { get; private set; }
    [ObservableProperty] public partial string SizeText { get; private set; }
    [ObservableProperty] public partial RowSeverity GameSeverity { get; private set; }
    [ObservableProperty] public partial string VerdictText { get; private set; }
    [ObservableProperty] public partial RowSeverity StatusSeverity { get; private set; }

    /// <summary>
    /// The save directory's raw last-write time, bound through the ported
    /// <c>RelativeTimeConverter</c> for the visible text and, converter-less, for the cell's
    /// absolute-value <c>ToolTip</c>. Set from <see cref="SaveStatus.LastModified"/> in
    /// <see cref="ApplyStatus"/>'s <c>Found</c> branch. Replaces the pre-formatted timestamp
    /// string the row used to carry.
    /// </summary>
    [ObservableProperty] public partial DateTime? LastPlayedAt { get; private set; }

    /// <summary>
    /// The newest backup's raw timestamp, replacing the pre-formatted timestamp string
    /// the row used to carry. Set from <see cref="BackupComparison"/> facts only, independent
    /// of <see cref="ComparisonKind"/> — a row with no current save data still shows when an
    /// archive exists.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewestBackup))]
    public partial DateTime? NewestBackupAt { get; private set; }

    /// <summary>
    /// Drives the merged Status cell's muted time subline's visibility — present whenever a
    /// backup exists, whatever this row's comparison kind is (E2 empty/populated).
    /// </summary>
    public bool HasNewestBackup => NewestBackupAt is not null;

    /// <summary>
    /// The verdict's raw kind, exposed so task 5's bulk-backup selection can filter rows without
    /// re-deriving it from severity or text.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpToDate))]
    public partial BackupComparisonKind ComparisonKind { get; private set; }

    /// <summary>
    /// True only for <see cref="BackupComparisonKind.UpToDate"/> — the Status cell icon's single
    /// accent-coloured state (the green accent is never a severity colour, and this is its
    /// one status-carrying use).
    /// </summary>
    public bool IsUpToDate => ComparisonKind == BackupComparisonKind.UpToDate;

    /// <summary>
    /// True while this row's own backup or restore is running. The design prefers a
    /// generated command's own <c>IsRunning</c> flag over a hand-rolled boolean, and that flag is
    /// what gates the window-level controls — but one command instance serves every row, so it
    /// cannot say <em>which</em> row is busy. This flag exists for exactly that reason and for no
    /// other: it scopes the ring and the disabled overflow to the row actually working, so the
    /// rest of the window stays live.
    /// </summary>
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>
    /// Whether this row's checkbox is checked. Public-setter exception (like
    /// <see cref="IsBusy"/>): the checkbox's <c>TwoWay</c> binding writes to it directly from the
    /// View. Defaults to <c>false</c>, C#'s own <c>bool</c> default — a row starts unselected.
    /// The durable source of truth is <see cref="ViewModels.MainViewModel"/>'s id-keyed selection
    /// set, not this property; <c>RegisterRow</c> seeds it on every row construction.
    /// </summary>
    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>
    /// The restore step currently running on this row, 1 to 3, or 0 when no restore is in flight.
    /// Bound as the determinate bar's value against a fixed maximum of three — the three stages
    /// are contractual, so the denominator is a fact, not an estimate.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRestoring))]
    [NotifyPropertyChangedFor(nameof(RestoreStepShortText))]
    public partial int RestoreStep { get; set; }

    /// <summary>
    /// <see cref="RestoreStep"/> in the few words that fit under the row's own progress bar; the
    /// full sentence in <see cref="RestoreStepText"/> is that label's tooltip. Empty when idle.
    /// </summary>
    public string RestoreStepShortText =>
        RestoreStep is >= 1 and <= 3 ? Strings.Get($"str_restoreStepShort_{RestoreStep}") : "";

    /// <summary>The plain-language label for <see cref="RestoreStep"/>, empty when idle.</summary>
    [ObservableProperty] public partial string RestoreStepText { get; set; }

    /// <summary>
    /// The <c>TakenAt</c> of the archive this row's save directory was last restored from, or
    /// <c>null</c> if no restore has happened since this row's last successful backup. Not bound
    /// to any view — a pure hint <see cref="Services.BackupComparer.Compare"/> uses to tell "the
    /// newest backup already matches what's on disk" from "a restore replaced disk content with
    /// an older archive, so the newest backup (the fresh pre-restore safety copy) no longer
    /// matches, whatever the plain mtime comparison says". <see cref="ViewModels.MainViewModel"/>
    /// sets it after a successful restore and clears it after this row's next successful backup,
    /// which is the point the newest backup genuinely reflects current disk content again.
    /// </summary>
    public DateTime? RestoredArchiveTakenAt { get; set; }

    /// <summary>
    /// Identifies this row's most recently started status scan. The view model bumps it when it
    /// starts one and applies a scan's result only while the value it captured is still current,
    /// so a slow older scan cannot overwrite a newer one's answer. Not bound to any view.
    /// </summary>
    internal int ScanToken { get; set; }

    /// <summary>Drives the restore bar's visibility — a restore is in flight on this row.</summary>
    public bool IsRestoring => RestoreStep > 0;

    /// <summary>
    /// This row's save directory total, in bytes, set from
    /// <see cref="Services.BackupService.ComputeSaveDataSize"/> before this row's write starts
    /// — the determinate-vs-indeterminate decision is made before the first byte
    /// is written. Zero at idle. Public-setter exception (like <see cref="IsBusy"/>): a producer
    /// outside the row (<see cref="ViewModels.MainViewModel"/>) writes it directly.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLargeBackup))]
    public partial long BackupBytesTotal { get; set; }

    /// <summary>Cumulative bytes archived so far for this row's in-flight backup. Zero at idle.</summary>
    [ObservableProperty] public partial long BackupBytesWritten { get; set; }

    /// <summary>
    /// A starting judgment call, not a measured value: below this, the indeterminate ring
    /// stays (a determinate bar would flicker for well under a second on the common small-save
    /// case); at or above it, the determinate bar replaces the ring. Kept as a single constant
    /// precisely so it is cheap to change if real-world save sizes prove it wrong.
    /// </summary>
    private const long LargeBackupThresholdBytes = 50L * 1024 * 1024;

    /// <summary>Drives the determinate bar's visibility, mutually exclusive with the ring.</summary>
    public bool IsLargeBackup => BackupBytesTotal >= LargeBackupThresholdBytes;

    public GameRowViewModel(Game game)
    {
        Game = game;
        StatusText = Strings.Get("str_common_checking");
        FileCountText = "";
        SizeText = "";
        VerdictText = Strings.Get("str_common_checking");
        RestoreStepText = "";
    }

    public Game Game { get; }

    public string DisplayName => Game.DisplayName;

    /// <summary>
    /// Maps a scan result onto this row's bound properties, using the authored copy from
    /// <c>Strings.en.xaml</c>.
    /// </summary>
    public void ApplyStatus(SaveStatus status)
    {
        switch (status.Kind)
        {
            case SaveStatusKind.PathError:
                GameSeverity = RowSeverity.Error;
                StatusText = Strings.Get("str_game_pathError");
                FileCountText = "";
                SizeText = "";
                LastPlayedAt = null;
                break;

            case SaveStatusKind.NoSaves:
                GameSeverity = RowSeverity.Normal;
                StatusText = Strings.Get("str_game_noSaves");
                FileCountText = "0";
                SizeText = "";
                LastPlayedAt = null;
                break;

            case SaveStatusKind.Found:
                GameSeverity = status.Oversized ? RowSeverity.Warning : RowSeverity.Normal;
                FileCountText = status.FileCount.ToString();
                SizeText = SizeFormat.Describe(status.TotalBytes);
                LastPlayedAt = status.LastModified;
                StatusText = status.Oversized
                    ? Strings.Get("str_game_oversized")
                    : "";
                break;

            case SaveStatusKind.Checking:
            default:
                GameSeverity = RowSeverity.Normal;
                StatusText = Strings.Get("str_common_checking");
                FileCountText = "";
                SizeText = "";
                LastPlayedAt = null;
                break;
        }
    }

    /// <summary>
    /// Maps a save-vs-backup comparison onto this row's bound properties, using the authored copy
    /// from <c>Strings.en.xaml</c>. A second method
    /// beside <see cref="ApplyStatus"/> rather than a merge into it: the save side and the backup
    /// side are two independent axes on one row, each with its own producer.
    /// </summary>
    public void ApplyBackupComparison(BackupComparison comparison)
    {
        // Driven by backup-side facts only — never gated on the verdict's own kind, so a
        // player whose save folder is empty still sees that an archive exists.
        NewestBackupAt = comparison.NewestBackupTakenAt;
        ComparisonKind = comparison.Kind;

        switch (comparison.Kind)
        {
            case BackupComparisonKind.UpToDate:
                StatusSeverity = RowSeverity.Normal;
                VerdictText = Strings.Get("str_status_upToDate");
                break;

            case BackupComparisonKind.SavesChangedSinceBackup:
                StatusSeverity = RowSeverity.Warning;
                VerdictText = Strings.Format("str_status_changedSinceBackup", ElapsedFormat.Describe(comparison.Elapsed!.Value));
                break;

            case BackupComparisonKind.NeverBackedUp:
                // Save data with no backup at all is the state most at risk, so it carries the
                // same weight as saves that changed since their last one.
                StatusSeverity = RowSeverity.Warning;
                VerdictText = Strings.Get("str_status_neverBackedUp");
                break;

            case BackupComparisonKind.NoSaveData:
                StatusSeverity = RowSeverity.Normal;
                VerdictText = comparison.NewestBackupTakenAt is null
                    ? Strings.Get("str_status_noSaveDataYet")
                    : Strings.Get("str_status_noSaveDataRestorable");
                break;

            case BackupComparisonKind.BackupFolderNotAvailable:
                StatusSeverity = RowSeverity.Error;
                VerdictText = Strings.Get("str_status_backupFolderUnavailable");
                break;

            case BackupComparisonKind.Checking:
            default:
                StatusSeverity = RowSeverity.Normal;
                VerdictText = Strings.Get("str_common_checking");
                break;
        }
    }
}
