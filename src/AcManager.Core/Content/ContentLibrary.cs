using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcTools.Core.Data;

namespace AcManager.Core.Content;

public sealed record CarSkin(string Id, string Name, string? PreviewPath);

/// <summary>
/// Content Manager's author name for an official car. A JSON author of this value is ignored
/// unless <see cref="OfficialCarIds"/> lists the car.
/// </summary>
public static class CarAuthors
{
    public const string Kunos = "Kunos";
}

public sealed record CarSummary(
    string Id,
    string Name,
    string? Brand,
    string? ClassName,
    IReadOnlyList<CarSkin> Skins,
    IReadOnlyList<string>? Tags = null,
    string? Country = null,
    int? Year = null,
    string? ParentId = null,
    string? Author = null,
    string? SpecsBhp = null,
    string? SpecsTorque = null,
    string? SpecsWeight = null,
    string? SpecsTopSpeed = null,
    string? SpecsAcceleration = null,
    string? SpecsPwRatio = null)
{
    public string Subtitle
    {
        get {
            if (string.IsNullOrWhiteSpace(Brand)) {
                return Id;
            }

            return Brand + "  ·  " + Id;
        }
    }

    public CarSkin? DefaultSkin => Skins.Count == 0 ? null : Skins[0];
}

public sealed record TrackLayout(string Id, string Name);

public sealed record TrackSummary(string Id, string Name, IReadOnlyList<TrackLayout> Layouts)
{
    public string Subtitle => Layouts.Count <= 1 ? Id : Id + "  ·  " + Layouts.Count.ToString(CultureInfo.InvariantCulture) + " layouts";
}

public sealed record WeatherSummary(string Id, string Name);

public static class ContentLibrary
{
    private static readonly string[] PreviewNames = ["preview.jpg", "preview.png", "preview.jpeg"];

    public static IReadOnlyList<CarSummary> ScanCars(string? carsDirectory, IReadOnlySet<string>? officialCarIds = null)
    {
        if (string.IsNullOrWhiteSpace(carsDirectory) || !Directory.Exists(carsDirectory)) {
            return [];
        }

        var cars = new List<CarSummary>();
        foreach (var directory in Directory.GetDirectories(carsDirectory)) {
            if (!IsListedCar(directory)) {
                continue;
            }

            var id = Path.GetFileName(directory);
            var json = ReadUiObject(Path.Combine(directory, "ui", "ui_car.json"));
            var jsonName = StringOf(json, "name");
            var name = jsonName ?? id;
            var yearName = jsonName?.Trim();
            cars.Add(new CarSummary(
                id,
                name,
                StringOf(json, "brand"),
                StringOf(json, "class"),
                ScanSkins(directory),
                TagsOf(json),
                CountryOf(json),
                YearOf(json, string.IsNullOrEmpty(yearName) ? null : yearName),
                TextOf(json, "parent"),
                officialCarIds != null && officialCarIds.Contains(id) ? CarAuthors.Kunos : AuthorOf(json),
                SpecOf(json, "bhp"),
                SpecOf(json, "torque"),
                SpecOf(json, "weight"),
                SpecOf(json, "topspeed"),
                SpecOf(json, "acceleration"),
                SpecOf(json, "pwratio")));
        }

        cars.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return cars;
    }

    public static IReadOnlyList<TrackSummary> ScanTracks(string? tracksDirectory)
    {
        if (string.IsNullOrWhiteSpace(tracksDirectory) || !Directory.Exists(tracksDirectory)) {
            return [];
        }

        var tracks = new List<TrackSummary>();
        foreach (var directory in Directory.GetDirectories(tracksDirectory)) {
            var id = Path.GetFileName(directory);
            var ui = Path.Combine(directory, "ui");
            if (id.StartsWith("ks_", StringComparison.Ordinal) && !ContainsTrackJson(ui)) {
                continue;
            }

            if (!Directory.Exists(ui) && !File.Exists(Path.Combine(directory, "models.ini"))) {
                continue;
            }

            var rootJson = Path.Combine(ui, "ui_track.json");
            var rootPresent = File.Exists(rootJson);
            var found = new List<TrackLayoutInfo>();
            if (Directory.Exists(ui)) {
                foreach (var layoutDirectory in Directory.GetDirectories(ui)) {
                    var layoutJson = Path.Combine(layoutDirectory, "ui_track.json");
                    if (!File.Exists(layoutJson)) {
                        continue;
                    }

                    var json = ReadUiObject(layoutJson);
                    found.Add(new TrackLayoutInfo(
                        Path.GetFileName(layoutDirectory),
                        StringOf(json, "name"),
                        NumberOf(json, "priority")));
                }
            }

            var layouts = TrackLayoutOrder.Arrange(
                id,
                rootPresent,
                rootPresent ? NameOf(rootJson, id) : id,
                found);
            var common = layouts.Count > 1
                ? TrackLayoutOrder.CommonName(layouts.Select(layout => (string?)layout.Name).ToArray())
                : null;
            tracks.Add(new TrackSummary(id, common ?? layouts[0].Name, layouts));
        }

        tracks.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return tracks;
    }

    public static IReadOnlyList<WeatherSummary> ScanWeather(string? weatherDirectory)
    {
        if (string.IsNullOrWhiteSpace(weatherDirectory) || !Directory.Exists(weatherDirectory)) {
            return [];
        }

        var list = new List<WeatherSummary>();
        foreach (var directory in Directory.GetDirectories(weatherDirectory)) {
            var iniPath = Path.Combine(directory, "weather.ini");
            if (!File.Exists(iniPath)) {
                continue;
            }

            var id = Path.GetFileName(directory);
            string? name = null;
            try {
                name = IniFile.Load(iniPath)["LAUNCHER"].Get("NAME");
            } catch (IOException) {
                name = null;
            }

            list.Add(new WeatherSummary(id, string.IsNullOrWhiteSpace(name) ? id : name));
        }

        list.Sort(static (a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static bool IsListedCar(string directory)
    {
        var id = Path.GetFileName(directory);
        if (id.StartsWith("__cm_tmp_", StringComparison.Ordinal)) {
            return false;
        }

        var json = File.Exists(Path.Combine(directory, "ui", "ui_car.json"));
        if (id.StartsWith("ks_", StringComparison.Ordinal) && !json) {
            return false;
        }

        return json
            || Directory.Exists(Path.Combine(directory, "data"))
            || File.Exists(Path.Combine(directory, "data.acd"));
    }

    private static bool ContainsTrackJson(string uiDirectory)
    {
        if (!Directory.Exists(uiDirectory)) {
            return false;
        }

        try {
            foreach (var file in Directory.EnumerateFiles(uiDirectory, "*", SearchOption.AllDirectories)) {
                if (string.Equals(Path.GetFileName(file), "ui_track.json", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }

        return false;
    }

    private static IReadOnlyList<string> TagsOf(JsonElement? element)
    {
        if (!TryObject(element, out var car)
            || !car.TryGetProperty("tags", out var tags)
            || tags.ValueKind != JsonValueKind.Array) {
            return [];
        }

        var list = new List<string>();
        foreach (var item in tags.EnumerateArray()) {
            var text = item.ValueKind switch {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Number => item.GetRawText(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                _ => null,
            };
            if (text != null) {
                list.Add(text);
            }
        }

        return list;
    }

    private static string? CountryOf(JsonElement? element)
    {
        var country = StringOf(element, "country")?.Trim();
        return string.IsNullOrEmpty(country) ? null : country;
    }

    private static int? YearOf(JsonElement? element, string? name)
    {
        var year = IntOf(element, "year");
        if (year.HasValue) {
            return year;
        }

        if (string.IsNullOrEmpty(name)) {
            return null;
        }

        return CarYear.FromName(name) ?? CarYear.FromId(name);
    }

    private static string? AuthorOf(JsonElement? element)
    {
        var author = TextOf(element, "author")?.Trim();
        if (string.IsNullOrEmpty(author) || author == CarAuthors.Kunos) {
            return null;
        }

        return author;
    }

    private static string? SpecOf(JsonElement? element, string property)
    {
        if (!TryObject(element, out var car)
            || !car.TryGetProperty("specs", out var specs)
            || specs.ValueKind != JsonValueKind.Object) {
            return null;
        }

        return TextOf(specs, property);
    }

    private static int? IntOf(JsonElement? element, string property)
    {
        var text = TextOf(element, property);
        if (string.IsNullOrEmpty(text)
            || !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)) {
            return null;
        }

        return (int)number;
    }

    private static string? TextOf(JsonElement? element, string property)
    {
        if (!TryObject(element, out var car) || !car.TryGetProperty(property, out var value)) {
            return null;
        }

        return TextOf(value);
    }

    private static string? TextOf(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(property, out var child)) {
            return null;
        }

        return TextOf(child);
    }

    private static string? TextOf(JsonElement value)
    {
        var text = value.ValueKind switch {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static bool TryObject(JsonElement? element, out JsonElement car)
    {
        if (element is { ValueKind: JsonValueKind.Object } value) {
            car = value;
            return true;
        }

        car = default;
        return false;
    }

    private static double? NumberOf(JsonElement? element, string property)
    {
        if (element == null || element.Value.ValueKind != JsonValueKind.Object) {
            return null;
        }

        if (!element.Value.TryGetProperty(property, out var value)) {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<CarSkin> ScanSkins(string carDirectory)
    {
        var skinsDirectory = Path.Combine(carDirectory, "skins");
        if (!Directory.Exists(skinsDirectory)) {
            return [];
        }

        var skins = new List<CarSkin>();
        foreach (var directory in Directory.GetDirectories(skinsDirectory)
                     .OrderBy(static path => Path.GetFileName(path), AlphanumOrder.Instance)) {
            var id = Path.GetFileName(directory);
            var json = ReadUiObject(Path.Combine(directory, "ui_skin.json"));
            var name = StringOf(json, "skinname") ?? id;
            skins.Add(new CarSkin(id, name, FindPreview(directory)));
        }

        return skins;
    }

    private static string? FindPreview(string directory)
    {
        foreach (var name in PreviewNames) {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) {
                return path;
            }
        }

        return null;
    }

    private static string NameOf(string jsonPath, string fallback)
    {
        var json = ReadUiObject(jsonPath);
        return StringOf(json, "name") ?? fallback;
    }

    private static string? StringOf(JsonElement? element, string property)
    {
        if (element == null || element.Value.ValueKind != JsonValueKind.Object) {
            return null;
        }

        if (!element.Value.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static JsonElement? ReadUiObject(string path)
    {
        if (!File.Exists(path)) {
            return null;
        }

        string text;
        try {
            text = File.ReadAllText(path);
        } catch (IOException) {
            return null;
        }

        var relaxed = Relax(text);
        try {
            using var document = JsonDocument.Parse(relaxed, new JsonDocumentOptions {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            return document.RootElement.Clone();
        } catch (JsonException) {
            return RegexFallback(text);
        }
    }

    private static JsonElement? RegexFallback(string text)
    {
        var name = Regex.Match(text, "\"name\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        var brand = Regex.Match(text, "\"brand\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        var skin = Regex.Match(text, "\"skinname\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        if (!name.Success && !brand.Success && !skin.Success) {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append('{');
        var wrote = false;
        void Append(string key, Match match)
        {
            if (!match.Success) {
                return;
            }

            if (wrote) {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(key);
            builder.Append("\":\"");
            builder.Append(match.Groups[1].Value.Replace("\"", "\\\"", StringComparison.Ordinal));
            builder.Append('"');
            wrote = true;
        }

        Append("name", name);
        Append("brand", brand);
        Append("skinname", skin);
        builder.Append('}');
        try {
            using var document = JsonDocument.Parse(builder.ToString());
            return document.RootElement.Clone();
        } catch (JsonException) {
            return null;
        }
    }

    internal static string Relax(string text)
    {
        var builder = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (c == '"' && !IsEscaped(text, i)) {
                inString = !inString;
            }

            if (!inString && c == '/' && i + 1 < text.Length && text[i + 1] == '/') {
                while (i < text.Length && text[i] != '\n') {
                    i++;
                }

                builder.Append('\n');
                continue;
            }

            builder.Append(c);
        }

        return TrailingComma().Replace(builder.ToString(), "$1");
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--) {
            slashes++;
        }

        return slashes % 2 == 1;
    }

    private static readonly Regex TrailingCommaPattern = new(@",(\s*[}\]])", RegexOptions.Compiled);

    private static Regex TrailingComma() => TrailingCommaPattern;
}
