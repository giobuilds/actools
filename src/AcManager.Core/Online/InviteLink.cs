using System.Globalization;

namespace AcManager.Core.Online;

public sealed record OnlineInvite(string Ip, int HttpPort, string? Password);

/// <summary>
/// The invite Content Manager registers as acmanager://race/online/join?ip=&amp;httpPort=.
/// The ACS Drift Join button opens that link.
/// </summary>
public static class InviteLink
{
    public static OnlineInvite? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || !text.StartsWith("acmanager:", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("acmanager", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var path = (uri.Host + uri.AbsolutePath).Trim('/');
        if (!path.Equals("race/online/join", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var query = ParseQuery(uri.Query);
        if (!query.TryGetValue("ip", out var ip) || !LobbyAddress.IsHost(ip)) {
            return null;
        }

        if (!query.TryGetValue("httpPort", out var portText)
            || !int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var httpPort)
            || httpPort is < 1 or > 65535) {
            return null;
        }

        query.TryGetValue("plainPassword", out var password);
        return new OnlineInvite(ip, httpPort, string.IsNullOrEmpty(password) ? null : password);
    }

    public static OnlineInvite? TryParseArgs(IEnumerable<string>? args)
    {
        if (args == null) {
            return null;
        }

        foreach (var arg in args) {
            var invite = TryParse(arg);
            if (invite != null) {
                return invite;
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = query.StartsWith('?') ? query[1..] : query;
        if (text.Length == 0) {
            return result;
        }

        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries)) {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
            var value = Uri.UnescapeDataString(eq < 0 ? "" : part[(eq + 1)..]);
            if (key.Length > 0) {
                result[key] = value;
            }
        }

        return result;
    }
}
