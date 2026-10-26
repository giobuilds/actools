using System.Net;
using System.Net.Sockets;

namespace AcManager.Core.Content;

/// <summary>
/// Download addresses published by an online server.
/// Only public http and https URLs are accepted.
/// </summary>
public static class ContentDownloadUrl
{
    public static bool IsAllowed(string? text) => TryCreate(text, out _);

    public static bool TryCreate(string? text, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(text) || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var parsed) || parsed == null) {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo) || string.IsNullOrEmpty(parsed.IdnHost) || parsed.IsLoopback) {
            return false;
        }

        if (IsLocalName(parsed.IdnHost)) {
            return false;
        }

        if (parsed.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6) {
            if (!IPAddress.TryParse(parsed.IdnHost, out var address) || !IsPublicAddress(address)) {
                return false;
            }
        } else if (parsed.HostNameType != UriHostNameType.Dns) {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6) {
            var bytes = address.GetAddressBytes();
            // fc00::/7 unique local.
            return (bytes[0] & 0xFE) != 0xFC;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork) {
            return false;
        }

        var ipv4 = address.GetAddressBytes();
        if (ipv4[0] is 0 or 10 or 127 || ipv4[0] >= 224) {
            return false;
        }

        if (ipv4[0] == 169 && ipv4[1] == 254) {
            return false;
        }

        if (ipv4[0] == 172 && ipv4[1] is >= 16 and <= 31) {
            return false;
        }

        if (ipv4[0] == 192 && ipv4[1] == 168) {
            return false;
        }

        return true;
    }

    private static bool IsLocalName(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        // A pure integer host is a decimal loopback trick, not a DNS name.
        foreach (var c in host) {
            if (!char.IsAsciiDigit(c)) {
                return false;
            }
        }

        return host.Length > 0;
    }
}
