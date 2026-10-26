using System.Globalization;

namespace AcManager.Core.Online;

public static class LobbyAddress
{
    public const int DefaultHttpPort = 8081;

    public static bool TryParse(string? text, out string host, out int httpPort)
    {
        host = "";
        httpPort = 0;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) {
            value = value["http://".Length..];
        }

        var slash = value.IndexOf('/');
        if (slash >= 0) {
            value = value[..slash];
        }

        if (value.Length == 0) {
            return false;
        }

        var colon = value.LastIndexOf(':');
        if (colon > 0 && colon < value.Length - 1
            && int.TryParse(value[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)) {
            host = value[..colon];
            httpPort = port;
        } else if (colon < 0) {
            host = value;
            httpPort = DefaultHttpPort;
        } else {
            return false;
        }

        return IsHost(host) && httpPort is >= 1 and <= 65535;
    }

    public static bool IsHost(string host)
    {
        if (host.Length == 0 || host.Length > 253 || host.StartsWith('.') || host.EndsWith('.') || host.Contains("..", StringComparison.Ordinal)) {
            return false;
        }

        foreach (var c in host) {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '-')) {
                return false;
            }
        }

        return true;
    }
}

public static class SteamIdText
{
    public static bool IsSeventeenDigits(string? id)
    {
        if (id == null || id.Length != 17) {
            return false;
        }

        foreach (var c in id) {
            if (c is < '0' or > '9') {
                return false;
            }
        }

        return true;
    }
}
