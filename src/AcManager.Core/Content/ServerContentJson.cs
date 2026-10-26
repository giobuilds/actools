using System.Text.Json;

namespace AcManager.Core.Content;

public sealed record ServerContentOffer(string Id, InstalledContentKind Kind, string Url);

/// <summary>
/// Reads the content.cars and content.track URLs from an AssettoServer /api/details body.
/// The track folder is the last real segment of the lobby track, so csp/3424/../dr_patreon_longbeach
/// installs as dr_patreon_longbeach.
/// </summary>
public static class ServerContentJson
{
    public static string? TrackFolderId(string? track)
    {
        if (string.IsNullOrWhiteSpace(track)) {
            return null;
        }

        var stack = new List<string>();
        foreach (var part in track.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (part == ".") {
                continue;
            }

            if (part == "..") {
                if (stack.Count > 0) {
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            stack.Add(part);
        }

        if (stack.Count == 0) {
            return null;
        }

        var id = stack[^1];
        return IsSafeId(id) ? id : null;
    }

    public static bool TryRead(string? json, string? trackField, out IReadOnlyList<ServerContentOffer> offers, out string error)
    {
        offers = [];
        error = "The server did not publish downloads.";
        if (string.IsNullOrWhiteSpace(json)) {
            return false;
        }

        var trimmed = json.Trim().TrimStart('\uFEFF');
        if (trimmed.Length == 0 || trimmed[0] != '{') {
            return false;
        }

        JsonDocument document;
        try {
            document = JsonDocument.Parse(trimmed);
        } catch (JsonException) {
            return false;
        }

        using (document) {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryProperty(document.RootElement, "content", out var content)
                || content.ValueKind != JsonValueKind.Object) {
                error = "";
                return true;
            }

            if (IsTrue(content, "password")) {
                error = "This server's downloads need the server password.";
                return false;
            }

            var list = new List<ServerContentOffer>();
            var refused = false;
            var needsSteam = false;
            if (TryProperty(content, "cars", out var cars) && cars.ValueKind == JsonValueKind.Object) {
                foreach (var pair in cars.EnumerateObject()) {
                    if (!IsSafeId(pair.Name)) {
                        continue;
                    }

                    if (!TryContentUrl(pair.Value, out var url, out var blocked)) {
                        needsSteam |= blocked;
                        continue;
                    }

                    if (!ContentDownloadUrl.IsAllowed(url)) {
                        refused = true;
                        continue;
                    }

                    list.Add(new ServerContentOffer(pair.Name, InstalledContentKind.Car, url));
                }
            }

            if (TryProperty(content, "track", out var track)) {
                if (TryContentUrl(track, out var trackUrl, out var trackBlocked)) {
                    var id = TrackFolderId(trackField) ?? IdFromUrl(trackUrl);
                    if (id != null && ContentDownloadUrl.IsAllowed(trackUrl)) {
                        list.Add(new ServerContentOffer(id, InstalledContentKind.Track, trackUrl));
                    } else if (id != null) {
                        refused = true;
                    }
                } else {
                    needsSteam |= trackBlocked;
                }
            }

            offers = list;
            if (list.Count > 0) {
                error = "";
                return true;
            }

            if (needsSteam) {
                error = "This server's downloads need Steam authentication.";
                return false;
            }

            if (refused) {
                error = "A download address was refused because it is not a public web address.";
                return false;
            }

            error = "";
            return true;
        }
    }

    private static bool TryContentUrl(JsonElement value, out string url, out bool needsSteam)
    {
        url = "";
        needsSteam = false;
        if (value.ValueKind == JsonValueKind.String) {
            url = value.GetString()?.Trim() ?? "";
            return url.Length > 0;
        }

        if (value.ValueKind != JsonValueKind.Object) {
            return false;
        }

        if (IsTrue(value, "steamAuth")) {
            needsSteam = true;
            return false;
        }

        if (!TryProperty(value, "url", out var urlValue) || urlValue.ValueKind != JsonValueKind.String) {
            return false;
        }

        url = urlValue.GetString()?.Trim() ?? "";
        return url.Length > 0;
    }

    private static string? IdFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(uri.AbsolutePath));
        return IsSafeId(name) ? name : null;
    }

    private static bool IsTrue(JsonElement obj, string name) =>
        TryProperty(obj, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object) {
            foreach (var property in obj.EnumerateObject()) {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool IsSafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id is "." or "..") {
            return false;
        }

        foreach (var c in id) {
            if (c is '/' or '\\' or ':' or '<' or '>' or '"' or '|' or '?' or '*') {
                return false;
            }

            if (c < ' ') {
                return false;
            }
        }

        return true;
    }
}
