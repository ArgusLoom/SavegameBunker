using SavegameBunker.Models;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises the Find Games window surface — counts, empty state, Details expansion and All/None —
/// entirely from synthetic <see cref="ScanResult"/>s in memory. Nothing here resolves a path or
/// touches the filesystem, matching the view model's own testability rule.
/// </summary>
public sealed class ScanWindowViewModelTests
{
    private static KnownGame MakeGame(string id, string displayName) =>
        new() { Id = id, DisplayName = displayName, SaveDirTemplate = "%APPDATA%/" + displayName };

    private static ScanHit MakeHit(string id, string displayName) =>
        new(MakeGame(id, displayName), @"C:\saves\" + id, FileCount: 3, TotalBytes: 1024);

    private static ScanResult MakeResult(int hitCount = 0, int sharedCount = 0, int alreadyKnownCount = 0)
    {
        var hits = new List<ScanHit>();
        for (var i = 0; i < hitCount; i++)
            hits.Add(MakeHit($"hit-{i}", $"Hit {i}"));

        var shared = new List<KnownGame>();
        for (var i = 0; i < sharedCount; i++)
            shared.Add(MakeGame($"shared-{i}", $"Shared {i}"));

        return new ScanResult(hits, shared, alreadyKnownCount);
    }

    [Fact]
    public void A_scan_with_zero_hits_reports_the_empty_state_and_disables_add()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 0));

        Assert.False(vm.HasHits);
        Assert.Contains("No known games with save data found", vm.HeaderText);
        Assert.Equal(0, vm.SelectedCount);
        Assert.Equal("Add (0)", vm.AddLabel);
        Assert.False(vm.CanAdd);
    }

    [Fact]
    public void A_scan_with_three_hits_starts_with_every_row_checked()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 3));

        Assert.True(vm.HasHits);
        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal("Add (3)", vm.AddLabel);
    }

    [Fact]
    public void Unchecking_every_row_by_hand_disables_add()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 2));

        foreach (var row in vm.Rows)
            row.IsSelected = false;

        Assert.False(vm.CanAdd);
        Assert.Equal("Add (0)", vm.AddLabel);
    }

    [Fact]
    public void Select_none_command_unchecks_every_row()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 3));

        vm.SelectNoneCommand.Execute(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.CanAdd);
        Assert.Empty(vm.BuildSelection());
    }

    [Fact]
    public void Select_all_command_after_select_none_rechecks_every_row()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 3));
        vm.SelectNoneCommand.Execute(null);

        vm.SelectAllCommand.Execute(null);

        Assert.Equal(vm.Rows.Count, vm.SelectedCount);
        Assert.True(vm.CanAdd);
    }

    [Fact]
    public void Select_all_command_on_an_already_fully_checked_list_changes_nothing()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 3));

        vm.SelectAllCommand.Execute(null);

        Assert.Equal(3, vm.SelectedCount);
    }

    [Fact]
    public void Fourteen_skipped_shared_entries_use_the_plural_wording()
    {
        var vm = new ScanWindowViewModel(MakeResult(sharedCount: 14));

        Assert.True(vm.HasSharedSkipped);
        Assert.Equal("14 titles skipped: shared folder", vm.SharedSkippedText);
    }

    [Fact]
    public void One_skipped_shared_entry_uses_the_singular_wording()
    {
        var vm = new ScanWindowViewModel(MakeResult(sharedCount: 1));

        Assert.Equal("1 title skipped: shared folder", vm.SharedSkippedText);
    }

    [Fact]
    public void Zero_skipped_shared_entries_render_an_empty_line()
    {
        var vm = new ScanWindowViewModel(MakeResult(sharedCount: 0));

        Assert.False(vm.HasSharedSkipped);
        Assert.Equal("", vm.SharedSkippedText);
    }

    [Fact]
    public void Six_already_known_skips_report_the_count()
    {
        var vm = new ScanWindowViewModel(MakeResult(alreadyKnownCount: 6));

        Assert.True(vm.HasAlreadyKnownSkipped);
        Assert.Equal("6 already in your games list", vm.AlreadyKnownSkippedText);
    }

    [Fact]
    public void Zero_already_known_skips_render_an_empty_line()
    {
        var vm = new ScanWindowViewModel(MakeResult(alreadyKnownCount: 0));

        Assert.False(vm.HasAlreadyKnownSkipped);
        Assert.Equal("", vm.AlreadyKnownSkippedText);
    }

    [Fact]
    public void Shared_titles_carries_the_display_names_in_scan_result_order()
    {
        var result = new ScanResult(
            new List<ScanHit>(),
            new List<KnownGame> { MakeGame("b", "Bravo"), MakeGame("a", "Alpha") },
            SkippedAlreadyKnown: 0);

        var vm = new ScanWindowViewModel(result);

        Assert.Equal(new[] { "Bravo", "Alpha" }, vm.SharedTitles);
    }

    [Fact]
    public void Toggle_details_command_flips_show_details_and_raises_a_change_notification()
    {
        var vm = new ScanWindowViewModel(MakeResult(sharedCount: 1));
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ToggleDetailsCommand.Execute(null);
        Assert.True(vm.ShowDetails);
        Assert.Contains(nameof(ScanWindowViewModel.ShowDetails), raised);

        vm.ToggleDetailsCommand.Execute(null);
        Assert.False(vm.ShowDetails);
    }

    [Fact]
    public void Show_details_starts_false()
    {
        var vm = new ScanWindowViewModel(MakeResult(hitCount: 1));

        Assert.False(vm.ShowDetails);
    }
}
