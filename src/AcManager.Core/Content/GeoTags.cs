using System.Text.Json;
using System.Text.RegularExpressions;
using AcTools.Utils.Helpers;

namespace AcManager.Core.Content;

/// <summary>
/// Latitude and longitude from a track <c>geotags</c> array.
/// Copied from <c>GeoTagsEntry</c>. The numeric constructor stores degrees directly,
/// the same way <c>GeoTagsEntry(double, double)</c> does for <c>Track Params.ini</c>.
/// </summary>
public sealed class GeoTags
{
    public GeoTags(string latitude, string longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        LatitudeValue = ParseCoordinate(latitude, true);
        LongitudeValue = ParseCoordinate(longitude, false);
    }

    public GeoTags(double latitude, double longitude)
    {
        LatitudeValue = latitude;
        LongitudeValue = longitude;
        Latitude = FormatCoordinates(latitude, true);
        Longitude = FormatCoordinates(longitude, false);
    }

    public string Latitude { get; }

    public string Longitude { get; }

    public double? LatitudeValue { get; }

    public double? LongitudeValue { get; }

    public bool IsEmptyOrInvalid => LatitudeValue is null || LongitudeValue is null;

    public string DisplayLatitude => IsEmptyOrInvalid ? Latitude : FormatCoordinates(LatitudeValue ?? 0d, true);

    public string DisplayLongitude => IsEmptyOrInvalid ? Longitude : FormatCoordinates(LongitudeValue ?? 0d, false);

    public override string ToString() => IsEmptyOrInvalid ? "" : DisplayLatitude + ", " + DisplayLongitude;

    public static GeoTags? Read(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } json
            || !json.TryGetProperty("geotags", out var tags)
            || tags.ValueKind != JsonValueKind.Array
            || tags.GetArrayLength() != 2) {
            return null;
        }

        var latitude = tags[0];
        var longitude = tags[1];
        if (latitude.ValueKind != JsonValueKind.String || longitude.ValueKind != JsonValueKind.String) {
            return null;
        }

        return new GeoTags(latitude.GetString() ?? "", longitude.GetString() ?? "");
    }

    private static double? ParseCoordinate(string text, bool latitude)
    {
        var matches = Regex.Matches(text, @"-?\d+([\.,]\d+)?");
        if (matches.Count < 1) {
            return null;
        }

        var parts = new double[matches.Count];
        for (var i = 0; i < matches.Count; i++) {
            parts[i] = FlexibleParser.TryParseDouble(matches[i].Value) ?? 0d;
        }

        var degrees = At(parts, 0);
        var minutes = At(parts, 1);
        var seconds = At(parts, 2);
        var milliseconds = At(parts, 3);
        var sign = degrees >= 0d ? 1d : -1d;
        degrees = Math.Abs(degrees);

        if (parts.Length == 1) {
            if (degrees > 909090d) {
                milliseconds = degrees;
                degrees = 0d;
            } else if (degrees > 9090d) {
                var packedDegrees = Math.Floor(degrees / 10000d);
                minutes = Math.Floor((degrees - packedDegrees * 10000d) / 100d);
                seconds = Math.Floor(degrees - packedDegrees * 10000d - minutes * 100d);
                degrees = packedDegrees;
            } else if (degrees > 360d) {
                var packedDegrees = Math.Floor(degrees / 100d);
                minutes = degrees - packedDegrees * 100d;
                degrees = packedDegrees;
            }
        }

        var result = sign * (degrees + minutes / 60d + seconds / 3600d + milliseconds / 3600000d);
        if (Math.Abs(result) > (latitude ? 90d : 180d)) {
            return null;
        }

        if (text.ToLowerInvariant().Contains(latitude ? "s" : "w")) {
            result = -result;
        }

        return result;
    }

    private static double At(double[] parts, int index) => index < parts.Length ? parts[index] : 0d;

    private static string FormatCoordinates(double degrees, bool latitude)
    {
        var isNegative = degrees < 0d;
        degrees = Math.Abs(degrees);

        var deg = (int)Math.Floor(degrees);
        var delta = degrees - deg;
        var wholeSeconds = (int)Math.Floor(3600.0 * delta);
        var sec = wholeSeconds % 60;
        var min = (int)Math.Round(wholeSeconds / 60.0);
        var post = latitude ? isNegative ? "S" : "N" : isNegative ? "W" : "E";
        return $"{deg}° {min:00}′ {sec:00}″ {post}";
    }
}
