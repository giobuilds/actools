using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AcTools.Core.Data;

namespace AcManager.Core.Content;

public sealed record CarSkin(string Id, string Name, string? PreviewPath);

public sealed record CarSummary(string Id, string Name, string? Brand, string? ClassName, IReadOnlyList<CarSkin> Skins)
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

    public static IReadOnlyList<CarSummary> ScanCars(string? carsDirectory)
    {
        if (string.IsNullOrWhiteSpace(carsDirectory) || !Directory.Exists(carsDirectory)) {
            return [];
        }

        var cars = new List<CarSummary>();
        foreach (var directory in Directory.GetDirectories(carsDirectory)) {
            if (!IsCarDirectory(directory)) {
                continue;
            }

            var id = Path.GetFileName(directory);
            var json = ReadUiObject(Path.Combine(directory, "ui", "ui_car.json"));
            var name = StringOf(json, "name") ?? id;
            var brand = StringOf(json, "brand");
            var className = StringOf(json, "class");
            cars.Add(new CarSummary(id, name, brand, className, ScanSkins(directory)));
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
            var ui = Path.Combine(directory, "ui");
            if (!Directory.Exists(ui) && !File.Exists(Path.Combine(directory, "models.ini"))) {
                continue;
            }

            var id = Path.GetFileName(directory);
            var layouts = new List<TrackLayout>();
            var mainJson = Path.Combine(ui, "ui_track.json");
            if (File.Exists(mainJson)) {
                layouts.Add(new TrackLayout("", NameOf(mainJson, id)));
            }

            if (Directory.Exists(ui)) {
                foreach (var layoutDirectory in Directory.GetDirectories(ui).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)) {
                    var layoutJson = Path.Combine(layoutDirectory, "ui_track.json");
                    if (!File.Exists(layoutJson)) {
                        continue;
                    }

                    var layoutId = Path.GetFileName(layoutDirectory);
                    layouts.Add(new TrackLayout(layoutId, NameOf(layoutJson, layoutId)));
                }
            }

            if (layouts.Count == 0) {
                layouts.Add(new TrackLayout("", id));
            }

            var display = layouts[0].Name;
            tracks.Add(new TrackSummary(id, display, layouts));
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

    private static bool IsCarDirectory(string directory) =>
        File.Exists(Path.Combine(directory, "ui", "ui_car.json"))
        || Directory.Exists(Path.Combine(directory, "data"))
        || File.Exists(Path.Combine(directory, "data.acd"));

    private static IReadOnlyList<CarSkin> ScanSkins(string carDirectory)
    {
        var skinsDirectory = Path.Combine(carDirectory, "skins");
        if (!Directory.Exists(skinsDirectory)) {
            return [];
        }

        var skins = new List<CarSkin>();
        foreach (var directory in Directory.GetDirectories(skinsDirectory).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)) {
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
