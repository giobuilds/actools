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
        var value = ReadValue(gameRoot, documentsDirectory, fileName, "BASIC", "ENABLED");
        if (value == null) {
            return fallback;
        }

        return value.Trim() is "1" or "true" or "TRUE";
    }

    public static string? ReadValue(string? gameRoot, string? documentsDirectory, string fileName, string section, string key)
    {
        if (!string.IsNullOrWhiteSpace(documentsDirectory)) {
            var user = ReadKey(UserPath(documentsDirectory, fileName), section, key);
            if (user != null) {
                return user;
            }
        }

        if (!string.IsNullOrWhiteSpace(gameRoot)) {
            return ReadKey(Path.Combine(gameRoot, "extension", "config", fileName), section, key);
        }

        return null;
    }

    public static string WriteEnabled(string documentsDirectory, string fileName, bool enabled) =>
        Write(documentsDirectory, fileName, [
            new ExtensionEdit("BASIC", [
                new KeyValuePair<string, string>("ENABLED", enabled ? "1" : "0"),
            ]),
        ]);

    public static string Write(string documentsDirectory, string fileName, IReadOnlyList<ExtensionEdit> edits)
    {
        var path = UserPath(documentsDirectory, fileName);
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        foreach (var edit in edits) {
            text = CfgEdit.Apply(text, edit.Section, edit.Values);
        }

        CfgEdit.Commit(path, text);
        return path;
    }

    private static string? ReadKey(string path, string section, string key)
    {
        if (!File.Exists(path)) {
            return null;
        }

        var file = IniFile.Load(path);
        if (!file.Contains(section) || !file[section].Contains(key)) {
            return null;
        }

        return file[section].Get(key);
    }
}

public sealed record ExtensionEdit(string Section, IReadOnlyList<KeyValuePair<string, string>> Values);
