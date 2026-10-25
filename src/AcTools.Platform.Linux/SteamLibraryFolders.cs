using System.Text.RegularExpressions;

namespace AcTools.Platform.Linux;

/// <summary>
/// Reads Steam's libraryfolders.vdf. Supports the current "path" entries and the older numbered paths.
/// </summary>
public static partial class SteamLibraryFolders
{
    public static IReadOnlyList<string> ReadPaths(string vdf)
    {
        var paths = new List<string>();
        foreach (Match match in PathPattern().Matches(vdf)) {
            Add(paths, match.Groups[1].Value);
        }

        foreach (Match match in LegacyPattern().Matches(vdf)) {
            Add(paths, match.Groups[2].Value);
        }

        return paths;
    }

    private static void Add(List<string> paths, string raw)
    {
        var value = raw.Replace("\\\\", "\\", StringComparison.Ordinal);
        if (value.Length == 0 || (!value.Contains('/', StringComparison.Ordinal) && !value.Contains('\\', StringComparison.Ordinal))) {
            return;
        }

        if (paths.Contains(value, StringComparer.Ordinal)) {
            return;
        }

        paths.Add(value);
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex PathPattern();

    // Same-line only. A multiline \s match treats the next key as the previous value.
    [GeneratedRegex("\"(\\d+)\"[ \\t]+\"([^\"\\r\\n]+)\"")]
    private static partial Regex LegacyPattern();
}
