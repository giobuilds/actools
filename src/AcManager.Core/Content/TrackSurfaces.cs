using System.Globalization;

namespace AcManager.Core.Content;

/// <summary>
/// The original game reads WAV_PITCH with INIReader::getFloat and crashes on anything else.
/// Custom Shaders Patch accepts WAV_PITCH=extended-0. Without that patch the value has to be a number.
/// </summary>
public static class TrackSurfaces
{
    public static string Sanitize(string text, out int changed)
    {
        changed = 0;
        if (text.Length == 0) {
            return text;
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var trailing = normalized.EndsWith('\n');
        var body = trailing ? normalized[..^1] : normalized;
        var lines = body.Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (TryFixLine(lines[i], out var updated)) {
                lines[i] = updated;
                changed++;
            }
        }

        var joined = string.Join(newline, lines);
        return trailing ? joined + newline : joined;
    }

    public static int SanitizeTree(string trackDirectory)
    {
        if (!Directory.Exists(trackDirectory)) {
            return 0;
        }

        var changed = 0;
        foreach (var file in Directory.EnumerateFiles(trackDirectory, "surfaces.ini", SearchOption.AllDirectories)) {
            var parent = Path.GetFileName(Path.GetDirectoryName(file));
            if (!string.Equals(parent, "data", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var text = File.ReadAllText(file);
            var updated = Sanitize(text, out var count);
            if (count == 0) {
                continue;
            }

            var backup = file + ".bak";
            if (!File.Exists(backup)) {
                File.Copy(file, backup);
            }

            File.WriteAllText(file, updated);
            changed += count;
        }

        return changed;
    }

    private static bool TryFixLine(string line, out string updated)
    {
        updated = line;
        var comment = line.IndexOf(';');
        var code = comment < 0 ? line : line[..comment];
        var equals = code.IndexOf('=');
        if (equals < 0) {
            return false;
        }

        if (!code[..equals].Trim().Equals("WAV_PITCH", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var value = code[(equals + 1)..].Trim();
        if (IsFloat(value)) {
            return false;
        }

        var tail = comment < 0 ? "" : line[comment..];
        updated = line[..(equals + 1)] + "0" + tail;
        return true;
    }

    private static bool IsFloat(string value) =>
        value.Length > 0
        && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
