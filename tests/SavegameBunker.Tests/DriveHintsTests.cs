using System.IO;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Pins <see cref="DriveHints.ChooseStartFolder"/>'s selection matrix and
/// <see cref="DriveHints.SuggestBackupStartFolder"/>'s never-throw contract on the real machine.
/// </summary>
public sealed class DriveHintsTests
{
    private const string SystemRoot = @"C:\";

    [Fact]
    public void No_qualifying_volume_yields_null()
    {
        var result = DriveHints.ChooseStartFolder(
            Array.Empty<(string Root, DriveType Type, bool IsReady)>(), SystemRoot);

        Assert.Null(result);
    }

    [Fact]
    public void A_single_ready_removable_volume_is_chosen()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"F:\", DriveType.Removable, true) }, SystemRoot);

        Assert.Equal(@"F:\", result);
    }

    [Fact]
    public void The_system_volume_is_never_chosen()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"C:\", DriveType.Fixed, true) }, SystemRoot);

        Assert.Null(result);
    }

    [Fact]
    public void A_non_system_fixed_volume_is_chosen_because_external_SSDs_report_as_fixed()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"C:\", DriveType.Fixed, true), (@"E:\", DriveType.Fixed, true) }, SystemRoot);

        Assert.Equal(@"E:\", result);
    }

    [Fact]
    public void Removable_wins_over_a_non_system_fixed_volume()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"E:\", DriveType.Fixed, true), (@"F:\", DriveType.Removable, true) }, SystemRoot);

        Assert.Equal(@"F:\", result);
    }

    [Fact]
    public void A_volume_that_is_not_ready_is_skipped()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"F:\", DriveType.Removable, false), (@"E:\", DriveType.Fixed, true) }, SystemRoot);

        Assert.Equal(@"E:\", result);
    }

    [Fact]
    public void A_network_volume_is_never_chosen()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"Z:\", DriveType.Network, true) }, SystemRoot);

        Assert.Null(result);
    }

    [Fact]
    public void Several_removable_volumes_resolve_to_the_lowest_root()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"G:\", DriveType.Removable, true), (@"F:\", DriveType.Removable, true) }, SystemRoot);

        Assert.Equal(@"F:\", result);
    }

    [Fact]
    public void The_system_root_comparison_ignores_drive_letter_case()
    {
        var result = DriveHints.ChooseStartFolder(
            new[] { (@"c:\", DriveType.Fixed, true) }, SystemRoot);

        Assert.Null(result);
    }

    [Fact]
    public void Enumerating_the_real_machine_never_throws_and_never_returns_the_system_volume()
    {
        var result = DriveHints.SuggestBackupStartFolder();

        if (result is not null)
        {
            Assert.True(Path.IsPathRooted(result));
            Assert.True(Directory.Exists(result));
            Assert.NotEqual(
                Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
                result,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
