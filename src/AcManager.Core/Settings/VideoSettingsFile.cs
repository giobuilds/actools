using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// The resolution and fullscreen flag in the prefix's cfg/video.ini.
/// Every other key, comment, and section is left as the game wrote it.
/// </summary>
public sealed record VideoSettings(int Width, int Height, bool Fullscreen)
{
    public static VideoSettings Default { get; } = new(1920, 1080, true);
}

public static class VideoSettingsFile
{
    public static string PathFor(string documentsDirectory) =>
        Path.Combine(documentsDirectory, "cfg", "video.ini");

    public static VideoSettings Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return VideoSettings.Default;
        }

        var video = IniFile.Load(path)["VIDEO"];
        return new VideoSettings(
            ParseInt(video.Get("WIDTH"), VideoSettings.Default.Width),
            ParseInt(video.Get("HEIGHT"), VideoSettings.Default.Height),
            IsEnabled(video.Get("FULLSCREEN"), VideoSettings.Default.Fullscreen));
    }

    public static string Write(string documentsDirectory, VideoSettings settings)
    {
        var path = PathFor(documentsDirectory);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        var updates = new KeyValuePair<string, string>[] {
            new("FULLSCREEN", settings.Fullscreen ? "1" : "0"),
            new("WIDTH", settings.Width.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("HEIGHT", settings.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };

        if (!File.Exists(path)) {
            File.WriteAllText(path, "[VIDEO]\r\n"
                + "FULLSCREEN=" + (settings.Fullscreen ? "1" : "0") + "\r\n"
                + "WIDTH=" + settings.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n"
                + "HEIGHT=" + settings.Height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n");
            return path;
        }

        File.Copy(path, path + ".bak", overwrite: true);
        var text = File.ReadAllText(path);
        File.WriteAllText(path, IniText.SetValues(text, "VIDEO", updates));
        return path;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool IsEnabled(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        return value.Trim() is "1" or "true" or "TRUE";
    }
}
