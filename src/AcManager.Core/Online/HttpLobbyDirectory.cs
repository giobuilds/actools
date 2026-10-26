using System.Globalization;
using System.Net;
using System.Net.Http;

namespace AcManager.Core.Online;

/// <summary>
/// Loads the public lobby and a single server's /INFO page.
/// The Windows client named these lobby hosts. A response that is not JSON is reported and the next host is tried.
/// </summary>
public sealed class HttpLobbyDirectory : IOnlineDirectory, IDisposable
{
    public const int MaxBodyChars = 8 * 1024 * 1024;

    public static readonly string[] ListUrls = [
        "http://lobby.assettocorsa.net/lobby.ashx/list",
        "http://93.57.10.21/lobby.ashx/list",
        "http://aclobby1.grecian.net/lobby.ashx/list",
    ];

    private readonly HttpClient _http;
    private readonly Func<string?> _steamId;

    public HttpLobbyDirectory(Func<string?>? steamId = null)
        : this(CreateHandler(), steamId, ownsHandler: true)
    {
    }

    public HttpLobbyDirectory(HttpMessageHandler handler, Func<string?>? steamId = null)
        : this(handler, steamId, ownsHandler: false)
    {
    }

    private HttpLobbyDirectory(HttpMessageHandler handler, Func<string?>? steamId, bool ownsHandler)
    {
        _steamId = steamId ?? (() => null);
        _http = new HttpClient(handler, ownsHandler) {
            Timeout = TimeSpan.FromSeconds(8),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ContentManager");
    }

    public async Task<LobbyFetch> LoadListAsync(CancellationToken cancellationToken)
    {
        var failure = "Could not reach the lobby list.";
        var strong = false;
        foreach (var url in ListUrls) {
            try {
                using var response = await _http.GetAsync(WithSteamId(url), cancellationToken).ConfigureAwait(false);
                if ((int)response.StatusCode is >= 300 and < 400) {
                    Note(ref failure, ref strong, "The lobby list redirected away from the server list.");
                    continue;
                }

                if (!response.IsSuccessStatusCode) {
                    Note(ref failure, ref strong, "The lobby list returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + ".");
                    continue;
                }

                var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                if (body == null || !LobbyListJson.TryParseList(body, out var servers, out _)) {
                    Note(ref failure, ref strong, "The lobby list was not a server list.");
                    continue;
                }

                if (IsPlaceholderList(servers)) {
                    Note(ref failure, ref strong, "The lobby returned a placeholder list. Link Assetto Corsa to the Steam account and refresh.");
                    continue;
                }

                return LobbyFetch.Success(servers);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException) {
                if (!strong) {
                    failure = "Could not reach the lobby list.";
                }
            }
        }

        return LobbyFetch.Failure(failure);
    }

    public async Task<LobbyFetch> LoadServerAsync(string ip, int httpPort, CancellationToken cancellationToken)
    {
        if (!LobbyAddress.IsHost(ip) || httpPort is < 1 or > 65535) {
            return LobbyFetch.Failure("Enter an address as ip:httpPort.");
        }

        var url = "http://" + ip + ":" + httpPort.ToString(CultureInfo.InvariantCulture) + "/INFO";
        try {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400 || !response.IsSuccessStatusCode) {
                return LobbyFetch.Failure("The server did not return session info.");
            }

            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (body == null) {
                return LobbyFetch.Failure("The server did not return session info.");
            }

            if (!LobbyListJson.TryParseServer(body, ip, httpPort, out var server, out var error) || server == null) {
                return LobbyFetch.Failure(string.IsNullOrEmpty(error) ? "The server did not return session info." : error);
            }

            return LobbyFetch.Success([server]);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException) {
            return LobbyFetch.Failure("Could not reach that server.");
        }
    }

    public async Task<DetailsFetch> LoadDetailsAsync(string ip, int httpPort, CancellationToken cancellationToken)
    {
        if (!LobbyAddress.IsHost(ip) || httpPort is < 1 or > 65535) {
            return DetailsFetch.Failure("Enter an address as ip:httpPort.");
        }

        var url = "http://" + ip + ":" + httpPort.ToString(CultureInfo.InvariantCulture) + "/api/details";
        try {
            using var response = await _http.GetAsync(WithSteamId(url), cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400 || !response.IsSuccessStatusCode) {
                return DetailsFetch.Failure("The server did not publish downloads.");
            }

            var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            if (body == null) {
                return DetailsFetch.Failure("The server did not publish downloads.");
            }

            return DetailsFetch.Success(body);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException) {
            return DetailsFetch.Failure("Could not reach that server.");
        }
    }

    public void Dispose() => _http.Dispose();

    private string WithSteamId(string url)
    {
        string? id;
        try {
            id = _steamId();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return url;
        }

        if (!SteamIdText.IsSeventeenDigits(id)) {
            return url;
        }

        return url + "?guid=" + Uri.EscapeDataString(id!);
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var length = response.Content.Headers.ContentLength;
        if (length is > MaxBodyChars) {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return body.Length > MaxBodyChars ? null : body;
    }

    private static void Note(ref string failure, ref bool strong, string message)
    {
        if (!strong) {
            failure = message;
            strong = true;
        }
    }

    private static bool IsPlaceholderList(IReadOnlyList<LobbyServer> servers)
    {
        if (servers.Count == 0 || !servers[0].Ip.StartsWith("192", StringComparison.Ordinal)) {
            return false;
        }

        var ip = servers[0].Ip;
        foreach (var server in servers) {
            if (!string.Equals(server.Ip, ip, StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    private static HttpClientHandler CreateHandler() => new() {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    };
}
