using System.Text.Json;

namespace AcManager.Core.Content;

/// <summary>
/// Official track and layout ids from gro-ove/actools-data <c>Miscellaneous/KunosContent.json</c>.
/// <c>TrackObjectBase.TestIfKunos</c> checks the track id for the root layout and
/// <c>track/layout</c> for a layout folder. A <c>ks_</c> prefix is not enough.
/// Showrooms, drivers, and fonts in the same file stay unread.
/// </summary>
public static class OfficialTracks
{
    private static readonly Lazy<OfficialTrackList> Loaded = new(Load);

    public static bool IsOfficial(string trackId, string layoutId) => Loaded.Value.IsOfficial(trackId, layoutId);

    private static OfficialTrackList Load() => OfficialTrackList.FromJson(EmbeddedData.Read("KunosContent.json"));
}

/// <summary>Parsed <c>tracks</c> and <c>layouts</c> lists. An empty or unreadable file lists nothing.</summary>
public sealed class OfficialTrackList
{
    private readonly HashSet<string> _tracks;
    private readonly HashSet<string> _layouts;

    private OfficialTrackList(HashSet<string> tracks, HashSet<string> layouts)
    {
        _tracks = tracks;
        _layouts = layouts;
    }

    public static OfficialTrackList FromJson(string json)
    {
        var tracks = new HashSet<string>(StringComparer.Ordinal);
        var layouts = new HashSet<string>(StringComparer.Ordinal);
        if (json.Length == 0) {
            return new OfficialTrackList(tracks, layouts);
        }

        try {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) {
                return new OfficialTrackList(tracks, layouts);
            }

            if (root.TryGetProperty("tracks", out var trackIds) && trackIds.ValueKind == JsonValueKind.Array) {
                foreach (var item in trackIds.EnumerateArray()) {
                    if (item.ValueKind == JsonValueKind.String) {
                        var id = item.GetString();
                        if (id != null) {
                            tracks.Add(id);
                        }
                    }
                }
            }

            if (root.TryGetProperty("layouts", out var layoutObject) && layoutObject.ValueKind == JsonValueKind.Object) {
                foreach (var track in layoutObject.EnumerateObject()) {
                    if (track.Value.ValueKind != JsonValueKind.Array) {
                        continue;
                    }

                    foreach (var layout in track.Value.EnumerateArray()) {
                        if (layout.ValueKind != JsonValueKind.String) {
                            continue;
                        }

                        var id = layout.GetString();
                        if (id != null) {
                            layouts.Add(track.Name + "/" + id);
                        }
                    }
                }
            }
        } catch (JsonException) {
            return new OfficialTrackList(tracks, layouts);
        }

        return new OfficialTrackList(tracks, layouts);
    }

    public bool IsOfficial(string trackId, string layoutId)
    {
        if (layoutId.Length == 0) {
            return _tracks.Contains(trackId);
        }

        return _layouts.Contains(trackId + "/" + layoutId);
    }
}
