using System.Text.Json;

namespace AcManager.Core.Online;

/// <summary>
/// CSP session flags from an AssettoServer /api/details body.
/// Extended is set when the server publishes a weather id.
/// Features is the comma-separated list Content Manager writes to race.ini __FEATURES.
/// </summary>
public sealed record ServerSessionFlags(bool Extended, string? Features);

public static class ServerSessionFlagsJson
{
    public static ServerSessionFlags Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) {
            return new ServerSessionFlags(false, null);
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(json.Trim().TrimStart('\uFEFF'));
        } catch (JsonException) {
            return new ServerSessionFlags(false, null);
        }

        using (document) {
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return new ServerSessionFlags(false, null);
            }

            var root = document.RootElement;
            var extended = false;
            if (root.TryGetProperty("currentWeatherId", out var weather) && weather.ValueKind == JsonValueKind.String) {
                var text = weather.GetString();
                extended = !string.IsNullOrWhiteSpace(text);
            }

            string? features = null;
            if (root.TryGetProperty("features", out var list) && list.ValueKind == JsonValueKind.Array) {
                features = Join(list);
            }

            return new ServerSessionFlags(extended, features);
        }
    }

    public static string? Join(IEnumerable<string> names)
    {
        var parts = new List<string>();
        foreach (var name in names) {
            if (!IsFeature(name) || parts.Contains(name)) {
                continue;
            }

            parts.Add(name);
            if (parts.Count == 32) {
                break;
            }
        }

        return parts.Count == 0 ? null : string.Join(",", parts);
    }

    private static string? Join(JsonElement list)
    {
        var names = new List<string>();
        foreach (var item in list.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.String) {
                continue;
            }

            var name = item.GetString();
            if (name != null) {
                names.Add(name);
            }
        }

        return Join(names);
    }

    private static bool IsFeature(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 40) {
            return false;
        }

        foreach (var c in token) {
            if (c is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')) {
                return false;
            }
        }

        return true;
    }
}
