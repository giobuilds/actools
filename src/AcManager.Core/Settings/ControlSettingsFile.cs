using System.Globalization;
using System.Text.RegularExpressions;
using AcTools.Core.Data;

namespace AcManager.Core.Settings;

public sealed record NamedController(int Index, string Name)
{
    public string Label => Index.ToString(CultureInfo.InvariantCulture) + "  " + Name;
}

/// <summary>
/// Device names, force-feedback gain, and steering lock from cfg/controls.ini.
/// Button and axis bindings are left untouched.
/// </summary>
public sealed record ControlSettings(
    string InputMethod,
    IReadOnlyList<NamedController> Controllers,
    int ForceFeedback,
    int SteerLock)
{
    public static ControlSettings Default { get; } = new("", [], 100, 900);
}

public static class ControlSettingsFile
{
    private static readonly Regex ControllerKey = new("^CON(\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string PathFor(string documentsDirectory) => CfgEdit.PathIn(documentsDirectory, "controls.ini");

    public static ControlSettings Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return ControlSettings.Default;
        }

        var file = IniFile.Load(path);
        var steer = file["STEER"];
        return new ControlSettings(
            file["HEADER"].Get("INPUT_METHOD")?.Trim() ?? "",
            Controllers(file["CONTROLLERS"]),
            Percent(steer.Get("FF_GAIN"), ControlSettings.Default.ForceFeedback),
            Lock(steer.Get("LOCK"), ControlSettings.Default.SteerLock));
    }

    public static string Write(string documentsDirectory, ControlSettings settings)
    {
        var path = PathFor(documentsDirectory);
        var values = new KeyValuePair<string, string>[] {
            new("FF_GAIN", Unit(settings.ForceFeedback)),
            new("LOCK", Math.Clamp(settings.SteerLock, 40, 1080).ToString(CultureInfo.InvariantCulture)),
        };
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        CfgEdit.Commit(path, CfgEdit.Apply(text, "STEER", values));
        return path;
    }

    private static IReadOnlyList<NamedController> Controllers(IniSection section)
    {
        var found = new List<NamedController>();
        foreach (var pair in section.Pairs) {
            var match = ControllerKey.Match(pair.Key);
            if (!match.Success) {
                continue;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) {
                continue;
            }

            var name = pair.Value.Trim();
            if (name.Length == 0) {
                continue;
            }

            found.Add(new NamedController(index, name));
        }

        found.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        return found;
    }

    private static int Percent(string? text, int fallback)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) {
            return fallback;
        }

        return (int)Math.Round(Math.Clamp(value, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }

    private static int Lock(string? text, int fallback)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) {
            return fallback;
        }

        return Math.Clamp(value, 40, 1080);
    }

    private static string Unit(int percent)
    {
        var unit = Math.Clamp(percent, 0, 100) / 100d;
        return unit.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
