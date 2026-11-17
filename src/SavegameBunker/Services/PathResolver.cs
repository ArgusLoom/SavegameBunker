using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SavegameBunker.Services;

/// <summary>
/// Expands the path templates used in the catalog file.
/// </summary>
/// <remarks>
/// Handles the standard %VAR% syntax plus the pseudo-variables %DOCUMENTS% and %SAVEDGAMES%,
/// which map to the current user's Documents and Saved Games folders respectively. Both exist
/// because Windows lets each folder be relocated away from the user profile (OneDrive Known
/// Folder Move for Documents, a manual move for Saved Games), which makes a hardcoded
/// %USERPROFILE%\Documents or %USERPROFILE%\Saved Games wrong on a large share of machines.
/// %SAVEDGAMES% is resolved via <see cref="KnownFolders.SavedGames"/> because there is no
/// <see cref="Environment.SpecialFolder"/> member for it.
///
/// Forward slashes are accepted so catalog files can be hand-edited without doubling every
/// backslash — the one genuine hand-editing hazard JSON has for Windows paths.
///
/// A template that cannot be fully resolved is reported rather than turned into a path relative
/// to the application directory: an unexpanded %VAR% used to produce an absolute-looking path
/// pointing somewhere plausible and wrong.
/// </remarks>
public static class PathResolver
{
    // Matches a %...% pair, the only shape a variable token can take. A lone "%" that is not
    // paired with a closing one — a literal percent sign in a folder name, e.g. "C:\100% saves" —
    // never matches this and so is never mistaken for an unresolved variable.
    private static readonly Regex VariableToken = new("%[^%]+%", RegexOptions.Compiled);


    /// <param name="template">The raw template from the catalog file.</param>
    /// <param name="baseDir">
    /// Directory a relative template is resolved against — the catalog file's own folder, for
    /// <c>backupRoot</c>. Null means the template must expand to an absolute path.
    /// </param>
    /// <param name="path">The resolved absolute path; empty when resolution failed.</param>
    /// <param name="error">Why resolution failed; null on success.</param>
    public static bool TryResolve(string? template, string? baseDir, out string path, out string? error)
    {
        path = "";
        error = null;

        if (string.IsNullOrWhiteSpace(template))
        {
            error = "path is empty";
            return false;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var withDocuments = template.Replace("%DOCUMENTS%", documents, StringComparison.OrdinalIgnoreCase);

        // Substituted only when resolvable; otherwise the token is left in place so the
        // leftover-% guard below reports it as an unresolved variable rather than a special case.
        var savedGames = KnownFolders.SavedGames;
        var withSavedGames = savedGames is null
            ? withDocuments
            : withDocuments.Replace("%SAVEDGAMES%", savedGames, StringComparison.OrdinalIgnoreCase);

        // Only the five documented variables are expanded — deliberately not
        // Environment.ExpandEnvironmentVariables, which would silently expand any process
        // environment variable (%TEMP%, %PATH%, ...) that happens to appear in a template, none of
        // which are part of the catalog's documented contract.
        var expanded = VariableToken.Replace(withSavedGames, match => match.Value.ToUpperInvariant() switch
        {
            "%APPDATA%" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "%LOCALAPPDATA%" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "%USERPROFILE%" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            _ => match.Value,
        });

        // A %...% pair that survived substitution is an unresolved variable — left alone it would
        // become a relative path segment and resolve against the application directory. A lone "%"
        // that is not part of such a pair (a literal percent sign in a folder name) never matches
        // VariableToken and so never reaches here.
        var unresolved = VariableToken.Match(expanded);
        if (unresolved.Success)
        {
            error = $"unresolved variable {unresolved.Value} in \"{template}\"";
            return false;
        }

        expanded = expanded.Replace('/', Path.DirectorySeparatorChar);

        try
        {
            if (Path.IsPathRooted(expanded))
            {
                path = Path.GetFullPath(expanded);
            }
            else if (baseDir is not null)
            {
                path = Path.GetFullPath(Path.Combine(baseDir, expanded));
            }
            else
            {
                error = $"\"{template}\" is not an absolute path";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"\"{template}\" is not a usable path: {ex.Message}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts an absolute folder into the longest-matching %VAR% template — the reverse of
    /// <see cref="TryResolve"/>. Used when the user picks a folder in a shell dialog: storing a
    /// template instead of a machine-specific path keeps the catalog usable after Documents or
    /// AppData relocate (OneDrive Known Folder Move, a new machine, a new user account).
    /// </summary>
    /// <remarks>
    /// Selection is "longest resolved root wins" with no hand-ordered priority list on top of it
    /// — that length comparison is what makes %SAVEDGAMES% and %DOCUMENTS% beat %USERPROFILE% on
    /// a standard profile, where all three normally live one inside the other. A folder
    /// under none of the candidate roots is not an error — it is returned as a plain absolute
    /// path with forward slashes. A relocated Documents or Saved Games folder needs no special
    /// case either: once it moves outside the profile, %USERPROFILE% no longer prefixes it at
    /// all, so only the relocated candidate still matches.
    /// </remarks>
    public static string ToTemplate(string absolutePath)
    {
        var candidates = new (string Token, string? Value)[]
        {
            ("%SAVEDGAMES%", KnownFolders.SavedGames),
            ("%DOCUMENTS%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            ("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        };

        var target = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar);

        var best = candidates
            .Where(c => !string.IsNullOrEmpty(c.Value))
            .Select(c => (c.Token, Value: Path.GetFullPath(c.Value!).TrimEnd(Path.DirectorySeparatorChar)))
            .Where(c => target.Equals(c.Value, StringComparison.OrdinalIgnoreCase)
                     || target.StartsWith(c.Value + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Value.Length)
            .FirstOrDefault();

        if (best.Token is null)
            return target.Replace(Path.DirectorySeparatorChar, '/');

        var remainder = target.Length == best.Value.Length
            ? ""
            : target[(best.Value.Length + 1)..];

        return remainder.Length == 0
            ? best.Token
            : $"{best.Token}/{remainder.Replace(Path.DirectorySeparatorChar, '/')}";
    }
}
