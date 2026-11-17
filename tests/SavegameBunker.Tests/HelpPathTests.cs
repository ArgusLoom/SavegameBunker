using System.IO;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

public sealed class HelpPathTests : IDisposable
{
    private readonly string _dir;

    public HelpPathTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* leftover must not fail the run */ }
    }

    private void WriteGuide(string language)
    {
        var folder = Path.Combine(_dir, "help", language);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "index.html"), "<html></html>");
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    public void LanguageFor_maps_only_German_specially(string uiLanguage, string expected)
    {
        Assert.Equal(expected, HelpPath.LanguageFor(uiLanguage));
    }

    [Fact]
    public void Resolve_prefers_the_site_matching_the_ui_language()
    {
        WriteGuide("en");
        WriteGuide("de");

        var resolved = HelpPath.Resolve(_dir, "de");

        Assert.Equal(Path.Combine(_dir, "help", "de", "index.html"), resolved);
    }

    [Fact]
    public void Resolve_sends_every_non_German_language_to_the_English_site()
    {
        WriteGuide("en");
        WriteGuide("de");

        var resolved = HelpPath.Resolve(_dir, "fr");

        Assert.Equal(Path.Combine(_dir, "help", "en", "index.html"), resolved);
    }

    [Fact]
    public void Resolve_falls_back_to_English_when_only_it_is_shipped()
    {
        WriteGuide("en");

        var resolved = HelpPath.Resolve(_dir, "de");

        Assert.Equal(Path.Combine(_dir, "help", "en", "index.html"), resolved);
    }

    [Fact]
    public void Resolve_returns_null_when_the_whole_guide_folder_is_missing()
    {
        Assert.Null(HelpPath.Resolve(_dir, "de"));
        Assert.Null(HelpPath.Resolve(_dir, "en"));
    }

    [Fact]
    public void Resolve_returns_null_when_only_the_preferred_site_exists_but_English_was_asked()
    {
        // English asked and English missing: there is no fallback below English.
        WriteGuide("de");

        Assert.Null(HelpPath.Resolve(_dir, "en"));
    }

    [Fact]
    public void Resolve_returns_null_when_the_preferred_site_lacks_index_html()
    {
        // help/de exists but carries no index.html — the fallback must still fire.
        Directory.CreateDirectory(Path.Combine(_dir, "help", "de"));
        WriteGuide("en");

        var resolved = HelpPath.Resolve(_dir, "de");

        Assert.Equal(Path.Combine(_dir, "help", "en", "index.html"), resolved);
    }
}
