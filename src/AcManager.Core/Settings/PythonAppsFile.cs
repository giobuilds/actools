using AcTools.Core.Data;

namespace AcManager.Core.Settings;

public sealed record PythonAppSetting(string Section, string Name, bool Active);

/// <summary>
/// Which Python apps are active in cfg/python.ini.
/// Apps come from that file and from apps/python in the game folder. The system folder is the game's own helper.
/// </summary>
public static class PythonAppsFile
{
    public static string PathFor(string documentsDirectory) => CfgEdit.PathIn(documentsDirectory, "python.ini");

    public static IReadOnlyList<PythonAppSetting> Read(string documentsDirectory, string? gameRoot)
    {
        var apps = new List<PythonAppSetting>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = PathFor(documentsDirectory);
        if (File.Exists(path)) {
            var file = IniFile.Load(path);
            foreach (var section in file.SectionNames) {
                var active = ActiveValue(file[section]);
                if (active == null) {
                    continue;
                }

                seen.Add(section);
                apps.Add(new PythonAppSetting(section, DisplayName(gameRoot, section), active.Value));
            }
        }

        foreach (var folder in Folders(gameRoot)) {
            if (!seen.Add(folder)) {
                continue;
            }

            apps.Add(new PythonAppSetting(folder.ToUpperInvariant(), folder, false));
        }

        return apps;
    }

    public static string Write(string documentsDirectory, IReadOnlyList<PythonAppSetting> apps)
    {
        var path = PathFor(documentsDirectory);
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        foreach (var app in apps) {
            if (!IsSafeSection(app.Section)) {
                continue;
            }

            text = CfgEdit.Apply(text, app.Section, [
                new KeyValuePair<string, string>("ACTIVE", app.Active ? "1" : "0"),
            ]);
        }

        CfgEdit.Commit(path, text);
        return path;
    }

    private static bool? ActiveValue(IniSection section)
    {
        foreach (var pair in section.Pairs) {
            if (!pair.Key.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            return pair.Value.Trim() is "1" or "true" or "TRUE";
        }

        return null;
    }

    private static string DisplayName(string? gameRoot, string section)
    {
        foreach (var folder in Folders(gameRoot)) {
            if (folder.Equals(section, StringComparison.OrdinalIgnoreCase)) {
                return folder;
            }
        }

        return section;
    }

    private static IEnumerable<string> Folders(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            yield break;
        }

        var root = Path.Combine(gameRoot, "apps", "python");
        if (!Directory.Exists(root)) {
            yield break;
        }

        foreach (var directory in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) {
            var name = Path.GetFileName(directory);
            if (name.Equals("system", StringComparison.OrdinalIgnoreCase) || !IsSafeSection(name)) {
                continue;
            }

            yield return name;
        }
    }

    private static bool IsSafeSection(string name) =>
        name.Length > 0
        && name.IndexOfAny(['[', ']', '\r', '\n', '=']) < 0;
}
