using System.IO;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Pins the <c>MoveNext</c> guard: a listing that was opened fine but fails partway through (a
/// share dropping, a volume pulled) must end that one listing and be reported, not unwind the walk.
/// </summary>
public sealed class DirectoryWalkTests
{
    private static IEnumerable<string> ListingThatFailsAfter(string first, Exception fault)
    {
        yield return first;
        throw fault;
    }

    [Fact]
    public void A_listing_that_fails_midway_yields_what_it_had_and_reports_the_fault_once()
    {
        var faults = 0;

        var items = DirectoryWalk.GuardListing(
            ListingThatFailsAfter("a.dat", new IOException("share dropped")), () => faults++).ToList();

        Assert.Equal(["a.dat"], items);
        Assert.Equal(1, faults);
    }

    [Fact]
    public void A_healthy_listing_passes_through_without_reporting_a_fault()
    {
        var faults = 0;

        var items = DirectoryWalk.GuardListing(["a.dat", "b.dat"], () => faults++).ToList();

        Assert.Equal(["a.dat", "b.dat"], items);
        Assert.Equal(0, faults);
    }

    [Fact]
    public void A_fault_that_is_not_a_filesystem_fault_still_propagates()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DirectoryWalk.GuardListing(
                ListingThatFailsAfter("a.dat", new InvalidOperationException("bug")), () => { }).ToList());
    }
}
