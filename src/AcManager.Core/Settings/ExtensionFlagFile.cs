using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// One ENABLED flag from a Custom Shaders Patch module.
/// The game folder holds the shipped default. The prefix cfg/extension file overrides it.
/// </summary>
public static class ExtensionFlagFile
{
    public static string UserPath(string documentsDirectory, string fileName) =>
        Path.Combine(documentsDirectory, "cfg", "extension", fileName);

    public static bool ReadEnabled(string? gameRoot, string? documentsDirectory, string fileName, bool fallback = true)
    {
        if (!string.IsNullOrWhiteSpace(documentsDirectory)) {
            var user = ReadFile(UserPath(documentsDirectory, fileName));
            if (user != null) {
                return user.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(gameRoot)) {
            var shipped = ReadFile(Path.Combine(gameRoot, "extension", "config", fileName));
            if (shipped != null) {
                return shipped.Value;
            }
        }

        return fallback;
    }

    public static string WriteEnabled(string documentsDirectory, string fileName, bool enabled)
    {
        var path = UserPath(documentsDirectory, fileName);
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        CfgEdit.Commit(path, CfgEdit.Apply(text, "BASIC", [
            new KeyValuePair<string, string>("ENABLED", enabled ? "1" : "0"),
        ]));
        return path;
    }

    private static bool? ReadFile(string path)
    {
        if (!File.Exists(path)) {
            return null;
        }

        var file = IniFile.Load(path);
        if (!file.Contains("BASIC") || !file["BASIC"].Contains("ENABLED")) {
            return null;
        }

        return file["BASIC"].Get("ENABLED")?.Trim() is "1" or "true" or "TRUE";
    }
}
