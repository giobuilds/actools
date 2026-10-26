using System.Globalization;
using System.Text.Json;

namespace AcManager.Core.Updates;

public static class ReleaseVersions
{
    public static bool TryNewerStable(string current, string releasesJson, out string? newer)
    {
        newer = null;
        if (!TryParts(current, out var currentParts)) {
            return false;
        }

        using var document = JsonDocument.Parse(releasesJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array) {
            return false;
        }

        int[]? best = null;
        foreach (var release in document.RootElement.EnumerateArray()) {
            if (release.ValueKind != JsonValueKind.Object) {
                continue;
            }

            if (Flag(release, "draft") || Flag(release, "prerelease")) {
                continue;
            }

            if (!release.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String) {
                continue;
            }

            var text = tag.GetString();
            if (string.IsNullOrWhiteSpace(text) || !TryParts(text, out var parts)) {
                continue;
            }

            if (Compare(parts, currentParts) <= 0) {
                continue;
            }

            if (best != null && Compare(parts, best) <= 0) {
                continue;
            }

            best = parts;
        }

        if (best != null) {
            newer = string.Join(".", best.Select(part => part.ToString(CultureInfo.InvariantCulture)));
        }

        return true;
    }

    private static bool Flag(JsonElement release, string name) =>
        release.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryParts(string text, out int[] parts)
    {
        parts = [];
        var value = text.Trim();
        if (value.Length > 0 && (value[0] == 'v' || value[0] == 'V')) {
            value = value[1..];
        }

        var cut = value.IndexOfAny(['-', '+']);
        if (cut >= 0) {
            value = value[..cut];
        }

        if (value.Length == 0) {
            return false;
        }

        var bits = value.Split('.');
        var parsed = new int[bits.Length];
        for (var i = 0; i < bits.Length; i++) {
            if (bits[i].Length == 0
                || !int.TryParse(bits[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number)) {
                return false;
            }

            parsed[i] = number;
        }

        parts = parsed;
        return true;
    }

    private static int Compare(int[] left, int[] right)
    {
        var count = Math.Max(left.Length, right.Length);
        for (var i = 0; i < count; i++) {
            var l = i < left.Length ? left[i] : 0;
            var r = i < right.Length ? right[i] : 0;
            if (l != r) {
                return l.CompareTo(r);
            }
        }

        return 0;
    }
}
