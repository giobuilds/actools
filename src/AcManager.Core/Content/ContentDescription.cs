using System.Net;
using System.Text.RegularExpressions;

namespace AcManager.Core.Content;

/// <summary>
/// HTML-like description text. Copied from <c>AcStringValues.DecodeDescription</c>.
/// </summary>
public static class ContentDescription
{
    private static readonly Regex LineBreak = new(@"<\s*/?\s*br\s*/?\s*>\s*", RegexOptions.Compiled);
    private static readonly Regex Markup = new(@"<\s*\w+(\s+[^>]*|\s*)>|</\s*\w+\s*>|\t", RegexOptions.Compiled);

    public static string? Decode(string? text)
    {
        if (text == null) {
            return null;
        }

        text = LineBreak.Replace(text, "\n");
        text = Markup.Replace(text, "");
        text = WebUtility.HtmlDecode(text).Trim();
        return text.Length == 0 ? null : text;
    }
}
