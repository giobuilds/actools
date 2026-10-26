using System.Globalization;
using System.Text.RegularExpressions;

namespace AcManager.Core.Content;

/// <summary>
/// Year at the end of a car name. Copied from <c>AcStringValues.GetYearFromName</c> and <c>GetYearFromId</c>.
/// <c>CarObject.LoadYear</c> calls both on the display name.
/// </summary>
public static class CarYear
{
    // Two digits after a space and an apostrophe. 00–17 map to 2000–2017; 18–99 map to 1918–1999.
    private static readonly Regex NameYear = new(@"\s(?:['’](\d\d))$", RegexOptions.Compiled);

    // 1920–2019 at the end of the name, not stuck to a letter or digit.
    private static readonly Regex IdYear = new(@"(?<![a-zA-Z0-9])(?:19[2-9]|20[01])\d$", RegexOptions.Compiled);

    public static int? FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || !char.IsDigit(name[^1])) {
            return null;
        }

        var result = NameYear.Match(name);
        if (!result.Success) {
            return null;
        }

        var matched = result.Groups[1].Value;
        if (string.IsNullOrEmpty(matched)) {
            return null;
        }

        var value = int.Parse(matched, NumberStyles.Any, CultureInfo.InvariantCulture);
        return value < 1000 ? value < 18 ? 2000 + value : 1900 + value : value;
    }

    public static int? FromId(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = IdYear.Match(name);
        if (!result.Success) {
            return null;
        }

        return int.Parse(result.Value, NumberStyles.Any, CultureInfo.InvariantCulture);
    }
}
