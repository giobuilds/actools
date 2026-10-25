using System.Text;

namespace AcTools.Core.Data;

/// <summary>
/// Reader and writer for the ini dialect Assetto Corsa and Content Manager use.
/// Comments start with ';' or '//', except '//' that is part of a URL ("://").
/// </summary>
public sealed class IniFile
{
    private readonly Dictionary<string, IniSection> _sections = new(StringComparer.Ordinal);

    public IEnumerable<string> SectionNames => _sections.Keys;

    public bool Contains(string section) => _sections.ContainsKey(section);

    public IniSection this[string section]
    {
        get {
            if (!_sections.TryGetValue(section, out var existing)) {
                existing = new IniSection();
                _sections[section] = existing;
            }

            return existing;
        }
    }

    public static IniFile Parse(string text)
    {
        var file = new IniFile();
        file.ParseInto(text);
        return file;
    }

    public static IniFile Load(string filename) => Parse(File.ReadAllText(filename));

    public void Save(string filename)
    {
        var directory = Path.GetDirectoryName(filename);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filename, Stringify());
    }

    public string Stringify()
    {
        var builder = new StringBuilder();
        foreach (var pair in _sections) {
            if (pair.Value.Count == 0) {
                continue;
            }

            builder.Append('[');
            builder.Append(pair.Key);
            builder.Append("]\r\n");
            pair.Value.Append(builder);
            builder.Append("\r\n");
        }

        return builder.ToString();
    }

    private void ParseInto(string data)
    {
        IniSection? current = null;
        var started = -1;
        var nonSpace = -1;
        string? key = null;

        for (var i = 0; i < data.Length; i++) {
            var c = data[i];
            switch (c) {
                case '[':
                    Finish(current, data, nonSpace, ref key, ref started);
                    var start = ++i;
                    if (start == data.Length) {
                        break;
                    }

                    for (; i < data.Length && data[i] != ']'; i++) {
                    }

                    current = this[data.Substring(start, i - start)];
                    break;

                case '\n':
                    Finish(current, data, nonSpace, ref key, ref started);
                    break;

                case '=':
                    if (started != -1 && key == null && current != null) {
                        key = data.Substring(started, 1 + nonSpace - started);
                        started = -1;
                    }

                    break;

                case '/':
                    if (i + 1 < data.Length && data[i + 1] == '/' && (i == 0 || data[i - 1] != ':')) {
                        goto case ';';
                    }

                    goto default;

                case ';':
                    Finish(current, data, nonSpace, ref key, ref started);
                    for (i++; i < data.Length && data[i] != '\n'; i++) {
                    }

                    break;

                default:
                    if (!char.IsWhiteSpace(c)) {
                        nonSpace = i;
                        if (started == -1) {
                            started = i;
                        }
                    }

                    break;
            }
        }

        Finish(current, data, nonSpace, ref key, ref started);
    }

    private static void Finish(IniSection? section, string data, int nonSpace, ref string? key, ref int started)
    {
        if (key != null && section != null) {
            string value;
            if (started != -1) {
                var length = 1 + nonSpace - started;
                value = length < 0 ? "" : data.Substring(started, length);
            } else {
                value = "";
            }

            section.SetDirect(key, value);
            key = null;
        }

        started = -1;
    }
}
