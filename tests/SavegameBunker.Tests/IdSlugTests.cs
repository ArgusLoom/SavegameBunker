using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Pure-function tests for id derivation and the a-z0-9- safety rule — no filesystem, no fixture.
/// </summary>
public sealed class IdSlugTests
{
    [Theory]
    [InlineData("The Witcher 3: Wild Hunt", "the-witcher-3-wild-hunt")]
    [InlineData("  Cyberpunk 2077  ", "cyberpunk-2077")]
    [InlineData("Ōkami HD", "kami-hd")]
    [InlineData("!!!", "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("A -- B", "a-b")]
    public void Derivation_produces_the_expected_id(string displayName, string expected)
    {
        Assert.Equal(expected, IdSlug.Derive(displayName));
    }

    [Theory]
    [InlineData("The Witcher 3: Wild Hunt")]
    [InlineData("  Cyberpunk 2077  ")]
    [InlineData("Ōkami HD")]
    [InlineData("!!!")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A -- B")]
    public void Derivation_is_idempotent_because_a_derived_id_is_valid_input_to_itself(string displayName)
    {
        var once = IdSlug.Derive(displayName);
        var twice = IdSlug.Derive(once);

        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("witcher3")]
    [InlineData("a")]
    [InlineData("a-b")]
    [InlineData("2077")]
    public void Well_formed_id_is_safe(string id)
    {
        Assert.True(IdSlug.IsSafe(id));
    }

    [Theory]
    [InlineData("Witcher3")]
    [InlineData("../escape")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    [InlineData("wîtcher")]
    public void Malformed_id_is_rejected_because_it_names_a_backup_folder(string id)
    {
        Assert.False(IdSlug.IsSafe(id));
    }

    [Fact]
    public void Empty_id_is_not_safe_because_it_names_no_folder_at_all()
    {
        Assert.False(IdSlug.IsSafe(""));
    }

    [Theory]
    [InlineData("con")]
    [InlineData("prn")]
    [InlineData("aux")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("com9")]
    [InlineData("lpt1")]
    [InlineData("lpt9")]
    public void Windows_reserved_device_name_is_not_safe_even_though_it_passes_the_character_check(string id)
    {
        // A folder named "con" or "nul" cannot be created or addressed normally through Explorer or
        // most Win32 APIs, whatever backupRoot it sits under — and every one of these names is
        // otherwise well-formed by the a-z0-9- rule alone.
        Assert.False(IdSlug.IsSafe(id));
    }

    [Theory]
    [InlineData("console")]
    [InlineData("nulled")]
    [InlineData("com10")]
    [InlineData("con-fig")]
    public void Id_merely_containing_a_reserved_name_as_a_substring_is_still_safe(string id)
    {
        // The reserved-name check is an exact match on the whole id, not a substring scan — only
        // the literal device name itself is unsafe.
        Assert.True(IdSlug.IsSafe(id));
    }
}

/// <summary>
/// <see cref="SizeFormat"/> is a one-method class — its own file is enough, but a third test file
/// for it would be excess; it lives alongside <see cref="IdSlugTests"/>, the sibling pure-function
/// service these tests also pin.
/// </summary>
public sealed class SizeFormatTests
{
    [Theory]
    [InlineData(0, "0.0 KB")]
    [InlineData(500, "0.5 KB")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(3_250_586, "3.1 MB")]
    [InlineData(1024L * 1024, "1.0 MB")]
    [InlineData(1024L * 1024 * 1024, "1.0 GB")]
    public void Describes_bytes_in_the_appropriate_unit_to_one_decimal_place(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormat.Describe(bytes));
    }
}
