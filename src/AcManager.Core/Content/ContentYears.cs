using System.Text.Json;

namespace AcManager.Core.Content;

/// <summary>
/// Car and track years from gro-ove/actools-data <c>Miscellaneous/Years.json</c>.
/// <c>CarObject.LoadYear</c> and <c>TrackObjectBase.LoadYear</c> use the folder id.
/// Showroom years stay unread.
/// </summary>
public static class ContentYears
{
    private static readonly Lazy<Lists> Loaded = new(Load);

    public static int? ForCar(string id) => Find(Loaded.Value.Cars, id);

    public static int? ForTrack(string id) => Find(Loaded.Value.Tracks, id);

    private static int? Find(Dictionary<string, int> years, string id)
    {
        return years.TryGetValue(id, out var year) ? year : null;
    }

    private static Lists Load()
    {
        var cars = new Dictionary<string, int>(StringComparer.Ordinal);
        var tracks = new Dictionary<string, int>(StringComparer.Ordinal);
        var text = EmbeddedData.Read("Years.json");
        if (text.Length == 0) {
            return new Lists(cars, tracks);
        }

        try {
            using var document = JsonDocument.Parse(text);
            ReadSection(document.RootElement, "cars", cars);
            ReadSection(document.RootElement, "tracks", tracks);
        } catch (JsonException) {
            return new Lists(cars, tracks);
        }

        return new Lists(cars, tracks);
    }

    private static void ReadSection(JsonElement root, string name, Dictionary<string, int> years)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(name, out var section)
            || section.ValueKind != JsonValueKind.Object) {
            return;
        }

        foreach (var property in section.EnumerateObject()) {
            if (property.Value.TryGetInt32(out var year)) {
                years[property.Name] = year;
            }
        }
    }

    private sealed record Lists(
        Dictionary<string, int> Cars,
        Dictionary<string, int> Tracks);
}
