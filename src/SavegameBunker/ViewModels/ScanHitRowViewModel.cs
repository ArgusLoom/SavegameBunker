using CommunityToolkit.Mvvm.ComponentModel;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// One row in the Find Games window, wrapping a <see cref="ScanHit"/> with the mutable selection
/// state the checkbox binds to.
/// </summary>
/// <remarks>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// </remarks>
public sealed partial class ScanHitRowViewModel : CommunityToolkitObservableObject
{
    public ScanHitRowViewModel(ScanHit hit)
    {
        Hit = hit;
        // Everything is preselected — the user unchecks what they do not want, rather than
        // hunting through 17+ rows to check the ones they do.
        IsSelected = true;
    }

    public ScanHit Hit { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string DisplayName => Hit.Game.DisplayName;

    public string Id => Hit.Game.Id;

    public string ResolvedPath => Hit.ResolvedPath;

    public string FileCountText => Hit.FileCount == 1
        ? Strings.Get("str_scan_hitFileCountOne")
        : Strings.Format("str_scan_hitFileCount", Hit.FileCount);

    public string SizeText => SizeFormat.Describe(Hit.TotalBytes);
}
