#nullable disable
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Core.Content;

/// <summary>
/// Track search for the fields scanned from <c>ui_track.json</c>. Plain text matches the folder id and a layout name.
/// Key names follow Content Manager. A query matches when any layout matches. Length is compared in kilometres and
/// width in metres. Pit boxes use the number in the text, and a missing pit box is not 2. A missing length compares as 0 km.
/// <c>kunos+</c> is true only when the stored author is exactly Kunos. The scanner clears that word, and the official-track list stays unread.
/// </summary>
public sealed class TrackSummaryTester : ITester<TrackSummary>
{
    public static readonly TrackSummaryTester Instance = new TrackSummaryTester();

    public string ParameterFromKey(string key)
    {
        switch (key) {
            case "id":
                return nameof(TrackSummary.Id);
            case "name":
                return nameof(TrackSummary.Name);
            case "layouts":
                return nameof(TrackSummary.Layouts);
            case "city":
                return nameof(TrackLayout.City);
            case "geotags":
                return nameof(TrackLayout.GeoTags);
            case "len":
            case "length":
                return nameof(TrackLayout.SpecsLength);
            case "width":
                return nameof(TrackLayout.SpecsWidth);
            case "pits":
            case "pitboxes":
                return nameof(TrackLayout.SpecsPitboxes);
            case "t":
            case "tag":
                return nameof(TrackLayout.Tags);
            case "desc":
            case "description":
                return nameof(TrackLayout.Description);
            case "c":
            case "country":
                return nameof(TrackLayout.Country);
            case "version":
                return nameof(TrackLayout.Version);
            case "url":
                return nameof(TrackLayout.Url);
            case "y":
            case "year":
                return nameof(TrackLayout.Year);
            case "a":
            case "author":
            case "k":
            case "kunos":
                return nameof(TrackLayout.Author);
            case null:
                return nameof(TrackSummary.Name);
            default:
                return null;
        }
    }

    public bool Test(TrackSummary track, string key, ITestEntry value)
    {
        if (key == "layouts") {
            return value.Test(track.Layouts.Count);
        }

        if (key == "id") {
            return value.Test(track.Id);
        }

        if (track.Layouts.Count == 0) {
            return key == null && (value.Test(track.Id) || value.Test(track.Name));
        }

        if (track.Layouts.Count == 1) {
            return TestLayout(track, track.Layouts[0], key, value);
        }

        foreach (var layout in track.Layouts) {
            if (TestLayout(track, layout, key, value)) {
                return true;
            }
        }

        return false;
    }

    private static bool TestLayout(TrackSummary track, TrackLayout layout, string key, ITestEntry value)
    {
        switch (key) {
            case null:
                return value.Test(track.Id) || value.Test(layout.Name);
            case "name":
                return value.Test(layout.Name);
            case "city":
                return value.Test(layout.City);
            case "geotags":
                return value.Test(layout.GeoTags?.ToString());
            case "len":
            case "length":
                value.Set(TrackDistanceQuery.Kilometers);
                return value.Test(TrackDistanceQuery.LengthKilometers(layout.SpecsLength));
            case "width":
                value.Set(TrackDistanceQuery.Meters);
                return value.Test(layout.SpecsWidth);
            case "pits":
            case "pitboxes":
                return value.Test(layout.SpecsPitboxes);
            case "t":
            case "tag":
                return layout.Tags != null && layout.Tags.Any(value.Test);
            case "desc":
            case "description":
                return value.Test(layout.Description);
            case "c":
            case "country":
                return value.Test(layout.Country);
            case "version":
                return value.Test(layout.Version);
            case "url":
                return value.Test(layout.Url);
            case "y":
            case "year":
                return layout.Year.HasValue && value.Test(layout.Year.Value);
            case "a":
            case "author":
                return value.Test(layout.Author);
            case "k":
            case "kunos":
                return value.Test(layout.Author == CarAuthors.Kunos);
            default:
                return false;
        }
    }
}
