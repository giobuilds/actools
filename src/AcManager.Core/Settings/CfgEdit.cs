namespace AcManager.Core.Settings;

internal static class CfgEdit
{
    public static string PathIn(string documentsDirectory, string fileName) =>
        Path.Combine(documentsDirectory, "cfg", fileName);

    public static string Apply(string? text, string section, IReadOnlyList<KeyValuePair<string, string>> values)
    {
        if (string.IsNullOrEmpty(text)) {
            var body = "[" + section + "]\r\n";
            foreach (var pair in values) {
                body += pair.Key + "=" + pair.Value + "\r\n";
            }

            return body;
        }

        return AcTools.Core.Data.IniText.SetValues(text, section, values);
    }

    public static void Commit(string path, string text)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path)) {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        File.WriteAllText(path, text);
    }
}
