namespace AcTools.Core.Data;

/// <summary>
/// Edits individual ini keys in the original text.
/// Parsing and saving the whole file would drop comments and the spacing the game wrote.
/// </summary>
public static class IniText
{
    public static string SetValues(string text, string section, IReadOnlyList<KeyValuePair<string, string>> values)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var endsWithNewline = text.EndsWith('\n');
        var lines = text.Length == 0
            ? new List<string>()
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (endsWithNewline && lines.Count > 0 && lines[^1].Length == 0) {
            lines.RemoveAt(lines.Count - 1);
        }

        var output = new List<string>(lines.Count + values.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inSection = false;
        var foundSection = false;
        var headerIndex = -1;

        foreach (var line in lines) {
            var trimmed = line.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']') {
                if (inSection) {
                    InsertMissing(output, headerIndex, values, seen);
                    inSection = false;
                }

                var name = trimmed[1..^1].Trim();
                output.Add(line);
                if (name.Equals(section, StringComparison.OrdinalIgnoreCase)) {
                    inSection = true;
                    foundSection = true;
                    headerIndex = output.Count - 1;
                }

                continue;
            }

            if (inSection && TryReplace(line, values, seen, out var replaced)) {
                output.Add(replaced);
                continue;
            }

            output.Add(line);
        }

        if (inSection) {
            InsertMissing(output, headerIndex, values, seen);
        } else if (!foundSection) {
            if (output.Count > 0 && output[^1].Length > 0) {
                output.Add("");
            }

            output.Add("[" + section + "]");
            foreach (var pair in values) {
                output.Add(pair.Key + "=" + pair.Value);
            }
        }

        var joined = string.Join(newline, output);
        if ((endsWithNewline || text.Length == 0) && output.Count > 0 && !joined.EndsWith('\n')) {
            joined += newline;
        }

        return joined;
    }

    public static string RemoveKeys(string text, string section, IReadOnlyList<string> keys)
    {
        if (string.IsNullOrEmpty(text) || keys.Count == 0) {
            return text ?? "";
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var endsWithNewline = text.EndsWith('\n');
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (endsWithNewline && lines.Count > 0 && lines[^1].Length == 0) {
            lines.RemoveAt(lines.Count - 1);
        }

        var output = new List<string>(lines.Count);
        var inSection = false;
        foreach (var line in lines) {
            var trimmed = line.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']') {
                var name = trimmed[1..^1].Trim();
                inSection = name.Equals(section, StringComparison.OrdinalIgnoreCase);
                output.Add(line);
                continue;
            }

            if (inSection && IsKey(line, keys)) {
                continue;
            }

            output.Add(line);
        }

        var joined = string.Join(newline, output);
        if (endsWithNewline && output.Count > 0 && !joined.EndsWith('\n')) {
            joined += newline;
        }

        return joined;
    }

    private static bool IsKey(string line, IReadOnlyList<string> keys)
    {
        var equals = line.IndexOf('=');
        if (equals <= 0) {
            return false;
        }

        var key = line[..equals].Trim();
        foreach (var name in keys) {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static void InsertMissing(
        List<string> output,
        int headerIndex,
        IReadOnlyList<KeyValuePair<string, string>> values,
        HashSet<string> seen)
    {
        var insertAt = headerIndex + 1;
        foreach (var pair in values) {
            if (!seen.Add(pair.Key)) {
                continue;
            }

            output.Insert(insertAt, pair.Key + "=" + pair.Value);
            insertAt++;
        }
    }

    private static bool TryReplace(
        string line,
        IReadOnlyList<KeyValuePair<string, string>> values,
        HashSet<string> seen,
        out string replaced)
    {
        replaced = line;
        var equals = line.IndexOf('=');
        if (equals <= 0) {
            return false;
        }

        var key = line[..equals].Trim();
        KeyValuePair<string, string>? match = null;
        foreach (var pair in values) {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) {
                match = pair;
                break;
            }
        }

        if (match == null) {
            return false;
        }

        var comment = line.IndexOf(';', equals + 1);
        var valueEnd = comment >= 0 ? comment : line.Length;
        var between = line[(equals + 1)..valueEnd];
        var leading = between.Length - between.TrimStart().Length;
        var trailing = between.Length - between.TrimEnd().Length;
        replaced = line[..(equals + 1)]
            + between[..leading]
            + match.Value.Value
            + (trailing > 0 ? between[^trailing..] : "")
            + (comment >= 0 ? line[comment..] : "");
        seen.Add(match.Value.Key);
        return true;
    }
}
