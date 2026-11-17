using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace SavegameBunker.Services;

/// <summary>
/// The single lookup seam every view model and every XAML <c>DynamicResource</c> binding uses to
/// reach an authored sentence in <c>Themes/Strings.en.xaml</c>. A missing key throws
/// with the key name in the message — a silently returned empty string is exactly the failure
/// mode this mechanism exists to make impossible.
/// </summary>
/// <remarks>
/// Loads the dictionary exactly once, into a static field, trying two routes in order:
/// <list type="number">
/// <item>The pack URI for this assembly's own <c>Themes/Strings.en.xaml</c> — what the running
/// app uses, resolved the same way <c>App.xaml</c>'s own merged dictionaries are.</item>
/// <item>If route 1 fails (no <see cref="Application"/> instance exists, as in a plain xUnit
/// host), the identical file is also carried as an <c>EmbeddedResource</c>
/// (<c>SavegameBunker.csproj</c>) and parsed with <see cref="XamlReader"/> over the embedded stream —
/// the same shape <c>KnownGamesCatalog</c> already uses for <c>Data/known-games.json</c>.</item>
/// </list>
/// Both routes resolve to the same file, so there is exactly one authored copy either way.
/// </remarks>
public static class Strings
{
    private const string PackUri = "pack://application:,,,/SavegameBunker;component/Themes/Strings.en.xaml";
    private const string EmbeddedResourceName = "SavegameBunker.Themes.Strings.en.xaml";

    /// <summary>
    /// Every authored string, materialised once into a plain dictionary.
    /// </summary>
    /// <remarks>
    /// This is deliberately NOT a live <see cref="ResourceDictionary"/>. A dictionary loaded from a
    /// <c>Source</c> URI holds its values as deferred BAML and deserialises each one on first
    /// access — and that deserialisation is not thread-safe. The status sweep applies its results
    /// through <c>Parallel.ForEachAsync</c>, so four threads hit these lookups at once on a cold
    /// dictionary; the observed failures were a null returned for a key that plainly exists
    /// ("No string is authored for key 'str_game_noSaves'") and a torn read surfacing as
    /// "Unexpected BAML record". Reading every entry once, up front, removes the deferred load
    /// entirely: afterwards this is an ordinary read-only dictionary and concurrent reads are safe.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> Dictionary = Load();

    /// <summary>
    /// Forces the one-time load on the calling thread. Call from <c>App.OnStartup</c>, on the UI
    /// thread, before anything can look a string up from a worker — resolving a <c>pack://</c> URI
    /// and parsing XAML both want to happen there, not on whichever pool thread happened to ask
    /// first.
    /// </summary>
    public static void Preload() => _ = Dictionary.Count;

    /// <summary>Looks up an authored, non-composed sentence. Throws when the key is not authored.</summary>
    public static string Get(string key)
    {
        if (Dictionary.TryGetValue(key, out var value))
            return value;

        throw new InvalidOperationException($"No string is authored for key '{key}' in Strings.en.xaml.");
    }

    /// <summary>
    /// Looks up an authored format string and fills its <c>{0}</c>-style placeholders. The
    /// view model keeps ownership of the sentence — only the wording lives in the resource file.
    /// </summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static IReadOnlyDictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        Materialise(LoadDictionary(), map);
        return map;
    }

    /// <summary>
    /// Copies every string entry out of <paramref name="source"/>, forcing the deferred BAML load
    /// to completion here — once, on one thread — rather than lazily under concurrent readers.
    /// Merged dictionaries are walked first so a later entry with the same key still wins, which
    /// is the precedence a live ResourceDictionary would apply.
    /// </summary>
    private static void Materialise(ResourceDictionary source, Dictionary<string, string> into)
    {
        foreach (var merged in source.MergedDictionaries)
            Materialise(merged, into);

        foreach (DictionaryEntry entry in source)
        {
            if (entry.Key is string key && entry.Value is string value)
                into[key] = value;
        }
    }

    private static ResourceDictionary LoadDictionary()
    {
        try
        {
            return new ResourceDictionary { Source = new Uri(PackUri, UriKind.Absolute) };
        }
        catch
        {
            // Route 1 needs a running WPF Application to resolve pack:// URIs, which a plain
            // xUnit host never constructs. Fall back to the embedded copy of the same file.
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);
            if (stream is null)
                throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' was not found.");

            return (ResourceDictionary)XamlReader.Load(stream);
        }
    }
}
