using AcTools.DataFile;

namespace AcManager.Core.Content;

/// <summary>
/// Latitude and longitude from gro-ove/actools-data <c>Miscellaneous/Track Params.ini</c>.
/// <c>TrackObjectBase.LoadData</c> uses the track folder id when the JSON geotags are missing or invalid.
/// </summary>
public static class TrackCoordinates
{
    private static readonly Lazy<IReadOnlyDictionary<string, GeoTags>> Loaded = new(Load);

    public static GeoTags? ForTrack(string trackId) => Loaded.Value.GetValueOrDefault(trackId);

    private static IReadOnlyDictionary<string, GeoTags> Load()
    {
        var result = new Dictionary<string, GeoTags>(StringComparer.Ordinal);
        var text = EmbeddedData.Read("Track Params.ini");
        if (text.Length == 0) {
            return result;
        }

        foreach (var pair in IniFile.Parse(text)) {
            var section = pair.Value;
            if (section == null
                || !section.ContainsKey("LATITUDE")
                || !section.ContainsKey("LONGITUDE")) {
                continue;
            }

            result[pair.Key] = new GeoTags(
                section.GetDouble("LATITUDE", 0d),
                section.GetDouble("LONGITUDE", 0d));
        }

        return result;
    }
}
