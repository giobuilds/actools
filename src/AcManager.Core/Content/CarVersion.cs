using System.Text.RegularExpressions;

namespace AcManager.Core.Content;

/// <summary>
/// Trailing version in a car name. Copied from <c>AcStringValues.GetVersionFromName</c>.
/// <c>AcJsonObjectNew.LoadVersionInfo</c> uses it when the JSON has no version.
/// </summary>
public static class CarVersion
{
    private static readonly Regex NameVersion = new(@"^(.+?) (?:[vV](\d+\.\d[\w\.]*))$", RegexOptions.Compiled);

    public static string? FromName(string name, out string nameWithoutVersion)
    {
        ArgumentNullException.ThrowIfNull(name);
        var match = NameVersion.Match(name);
        if (match.Success) {
            nameWithoutVersion = match.Groups[1].Value;
            return match.Groups[2].Value;
        }

        nameWithoutVersion = name;
        return null;
    }
}
