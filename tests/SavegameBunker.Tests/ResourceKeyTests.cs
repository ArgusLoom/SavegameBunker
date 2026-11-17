using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SavegameBunker.Tests;

/// <summary>
/// Guards the one resource-reference mistake that fails silently.
/// </summary>
/// <remarks>
/// A <c>{DynamicResource Foo}</c> that resolves to nothing does not throw and does not warn — the
/// property simply keeps its default value. That is how <c>PaletteRedBrush</c> shipped referenced
/// but never defined: every error-severity row rendered in the ordinary foreground colour, so a
/// failed row looked exactly like a healthy one. Found by hand during a manual
/// walkthrough, not by the suite.
///
/// Scope is deliberately narrow: only the palette keys SavegameBunker owns. Keys supplied by WPF-UI
/// (<c>BodyTextBlockStyle</c>, <c>TextFillColorPrimaryBrush</c>, …) live in that library's own
/// dictionaries and are not this project's to define, so checking them would need an allowlist to
/// keep in sync — a maintenance burden that would itself rot.
/// </remarks>
public class ResourceKeyTests
{
    private static readonly string SourceDir = FindSourceDir();

    private static string FindSourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SavegameBunker")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "SavegameBunker");
    }

    [Fact]
    public void Every_SavegameBunker_owned_palette_resource_referenced_in_XAML_is_also_defined()
    {
        var xamlFiles = Directory.GetFiles(SourceDir, "*.xaml", SearchOption.AllDirectories);

        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in xamlFiles)
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"x:Key=""([^""]+)"""))
                defined.Add(m.Groups[1].Value);
        }

        // The keys this project owns rather than inherits: our palette names.
        var ownedPrefixes = new[] { "Palette", "SavegameBunker" };

        var unresolved = new List<string>();
        foreach (var file in xamlFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_.]+)\}"))
            {
                var key = m.Groups[1].Value;
                if (!ownedPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                    continue;

                if (!defined.Contains(key))
                    unresolved.Add($"{key} (referenced in {Path.GetFileName(file)})");
            }
        }

        Assert.Empty(unresolved.Distinct());
    }

    [Fact]
    public void Both_severity_colours_have_a_brush_to_go_with_them()
    {
        var appXaml = File.ReadAllText(Path.Combine(SourceDir, "App.xaml"));

        // A Color on its own cannot be assigned to a Foreground; every severity colour needs the
        // matching SolidColorBrush or the trigger that uses it does nothing at all.
        Assert.Contains(@"x:Key=""PaletteRedBrush""", appXaml);
        Assert.Contains(@"x:Key=""SavegameBunkerCautionBrush""", appXaml);
    }
}
