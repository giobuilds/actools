using System.Globalization;
using System.Text.Json;

namespace AcManager.Core.Content;

/// <summary>
/// Canonical country names from gro-ove/actools-data <c>Miscellaneous/Countries.json</c>
/// and <c>TagCountries.json</c>. This is <c>AcStringValues.CountryFromTag</c>.
/// </summary>
public static class CountryNames
{
    private static readonly Lazy<Maps> Loaded = new(Load);

    public static string? FromTag(string tag)
    {
        var key = tag.ToLower(CultureInfo.CurrentCulture);
        var maps = Loaded.Value;
        var first = Pass(maps, key);
        if (first != null) {
            return first;
        }

        if (key.IndexOfAny(['(', ',', ';']) != -1) {
            var pieces = key.Split(['(', ')', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = pieces.Length - 1; i >= 0; i--) {
                var extra = Pass(maps, pieces[i]);
                if (extra != null) {
                    return extra;
                }
            }
        }

        return null;
    }

    private static string? Pass(Maps maps, string key)
    {
        key = key.Trim();
        return maps.Tags.GetValueOrDefault(key) ?? maps.Countries.GetValueOrDefault(key);
    }

    private static Maps Load()
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        var countries = new Dictionary<string, string>(StringComparer.Ordinal);
        try {
            ReadAliases(Read("TagCountries.json"), tags);
            ReadCountries(Read("Countries.json"), countries);
        } catch (JsonException) {
            return new Maps(tags, countries);
        } catch (IOException) {
            return new Maps(tags, countries);
        }

        return new Maps(tags, countries);
    }

    private static void ReadAliases(string json, Dictionary<string, string> tags)
    {
        if (json.Length == 0) {
            return;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            return;
        }

        foreach (var property in document.RootElement.EnumerateObject()) {
            if (property.Value.ValueKind != JsonValueKind.Array) {
                continue;
            }

            foreach (var alias in property.Value.EnumerateArray()) {
                if (alias.ValueKind != JsonValueKind.String) {
                    continue;
                }

                var name = alias.GetString();
                if (name != null) {
                    tags[name] = property.Name;
                }
            }
        }
    }

    private static void ReadCountries(string json, Dictionary<string, string> countries)
    {
        if (json.Length == 0) {
            return;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) {
            return;
        }

        foreach (var item in document.RootElement.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                continue;
            }

            var name = item.GetString();
            if (string.IsNullOrEmpty(name)) {
                continue;
            }

            countries[name.ToLowerInvariant()] = name;
        }
    }

    private static string Read(string fileName)
    {
        var stream = typeof(CountryNames).Assembly.GetManifestResourceStream(fileName);
        if (stream == null) {
            return "";
        }

        using (stream) {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    private sealed record Maps(
        Dictionary<string, string> Tags,
        Dictionary<string, string> Countries);
}
