using SavegameBunker.Services;

namespace SavegameBunker.Tests;

public sealed class WikiUrlTests
{
    [Fact]
    public void A_pcgamingwiki_https_url_is_allowed()
    {
        Assert.True(WikiUrl.IsAllowed("https://www.pcgamingwiki.com/w/index.php?search=Celeste"));
    }

    [Fact]
    public void The_allowlist_is_case_insensitive_on_the_scheme_and_host()
    {
        Assert.True(WikiUrl.IsAllowed("HTTPS://WWW.PCGAMINGWIKI.COM/wiki/Celeste"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://www.pcgamingwiki.com/w/index.php?search=Celeste")]
    [InlineData("https://example.com/")]
    [InlineData("https://pcgamingwiki.com/w/index.php?search=Celeste")]
    [InlineData("https://evil.example/https://www.pcgamingwiki.com/")]
    [InlineData("file:///C:/Games/save.dat")]
    public void Anything_else_is_refused(string? url)
    {
        Assert.False(WikiUrl.IsAllowed(url));
    }

    [Fact]
    public void Search_builds_the_escaped_index_php_url()
    {
        Assert.Equal(
            "https://www.pcgamingwiki.com/w/index.php?search=Hollow%20Knight",
            WikiUrl.Search("Hollow Knight"));
    }
}
