using System.Globalization;
using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// Force-feedback post-processing in cfg/ff_post_process.ini.
/// Gamma is the 0 to 2 value the file stores. LUT files are the cfg folder's .lut names.
/// </summary>
public sealed record FfbPostProcess(bool Enabled, string Type, double Gamma, string? Curve)
{
    public const string GammaType = "GAMMA";
    public const string LutType = "LUT";

    public static FfbPostProcess Default { get; } = new(false, GammaType, 0.5, null);
}

public static class FfbPostProcessFile
{
    public static string PathFor(string documentsDirectory) => CfgEdit.PathIn(documentsDirectory, "ff_post_process.ini");

    public static IReadOnlyList<string> Curves(string documentsDirectory)
    {
        var cfg = Path.Combine(documentsDirectory, "cfg");
        if (!Directory.Exists(cfg)) {
            return [];
        }

        return Directory.GetFiles(cfg, "*.lut")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static FfbPostProcess Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return FfbPostProcess.Default;
        }

        var file = IniFile.Load(path);
        var header = file.Contains("HEADER") ? file["HEADER"] : null;
        var gamma = file.Contains("GAMMA") ? file["GAMMA"] : null;
        var lut = file.Contains("LUT") ? file["LUT"] : null;
        return new FfbPostProcess(
            On(header?.Get("ENABLED")),
            Mode(header?.Get("TYPE")),
            ParseGamma(gamma?.Get("VALUE")),
            Curve(lut?.Get("CURVE")));
    }

    public static string Write(string documentsDirectory, FfbPostProcess settings)
    {
        var path = PathFor(documentsDirectory);
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        var gamma = ClampGamma(settings.Gamma);
        text = CfgEdit.Apply(text, "HEADER", [
            new KeyValuePair<string, string>("ENABLED", settings.Enabled ? "1" : "0"),
            new KeyValuePair<string, string>("TYPE", Mode(settings.Type)),
        ]);
        text = CfgEdit.Apply(text, "GAMMA", [
            new KeyValuePair<string, string>("VALUE", gamma.ToString("0.##", CultureInfo.InvariantCulture)),
        ]);
        var curve = Curve(settings.Curve);
        if (curve != null) {
            text = CfgEdit.Apply(text, "LUT", [
                new KeyValuePair<string, string>("CURVE", curve),
            ]);
        }

        CfgEdit.Commit(path, text);
        return path;
    }

    private static bool On(string? value) => value?.Trim() is "1" or "true" or "TRUE";

    private static string Mode(string? value)
    {
        var mode = value?.Trim() ?? "";
        return mode.Equals(FfbPostProcess.LutType, StringComparison.OrdinalIgnoreCase)
            ? FfbPostProcess.LutType
            : FfbPostProcess.GammaType;
    }

    private static double ParseGamma(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? ClampGamma(parsed)
            : FfbPostProcess.Default.Gamma;

    private static double ClampGamma(double value) =>
        Math.Round(Math.Clamp(value, 0, 2), 2, MidpointRounding.AwayFromZero);

    private static string? Curve(string? value)
    {
        var curve = value?.Trim() ?? "";
        return curve.Length == 0 ? null : curve;
    }
}
