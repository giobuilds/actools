using System.Globalization;
using System.Text;

namespace AcTools.Core.Data;

/// <summary>
/// One section of an Assetto Corsa ini file. Keys keep insertion order.
/// Bools are stored as 1 and 0, matching Content Manager.
/// </summary>
public sealed class IniSection
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public int Count => _values.Count;

    public IEnumerable<KeyValuePair<string, string>> Pairs => _values;

    public bool Contains(string key) => _values.ContainsKey(key);

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value)
    {
        if (value == null) {
            return;
        }

        _values[key] = value;
    }

    public void Set(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public void Set(string key, double value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public void Set(string key, double value, string format) =>
        Set(key, value.ToString(format, CultureInfo.InvariantCulture));

    public void Set(string key, bool value) => Set(key, value ? "1" : "0");

    public void SetId(string key, string? value)
    {
        if (value == null) {
            return;
        }

        Set(key, value.ToLowerInvariant());
    }

    internal void SetDirect(string key, string value) => _values[key] = value;

    public void Append(StringBuilder builder)
    {
        foreach (var pair in _values) {
            builder.Append(pair.Key);
            builder.Append('=');
            AppendValue(builder, pair.Value);
            builder.Append("\r\n");
        }
    }

    private static void AppendValue(StringBuilder builder, string value)
    {
        foreach (var c in value) {
            builder.Append(c is '[' or ']' or ';' ? '_' : c);
        }
    }
}
