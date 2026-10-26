using System.Globalization;
using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// The driving assists in cfg/assists.ini. Other keys and the stability comment stay as written.
/// </summary>
public sealed record AssistSettings(
    bool IdealLine,
    bool AutoBlip,
    int StabilityControl,
    bool AutoBrake,
    bool AutoShifter,
    int Abs,
    int TractionControl,
    bool AutoClutch,
    bool VisualDamage,
    double Damage,
    double FuelRate,
    double TyreWear,
    bool TyreBlankets,
    double Slipstream)
{
    public static AssistSettings Default { get; } = new(
        false, false, 0, false, false, 1, 1, false, true, 100, 1, 1, false, 1);
}

public static class AssistSettingsFile
{
    public static string PathFor(string documentsDirectory) => CfgEdit.PathIn(documentsDirectory, "assists.ini");

    public static AssistSettings Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return AssistSettings.Default;
        }

        var section = IniFile.Load(path)["ASSISTS"];
        var fallback = AssistSettings.Default;
        return new AssistSettings(
            On(section, "IDEAL_LINE", fallback.IdealLine),
            On(section, "AUTO_BLIP", fallback.AutoBlip),
            Clamp(Int(section, "STABILITY_CONTROL", fallback.StabilityControl), 0, 100),
            On(section, "AUTO_BRAKE", fallback.AutoBrake),
            On(section, "AUTO_SHIFTER", fallback.AutoShifter),
            Clamp(Int(section, "ABS", fallback.Abs), 0, 2),
            Clamp(Int(section, "TRACTION_CONTROL", fallback.TractionControl), 0, 2),
            On(section, "AUTO_CLUTCH", fallback.AutoClutch),
            On(section, "VISUALDAMAGE", fallback.VisualDamage),
            Round(Double(section, "DAMAGE", fallback.Damage), 0, 100, 1),
            Round(Double(section, "FUEL_RATE", fallback.FuelRate), 0, 50, 2),
            Round(Double(section, "TYRE_WEAR", fallback.TyreWear), 0, 5, 2),
            On(section, "TYRE_BLANKETS", fallback.TyreBlankets),
            Round(Double(section, "SLIPSTREAM", fallback.Slipstream), 0, 10, 1));
    }

    public static string Write(string documentsDirectory, AssistSettings settings)
    {
        var path = PathFor(documentsDirectory);
        var clamped = ReadBack(settings);
        var values = new KeyValuePair<string, string>[] {
            new("IDEAL_LINE", Bit(clamped.IdealLine)),
            new("AUTO_BLIP", Bit(clamped.AutoBlip)),
            new("STABILITY_CONTROL", clamped.StabilityControl.ToString(CultureInfo.InvariantCulture)),
            new("AUTO_BRAKE", Bit(clamped.AutoBrake)),
            new("AUTO_SHIFTER", Bit(clamped.AutoShifter)),
            new("ABS", clamped.Abs.ToString(CultureInfo.InvariantCulture)),
            new("TRACTION_CONTROL", clamped.TractionControl.ToString(CultureInfo.InvariantCulture)),
            new("AUTO_CLUTCH", Bit(clamped.AutoClutch)),
            new("VISUALDAMAGE", Bit(clamped.VisualDamage)),
            new("DAMAGE", Number(clamped.Damage)),
            new("FUEL_RATE", Number(clamped.FuelRate)),
            new("TYRE_WEAR", Number(clamped.TyreWear)),
            new("TYRE_BLANKETS", Bit(clamped.TyreBlankets)),
            new("SLIPSTREAM", Number(clamped.Slipstream)),
        };
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        CfgEdit.Commit(path, CfgEdit.Apply(text, "ASSISTS", values));
        return path;
    }

    private static AssistSettings ReadBack(AssistSettings settings) => new(
        settings.IdealLine,
        settings.AutoBlip,
        Clamp(settings.StabilityControl, 0, 100),
        settings.AutoBrake,
        settings.AutoShifter,
        Clamp(settings.Abs, 0, 2),
        Clamp(settings.TractionControl, 0, 2),
        settings.AutoClutch,
        settings.VisualDamage,
        Round(settings.Damage, 0, 100, 1),
        Round(settings.FuelRate, 0, 50, 2),
        Round(settings.TyreWear, 0, 5, 2),
        settings.TyreBlankets,
        Round(settings.Slipstream, 0, 10, 1));

    private static bool On(IniSection section, string key, bool fallback)
    {
        var value = section.Get(key);
        if (string.IsNullOrWhiteSpace(value)) {
            return fallback;
        }

        return value.Trim() is "1" or "true" or "TRUE";
    }

    private static int Int(IniSection section, string key, int fallback) =>
        int.TryParse(section.Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static double Double(IniSection section, string key, double fallback) =>
        double.TryParse(section.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);

    private static double Round(double value, double min, double max, int digits) =>
        Math.Round(Math.Clamp(value, min, max), digits, MidpointRounding.AwayFromZero);

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
