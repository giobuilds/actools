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
    string? SpecsPwRatio = null,
    string? Version = null,
    string? Description = null,
    string? Url = null,
    double MaxRpm = 0)
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

public sealed record TrackLayout(
    string Id,
    string Name,
    string? Version = null,
    string? Description = null,
    string? City = null,
    string? SpecsLength = null,
    string? SpecsPitboxes = null,
    string? SpecsWidth = null,
    GeoTags? GeoTags = null,
    string? Country = null,
    int? Year = null,
    IReadOnlyList<string>? Tags = null,
    string? Author = null,
    string? Url = null,
    double Priority = 0);

public sealed record TrackSummary(string Id, string Name, IReadOnlyList<TrackLayout> Layouts)
{
    public string Subtitle => Layouts.Count <= 1 ? Id : Id + "  ·  " + Layouts.Count.ToString(CultureInfo.InvariantCulture) + " layouts";

    /// <summary>City of the main layout. Content Manager stores city on each layout.</summary>
    public string? City => Layouts.Count == 0 ? null : Layouts[0].City;

    /// <summary>Length text of the main layout. Search converts this text to kilometres.</summary>
    public string? SpecsLength => Layouts.Count == 0 ? null : Layouts[0].SpecsLength;

    /// <summary>Pit box text of the main layout. Search reads the number in the text. A missing value is not 2.</summary>
    public string? SpecsPitboxes => Layouts.Count == 0 ? null : Layouts[0].SpecsPitboxes;

    /// <summary>Width text of the main layout. Search converts this text to metres.</summary>
    public string? SpecsWidth => Layouts.Count == 0 ? null : Layouts[0].SpecsWidth;

    /// <summary>Geotags of the main layout. The DataProvider latitude list stays unread.</summary>
    public GeoTags? GeoTags => Layouts.Count == 0 ? null : Layouts[0].GeoTags;

    /// <summary>Country of the main layout. A mapped tag fills this when the JSON country is missing.</summary>
    public string? Country => Layouts.Count == 0 ? null : Layouts[0].Country;

    /// <summary>Year of the main layout. The DataProvider track-year list stays unread.</summary>
    public int? Year => Layouts.Count == 0 ? null : Layouts[0].Year;

    /// <summary>Tags of the main layout. A mapped tag can also fill the country.</summary>
    public IReadOnlyList<string> Tags => Layouts.Count == 0 ? [] : Layouts[0].Tags ?? [];

    /// <summary>Author of the main layout. <c>Kunos</c> stays empty, and a <c>ks_</c> id does not fill it.</summary>
    public string? Author => Layouts.Count == 0 ? null : Layouts[0].Author;

    /// <summary>URL of the main layout. A blank value stays empty.</summary>
    public string? Url => Layouts.Count == 0 ? null : Layouts[0].Url;

    /// <summary>Priority of the main layout. A missing priority is 0.</summary>
    public double Priority => Layouts.Count == 0 ? 0d : Layouts[0].Priority;
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
            var trimmedName = jsonName?.Trim();
            var (name, version) = SplitVersion(id, trimmedName, TextOf(json, "version"));
            cars.Add(new CarSummary(
                id,
                name,
                StringOf(json, "brand"),
                StringOf(json, "class"),
                ScanSkins(directory),
                TagsOf(json),
                CountryOf(json),
                YearOf(json, string.IsNullOrEmpty(trimmedName) ? null : trimmedName),
                TextOf(json, "parent"),
                officialCarIds != null && officialCarIds.Contains(id) ? CarAuthors.Kunos : AuthorOf(json),
                SpecOf(json, "bhp"),
                SpecOf(json, "torque"),
                SpecOf(json, "weight"),
                SpecOf(json, "topspeed"),
                SpecOf(json, "acceleration"),
                SpecOf(json, "pwratio"),
                version,
                ContentDescription.Decode(TextOf(json, "description")),
                UrlOf(json),
                MaxRpmOf(json)));
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
                    var layoutId = Path.GetFileName(layoutDirectory);
                    var trimmedName = StringOf(json, "name")?.Trim();
                    var (layoutName, layoutVersion) = SplitVersion(layoutId, trimmedName, TextOf(json, "version"));
                    var (version, description) = DescriptionVersion(layoutVersion, json);
                    found.Add(new TrackLayoutInfo(
                        layoutId,
                        layoutName,
                        PriorityOf(json),
                        version,
                        description,
                        TextOf(json, "city"),
                        LengthOf(json),
                        PitboxesOf(json),
                        WidthOf(json),
                        GeoTags.Read(json),
                        CountryOf(json),
                        YearOf(json, string.IsNullOrEmpty(trimmedName) ? null : trimmedName),
                        TagsOf(json),
                        TrackAuthorOf(json),
                        UrlOf(json)));
                }
            }

            string rootName;
            string? rootVersion;
            string? rootDescription;
            string? rootCity;
            string? rootLength;
            string? rootPitboxes;
            string? rootWidth;
            GeoTags? rootGeoTags;
            string? rootCountry;
            int? rootYear;
            IReadOnlyList<string>? rootTags;
            string? rootAuthor;
            string? rootUrl;
            double rootPriority;
            if (rootPresent) {
                var rootElement = ReadUiObject(rootJson);
                var trimmedName = StringOf(rootElement, "name")?.Trim();
                (rootName, rootVersion) = SplitVersion(id, trimmedName, TextOf(rootElement, "version"));
                (rootVersion, rootDescription) = DescriptionVersion(rootVersion, rootElement);
                rootCity = TextOf(rootElement, "city");
                rootLength = LengthOf(rootElement);
                rootPitboxes = PitboxesOf(rootElement);
                rootWidth = WidthOf(rootElement);
                rootGeoTags = GeoTags.Read(rootElement);
                rootCountry = CountryOf(rootElement);
                rootYear = YearOf(rootElement, string.IsNullOrEmpty(trimmedName) ? null : trimmedName);
                rootTags = TagsOf(rootElement);
                rootAuthor = TrackAuthorOf(rootElement);
                rootUrl = UrlOf(rootElement);
                rootPriority = PriorityOf(rootElement);
            } else {
                rootName = id;
                rootVersion = null;
                rootDescription = null;
                rootCity = null;
                rootLength = null;
                rootPitboxes = null;
                rootWidth = null;
                rootGeoTags = null;
                rootCountry = null;
                rootYear = null;
                rootTags = null;
                rootAuthor = null;
                rootUrl = null;
                rootPriority = 0d;
            }

            var layouts = TrackLayoutOrder.Arrange(
                id,
                rootPresent,
                rootName,
                found,
                rootVersion,
                rootDescription,
                rootCity,
                rootLength,
                rootPitboxes,
                rootWidth,
                rootGeoTags,
                rootCountry,
                rootYear,
                rootTags,
                rootAuthor,
                rootUrl,
                rootPriority);
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

    private static (string Name, string? Version) SplitVersion(string fallback, string? trimmedName, string? jsonVersion)
    {
        var version = jsonVersion?.Trim();
        if (string.IsNullOrEmpty(version)) {
            version = null;
        }

        if (string.IsNullOrEmpty(trimmedName)) {
            return (fallback, version);
        }

        if (version != null) {
            return (trimmedName, version);
        }

        var parsed = CarVersion.FromName(trimmedName, out var stripped);
        return parsed == null ? (trimmedName, null) : (stripped, parsed);
    }

    /// <summary>
    /// <c>TrackObjectBase.LoadData</c> reads a trailing version from the description
    /// when the JSON and the name left the version empty.
    /// </summary>
    private static (string? Version, string? Description) DescriptionVersion(string? version, JsonElement? json)
    {
        var description = ContentDescription.Decode(TextOf(json, "description"));
        if (version != null || description == null) {
            return (version, description);
        }

        var parsed = CarVersion.FromName(description, out var shortened);
        return parsed == null ? (null, description) : (parsed, shortened);
    }

    /// <summary>
    /// <c>TrackObjectBase.SpecsLength</c> trims the JSON text. An all-space value becomes empty.
    /// The kilometre conversion stays unread.
    /// </summary>
    private static string? LengthOf(JsonElement? element) => TextOf(element, "length")?.Trim();

    /// <summary>
    /// <c>TrackObjectBase.SpecsPitboxes</c> trims the JSON text. An all-space value becomes empty.
    /// The numeric parse stays unread.
    /// </summary>
    private static string? PitboxesOf(JsonElement? element) => TextOf(element, "pitboxes")?.Trim();

    /// <summary>
    /// <c>TrackObjectBase.SpecsWidth</c> trims the JSON text. An all-space value becomes empty.
    /// The metre conversion stays unread.
    /// </summary>
    private static string? WidthOf(JsonElement? element) => TextOf(element, "width")?.Trim();

    /// <summary>
    /// <c>AcJsonObjectNew.LoadCountry</c>. A present country is normalized with
    /// <c>CountryFromTag</c>. When that key is missing, the first mapped tag is used.
    /// An all-space country stays empty. Brand countries stay unread.
    /// </summary>
    private static string? CountryOf(JsonElement? element)
    {
        var value = TextOf(element, "country")?.Trim();
        if (value != null) {
            return CountryNames.FromTag(value) ?? value;
        }

        foreach (var tag in TagsOf(element)) {
            var country = CountryNames.FromTag(tag);
            if (country != null) {
                return country;
            }
        }

        return null;
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

    /// <summary>
    /// JSON year first, then the year at the end of <paramref name="name"/>.
    /// The DataProvider year lists stay unread. <paramref name="name"/> is the
    /// trimmed JSON name before a trailing version is removed.
    /// </summary>
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

    /// <summary>
    /// <c>AcJsonObjectNew.LoadVersionInfo</c> trims the JSON text. An all-space value becomes empty.
    /// <c>Kunos</c> becomes empty. The official-track list stays unread, so a <c>ks_</c> id does not fill the author.
    /// </summary>
    private static string? TrackAuthorOf(JsonElement? element)
    {
        var author = TextOf(element, "author")?.Trim();
        if (author == null || author == CarAuthors.Kunos) {
            return null;
        }

        return author;
    }

    /// <summary>
    /// <c>AcJsonObjectNew.Url</c> trims the JSON text and clears a blank value.
    /// </summary>
    private static string? UrlOf(JsonElement? element)
    {
        var url = TextOf(element, "url")?.Trim();
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    /// <summary>
    /// <c>CarObject.GetRpmMaxValue</c> reads the highest RPM on the torque curve.
    /// LoadData always builds that curve, so a missing or empty <c>torqueCurve</c> contributes 0.
    /// The power curve stays unread for this value.
    /// </summary>
    private static double MaxRpmOf(JsonElement? element)
    {
        if (!TryObject(element, out var car)
            || !car.TryGetProperty("torqueCurve", out var curve)
            || curve.ValueKind != JsonValueKind.Array) {
            return 0d;
        }

        double? max = null;
        foreach (var entry in curve.EnumerateArray()) {
            if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() != 2) {
                continue;
            }

            var xText = TextOf(entry[0]);
            var yText = TextOf(entry[1]);
            if (!CarSpecQuery.TryParse(xText, out var x) || !CarSpecQuery.TryParse(yText, out _)) {
                continue;
            }

            if (max == null || x > max) {
                max = x;
            }
        }

        return max ?? 0d;
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

    /// <summary>
    /// <c>TrackObjectBase.LoadData</c> reads <c>priority</c> with <c>GetDoubleValueOnly</c>.
    /// A missing or unreadable value becomes 0.
    /// </summary>
    private static double PriorityOf(JsonElement? element) => NumberOf(element, "priority") ?? 0d;

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
            && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) {
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
