using System.Globalization;

namespace AcManager.Core.Settings;

/// <summary>
/// Graphics controls from Custom Shaders Patch. Pixel density is a percent of the 0.1 to 1 render scale.
/// Grass quality is 0 to 4. Saves override the prefix cfg/extension files and leave the game folder alone.
/// </summary>
public sealed record GraphicsSettings(bool Adjustments, int PixelDensity, bool Grass, int GrassQuality, bool ExtraFx)
{
    public const string AdjustmentsFile = "graphics_adjustments.ini";
    public const string GrassFile = "grass_fx.ini";
    public const string ExtraFxFile = "extra_fx.ini";

    public static GraphicsSettings Default { get; } = new(true, 100, true, 2, false);
}

public static class GraphicsSettingsFile
{
    public static GraphicsSettings Read(string? gameRoot, string? documentsDirectory)
    {
        var fallback = GraphicsSettings.Default;
        return new GraphicsSettings(
            ExtensionFlagFile.ReadEnabled(gameRoot, documentsDirectory, GraphicsSettings.AdjustmentsFile, fallback.Adjustments),
            Percent(ExtensionFlagFile.ReadValue(gameRoot, documentsDirectory, GraphicsSettings.AdjustmentsFile, "RENDER_SCALE", "SCALE"), fallback.PixelDensity),
            ExtensionFlagFile.ReadEnabled(gameRoot, documentsDirectory, GraphicsSettings.GrassFile, fallback.Grass),
            Quality(ExtensionFlagFile.ReadValue(gameRoot, documentsDirectory, GraphicsSettings.GrassFile, "BASIC", "QUALITY"), fallback.GrassQuality),
            ExtensionFlagFile.ReadEnabled(gameRoot, documentsDirectory, GraphicsSettings.ExtraFxFile, fallback.ExtraFx));
    }

    public static void Write(string documentsDirectory, GraphicsSettings settings)
    {
        ExtensionFlagFile.Write(documentsDirectory, GraphicsSettings.AdjustmentsFile, [
            new ExtensionEdit("BASIC", [
                new KeyValuePair<string, string>("ENABLED", settings.Adjustments ? "1" : "0"),
            ]),
            new ExtensionEdit("RENDER_SCALE", [
                new KeyValuePair<string, string>("SCALE", Unit(settings.PixelDensity)),
            ]),
        ]);
        ExtensionFlagFile.Write(documentsDirectory, GraphicsSettings.GrassFile, [
            new ExtensionEdit("BASIC", [
                new KeyValuePair<string, string>("ENABLED", settings.Grass ? "1" : "0"),
                new KeyValuePair<string, string>("QUALITY", Math.Clamp(settings.GrassQuality, 0, 4).ToString(CultureInfo.InvariantCulture)),
            ]),
        ]);
        ExtensionFlagFile.WriteEnabled(documentsDirectory, GraphicsSettings.ExtraFxFile, settings.ExtraFx);
    }

    private static int Percent(string? text, int fallback)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
            return fallback;
        }

        return (int)Math.Round(Math.Clamp(value, 0.1, 1) * 100, MidpointRounding.AwayFromZero);
    }

    private static string Unit(int percent)
    {
        var unit = Math.Clamp(percent, 10, 100) / 100d;
        return unit.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static int Quality(string? text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 4)
            : fallback;
}
