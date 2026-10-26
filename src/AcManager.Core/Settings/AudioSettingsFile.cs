using System.Globalization;
using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// Volume levels in cfg/audio.ini. The file stores 0 to 1. The page shows percent.
/// </summary>
public sealed record AudioSettings(
    int Master,
    int Engine,
    int Tyres,
    int Wind,
    int Opponents,
    int Music,
    int Ui,
    int Surfaces,
    int Dirt)
{
    public static AudioSettings Default { get; } = new(100, 100, 80, 90, 90, 50, 70, 100, 100);
}

public static class AudioSettingsFile
{
    public static string PathFor(string documentsDirectory) => CfgEdit.PathIn(documentsDirectory, "audio.ini");

    public static AudioSettings Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return AudioSettings.Default;
        }

        var levels = IniFile.Load(path)["LEVELS"];
        var fallback = AudioSettings.Default;
        return new AudioSettings(
            Percent(levels, "MASTER", fallback.Master),
            Percent(levels, "ENGINE", fallback.Engine),
            Percent(levels, "TYRES", fallback.Tyres),
            Percent(levels, "WIND", fallback.Wind),
            Percent(levels, "OPPONENTS", fallback.Opponents),
            Percent(levels, "MUSIC", fallback.Music),
            Percent(levels, "UISOUNDS", fallback.Ui),
            Percent(levels, "SURFACES", fallback.Surfaces),
            Percent(levels, "DIRT_BOTTOM", fallback.Dirt));
    }

    public static string Write(string documentsDirectory, AudioSettings settings)
    {
        var path = PathFor(documentsDirectory);
        var values = new KeyValuePair<string, string>[] {
            new("MASTER", Unit(settings.Master)),
            new("ENGINE", Unit(settings.Engine)),
            new("TYRES", Unit(settings.Tyres)),
            new("WIND", Unit(settings.Wind)),
            new("OPPONENTS", Unit(settings.Opponents)),
            new("MUSIC", Unit(settings.Music)),
            new("UISOUNDS", Unit(settings.Ui)),
            new("SURFACES", Unit(settings.Surfaces)),
            new("DIRT_BOTTOM", Unit(settings.Dirt)),
        };
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        CfgEdit.Commit(path, CfgEdit.Apply(text, "LEVELS", values));
        return path;
    }

    private static int Percent(IniSection section, string key, int fallback)
    {
        if (!double.TryParse(section.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
            return fallback;
        }

        return (int)Math.Round(Math.Clamp(value, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }

    private static string Unit(int percent)
    {
        var unit = Math.Clamp(percent, 0, 100) / 100d;
        return unit.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
