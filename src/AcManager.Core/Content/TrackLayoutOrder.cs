using System.Text.RegularExpressions;

namespace AcManager.Core.Content;

/// <summary>
/// One layout folder under a track's <c>ui</c> directory.
/// </summary>
public readonly record struct TrackLayoutInfo(string Id, string? Name, double? Priority, string? Version = null);

/// <summary>
/// Picks the main track layout the way <c>TrackObject.GetLayouts</c> does when the
/// official Kunos layout list is not loaded: a name heuristic, then priority.
/// </summary>
public static class TrackLayoutOrder
{
    public static IReadOnlyList<TrackLayout> Arrange(
        string trackId,
        bool rootPresent,
        string rootName,
        IReadOnlyList<TrackLayoutInfo> layouts,
        string? rootVersion = null)
    {
        if (rootPresent) {
            var extras = layouts
                .OrderByDescending(layout => layout.Priority ?? 0d)
                .ThenBy(layout => layout.Id, StringComparer.Ordinal)
                .Select(Display)
                .ToList();
            extras.Insert(0, new TrackLayout("", rootName, rootVersion));
            return extras;
        }

        if (layouts.Count == 0) {
            return [new TrackLayout("", trackId)];
        }

        var mainId = Preferred(trackId, layouts);
        var rest = layouts
            .Where(layout => !string.Equals(layout.Id, mainId, StringComparison.Ordinal))
            .OrderByDescending(layout => layout.Priority ?? 0d)
            .ThenBy(layout => layout.Id, StringComparer.Ordinal)
            .Select(Display);
        var main = layouts.First(layout => string.Equals(layout.Id, mainId, StringComparison.Ordinal));
        return rest.Prepend(Display(main)).ToList();
    }

    /// <summary>
    /// Shared prefix of the layout names, from <c>TrackObject.FindNameForMultiLayoutMode</c>.
    /// </summary>
    public static string? CommonName(IReadOnlyList<string?> names)
    {
        if (names.Count == 0) {
            return null;
        }

        var baseName = names[0];
        if (baseName == null) {
            return null;
        }

        var lengths = names.Where(name => name != null).Select(name => name!.Length).ToList();
        if (lengths.Count == 0) {
            return null;
        }

        for (var i = lengths.Min(); i > 2; i--) {
            var result = baseName.Substring(0, i);
            if (names.Skip(1).Any(name => name?.Substring(0, i) != result)) {
                continue;
            }

            result = result.Trim();
            if (result.Length > 2 && (result.EndsWith('-') || result.EndsWith('—') || result.EndsWith('/') || result.EndsWith('('))) {
                result = result[..^1].Trim();
            }

            return result;
        }

        return null;
    }

    private static TrackLayout Display(TrackLayoutInfo layout) =>
        new(layout.Id, string.IsNullOrWhiteSpace(layout.Name) ? layout.Id : layout.Name, layout.Version);

    private static string Preferred(string trackId, IReadOnlyList<TrackLayoutInfo> layouts)
    {
        string? preferred = null;
        var best = double.MinValue;
        var ordered = layouts.OrderBy(layout => layout.Id, StringComparer.Ordinal);
        foreach (var layout in ordered) {
            var score = Score(trackId, layout);
            if (best < score) {
                best = score;
                preferred = layout.Id;
            }
        }

        return preferred ?? layouts[0].Id;
    }

    private static double Score(string trackId, TrackLayoutInfo layout)
    {
        var layoutId = layout.Id.ToLowerInvariant();
        var name = (string.IsNullOrWhiteSpace(layout.Name) ? layout.Id : layout.Name).ToLowerInvariant();
        var distance = Levenshtein(name, trackId.ToLowerInvariant());
        double priority = Math.Clamp(100 - distance * 10, 0, 100) - name.Length;
        priority -= NonDefaultLayout.Matches(layoutId).Sum(GetWeight) * 10;
        priority += PreferredLayout.Matches(layoutId).Sum(GetWeight) * 10;
        priority -= NonDefaultLayout.Matches(name).Sum(GetWeight) * 100;
        priority += PreferredLayout.Matches(name).Sum(GetWeight) * 100;
        priority += layout.Priority * 1e8 ?? 0;
        return priority;
    }

    private static int GetWeight(Match match)
    {
        if (match.Value is "full" or "drift" or "normal" or "pursuit") {
            return 10;
        }

        if (match.Value is "international" or "circuit") {
            return 2;
        }

        return match.Length;
    }

    private static int Levenshtein(string source, string target)
    {
        if (source.Length == 0) {
            return target.Length;
        }

        if (target.Length == 0) {
            return source.Length;
        }

        var rows = source.Length + 1;
        var columns = target.Length + 1;
        var table = new int[rows, columns];
        for (var i = 0; i < rows; table[i, 0] = i++) {
        }

        for (var j = 1; j < columns; table[0, j] = j++) {
        }

        for (var i = 1; i < rows; i++) {
            for (var j = 1; j < columns; j++) {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                table[i, j] = Math.Min(
                    Math.Min(table[i - 1, j] + 1, table[i, j - 1] + 1),
                    table[i - 1, j - 1] + cost);
            }
        }

        return table[source.Length, target.Length];
    }

    // Copied from TrackObject. In this ordinary string, \b is a backspace, the same as the Windows source.
    private static readonly Regex NonDefaultLayout = new(
        "downhill|drift|fall|freeroam|grid|mini|no ?chicane|osrw|oval|pursuit|rev(?:\b|erse)?",
        RegexOptions.Compiled);

    private static readonly Regex PreferredLayout = new(
        "circuit|international|full|gp|grand?|hill ?climb|normal|standar[dt]?|uphill",
        RegexOptions.Compiled);
}
