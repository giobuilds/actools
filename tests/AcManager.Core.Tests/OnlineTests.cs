using System.Net;
using System.Text;
using AcManager.Core.Online;
using AcTools.Core.Data;
using Xunit;

namespace AcManager.Core.Tests;

public class OnlineTests
{
    private const string ListJson =
        "[{\"ip\":\"1.2.3.4\",\"port\":9000,\"tport\":9600,\"cport\":8081,\"name\":\"Spa Race\",\"clients\":3,\"maxclients\":16,\"track\":\"spa\",\"cars\":[\"ks_mazda_miata\",\"ks_porsche\"],\"pass\":true},"
        + "{\"ip\":\"5.6.7.8\",\"port\":9100,\"tport\":\"9601\",\"cport\":8082,\"name\":\"Quiet\",\"clients\":0,\"maxclients\":10,\"track\":\"imola\",\"cars\":[],\"pass\":false}]";

    [Fact]
    public void Parse_list_uses_tport_as_the_race_port()
    {
        Assert.True(LobbyListJson.TryParseList(ListJson, out var servers, out var error));
        Assert.Equal("", error);
        Assert.Equal(2, servers.Count);
        Assert.Equal("1.2.3.4", servers[0].Ip);
        Assert.Equal(8081, servers[0].HttpPort);
        Assert.Equal(9600, servers[0].RacePort);
        Assert.Equal("Spa Race", servers[0].Name);
        Assert.Equal("spa", servers[0].Track);
        Assert.Equal(3, servers[0].Players);
        Assert.Equal(16, servers[0].Capacity);
        Assert.True(servers[0].PasswordRequired);
        Assert.Equal(["ks_mazda_miata", "ks_porsche"], servers[0].Cars);
        Assert.Equal(9601, servers[1].RacePort);
        Assert.False(servers[1].PasswordRequired);
        Assert.Equal("5.6.7.8:8082", servers[1].Address);
    }

    [Fact]
    public void Parse_rejects_html_and_reads_direct_info()
    {
        Assert.False(LobbyListJson.TryParseList("<html><title>Object moved</title></html>", out _, out var error));
        Assert.Equal("The lobby list was not a server list.", error);
        Assert.False(LobbyListJson.TryParseList("{\"ip\":\"1.2.3.4\"}", out _, out _));

        const string info = "{\"ip\":\"127.0.0.1\",\"port\":9700,\"tport\":9600,\"cport\":8081,\"name\":\"Home\",\"track\":\"monza\",\"clients\":1,\"maxclients\":10,\"pass\":0,\"cars\":[\"ks_mazda_miata\"]}";
        Assert.True(LobbyListJson.TryParseServer(info, "10.1.2.3", 8081, out var server, out var infoError));
        Assert.Equal("", infoError);
        Assert.NotNull(server);
        Assert.Equal("10.1.2.3", server.Ip);
        Assert.Equal(8081, server.HttpPort);
        Assert.Equal(9600, server.RacePort);
        Assert.Equal("monza", server.Track);
        Assert.False(server.PasswordRequired);

        Assert.True(LobbyListJson.TryParseServer("{\"name\":\"No race port\"}", "10.1.2.3", 8081, out var missing, out _));
        Assert.NotNull(missing);
        Assert.Equal(0, missing.RacePort);
    }

    [Theory]
    [InlineData("1.2.3.4:8081", "1.2.3.4", 8081)]
    [InlineData("1.2.3.4", "1.2.3.4", 8081)]
    [InlineData("http://1.2.3.4:9600/INFO", "1.2.3.4", 9600)]
    public void Parse_address_accepts_ip_and_optional_http_port(string text, string host, int port)
    {
        Assert.True(LobbyAddress.TryParse(text, out var parsedHost, out var parsedPort));
        Assert.Equal(host, parsedHost);
        Assert.Equal(port, parsedPort);
    }

    [Fact]
    public void Invite_link_reads_the_ip_and_http_port()
    {
        var invite = InviteLink.TryParse("acmanager://race/online/join?ip=170.39.194.209&httpPort=3722");
        Assert.NotNull(invite);
        Assert.Equal("170.39.194.209", invite.Ip);
        Assert.Equal(3722, invite.HttpPort);
        Assert.Null(invite.Password);

        var withPassword = InviteLink.TryParse("acmanager://race/online/join?ip=10.0.0.8&httpPort=8081&plainPassword=let%20me");
        Assert.NotNull(withPassword);
        Assert.Equal("let me", withPassword.Password);

        Assert.Null(InviteLink.TryParse("https://acstuff.ru/s/q:race/online/join?ip=170.39.194.209&httpPort=3722"));
        Assert.Null(InviteLink.TryParse("acmanager://race/online?ip=170.39.194.209&httpPort=3722"));
        Assert.Null(InviteLink.TryParse("acmanager://race/online/join?ip=170.39.194.209"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2.3.4:0")]
    [InlineData("1.2.3.4:99999")]
    [InlineData("bad host")]
    public void Parse_address_rejects_a_bad_value(string text)
    {
        Assert.False(LobbyAddress.TryParse(text, out _, out _));
    }

    [Fact]
    public void Write_sets_remote_and_leaves_the_local_session()
    {
        var documents = Directory.CreateTempSubdirectory("actools-online-").FullName;
        try {
            var path = Path.Combine(documents, "cfg", "race.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string original =
                "[RACE]\r\nMODEL=stay\r\nTRACK=imola\r\nCUSTOM=keep\r\n\r\n"
                + "[REMOTE]\r\nACTIVE=0\r\nSERVER_IP=old ; public address\r\nSERVER_PORT=1\r\nSERVER_NAME=old name\r\n"
                + "SERVER_HTTP_PORT=9\r\nPASSWORD=secret\r\nTEAM=oldteam\r\nGUID=oldguid\r\nNAME=old\r\n"
                + "REQUESTED_CAR=old_car\r\n__CM_EXTENDED=1\r\n\r\n"
                + "[SESSION_0]\r\nNAME=Practice\r\nTYPE=1\r\n\r\n"
                + "[REPLAY]\r\nFILENAME=keep.acreplay\r\nACTIVE=1\r\n\r\n"
                + "[GHOST_CAR]\r\nENABLED=1\r\n";
            File.WriteAllText(path, original);

            var written = OnlineSessionFile.Write(documents, new OnlineJoin(
                "1.2.3.4", 8081, 9600, "Spa Race", "ks_mazda_miata", "let\nme", "Ada", "76561198000000001"));

            Assert.Equal(path, written);
            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            var text = File.ReadAllText(path);
            var remote = IniFile.Parse(text)["REMOTE"];
            Assert.Equal("1", remote.Get("ACTIVE"));
            Assert.Equal("1.2.3.4", remote.Get("SERVER_IP"));
            Assert.Equal("9600", remote.Get("SERVER_PORT"));
            Assert.Equal("8081", remote.Get("SERVER_HTTP_PORT"));
            Assert.Equal("Spa Race", remote.Get("SERVER_NAME"));
            Assert.Equal("ks_mazda_miata", remote.Get("REQUESTED_CAR"));
            Assert.Equal("letme", remote.Get("PASSWORD"));
            Assert.Equal("Ada", remote.Get("NAME"));
            Assert.Equal("76561198000000001", remote.Get("GUID"));
            Assert.Equal("", remote.Get("TEAM"));
            Assert.Equal("0", remote.Get("__CM_EXTENDED"));
            Assert.Null(remote.Get("__FEATURES"));
            Assert.Contains("SERVER_IP=1.2.3.4 ; public address", text, StringComparison.Ordinal);
            Assert.Contains("MODEL=stay", text, StringComparison.Ordinal);
            Assert.Contains("TRACK=imola", text, StringComparison.Ordinal);
            Assert.Contains("CUSTOM=keep", text, StringComparison.Ordinal);
            Assert.Contains("NAME=Practice", text, StringComparison.Ordinal);
            Assert.Contains("FILENAME=keep.acreplay", text, StringComparison.Ordinal);
            Assert.Contains("[GHOST_CAR]\r\nENABLED=1", text, StringComparison.Ordinal);
            Assert.Equal("0", IniFile.Parse(text)["REPLAY"].Get("ACTIVE"));
            Assert.DoesNotContain("oldteam", text, StringComparison.Ordinal);
            Assert.DoesNotContain("oldguid", text, StringComparison.Ordinal);
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Write_clears_a_previous_guid_and_refuses_a_missing_race_port()
    {
        var documents = Directory.CreateTempSubdirectory("actools-online-empty-").FullName;
        try {
            var path = OnlineSessionFile.Write(documents, new OnlineJoin(
                "1.2.3.4", 8081, 9600, "Spa", "ks_mazda_miata", "", "", "not-a-steam-id"));
            var remote = IniFile.Parse(File.ReadAllText(path))["REMOTE"];
            Assert.Equal("", remote.Get("GUID"));
            Assert.Equal("Player", remote.Get("NAME"));
            Assert.Equal("0", IniFile.Parse(File.ReadAllText(path))["REPLAY"].Get("ACTIVE"));

            Assert.Throws<InvalidOperationException>(() => OnlineSessionFile.Write(documents, new OnlineJoin(
                "1.2.3.4", 8081, 0, "Spa", "ks_mazda_miata", "", "Ada", null)));
            Assert.Equal("9600", IniFile.Parse(File.ReadAllText(path))["REMOTE"].Get("SERVER_PORT"));
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Write_copies_csp_features_and_clears_them_for_the_next_server()
    {
        var documents = Directory.CreateTempSubdirectory("actools-online-features-").FullName;
        try {
            var path = OnlineSessionFile.Write(documents, new OnlineJoin(
                "1.2.3.4",
                8081,
                9600,
                "Spa",
                "ks_mazda_miata",
                "",
                "Ada",
                null,
                true,
                "WEATHERFX_V1, CLIENT_MESSAGES, bad name, WEATHERFX_V1"));
            var remote = IniFile.Parse(File.ReadAllText(path))["REMOTE"];
            Assert.Equal("1", remote.Get("__CM_EXTENDED"));
            Assert.Equal("WEATHERFX_V1,CLIENT_MESSAGES", remote.Get("__FEATURES"));

            OnlineSessionFile.Write(documents, new OnlineJoin(
                "1.2.3.4", 8081, 9600, "Spa", "ks_mazda_miata", "", "Ada", null));
            remote = IniFile.Parse(File.ReadAllText(path))["REMOTE"];
            Assert.Equal("0", remote.Get("__CM_EXTENDED"));
            Assert.Null(remote.Get("__FEATURES"));
            Assert.Equal("1.2.3.4", remote.Get("SERVER_IP"));
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Details_flags_keep_weather_and_feature_names()
    {
        const string json =
            "{\"currentWeatherId\":\"Clear\",\"features\":[\"SPECTATING_AWARE\",\"nope name\",\"CUSTOM_UPDATE\",1,\"CUSTOM_UPDATE\"]}";
        var flags = ServerSessionFlagsJson.Read(json);
        Assert.True(flags.Extended);
        Assert.Equal("SPECTATING_AWARE,CUSTOM_UPDATE", flags.Features);

        var plain = ServerSessionFlagsJson.Read("{\"content\":{}}");
        Assert.False(plain.Extended);
        Assert.Null(plain.Features);
        Assert.False(ServerSessionFlagsJson.Read("not json").Extended);
    }

    [Fact]
    public async Task List_request_skips_a_redirect_and_keeps_a_json_body()
    {
        var handler = new ScriptedHandler(
            Redirect(),
            Json(ListJson));
        using var directory = new HttpLobbyDirectory(handler);
        var result = await directory.LoadListAsync(CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(2, result.Servers.Count);
        Assert.Equal(9600, result.Servers[0].RacePort);
        Assert.Equal(2, handler.Uris.Count);
        Assert.StartsWith(HttpLobbyDirectory.ListUrls[0], handler.Uris[0], StringComparison.Ordinal);
        Assert.StartsWith(HttpLobbyDirectory.ListUrls[1], handler.Uris[1], StringComparison.Ordinal);
        Assert.DoesNotContain("guid=", handler.Uris[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_request_reports_html_and_can_send_a_steam_id()
    {
        var handler = new ScriptedHandler(Json(ListJson));
        const string steamId = "76561198000000001";
        using var directory = new HttpLobbyDirectory(handler, () => steamId);
        var result = await directory.LoadListAsync(CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Contains("guid=" + steamId, handler.Uris[0], StringComparison.Ordinal);

        var html = new ScriptedHandler(Html(), Html(), Html());
        using var failed = new HttpLobbyDirectory(html);
        var failure = await failed.LoadListAsync(CancellationToken.None);
        Assert.False(failure.Ok);
        Assert.Equal("The lobby list was not a server list.", failure.Error);
        Assert.Equal(3, html.Uris.Count);
    }

    [Fact]
    public async Task List_request_rejects_one_repeated_private_address()
    {
        const string placeholder =
            "[{\"ip\":\"192.168.0.1\",\"port\":9000,\"tport\":9600,\"cport\":8081,\"name\":\"A\"},"
            + "{\"ip\":\"192.168.0.1\",\"port\":9000,\"tport\":9600,\"cport\":8081,\"name\":\"B\"}]";
        const string mixed =
            "[{\"ip\":\"192.168.0.1\",\"port\":9000,\"tport\":9600,\"cport\":8081,\"name\":\"Lan\"},"
            + "{\"ip\":\"1.2.3.4\",\"port\":9000,\"tport\":9600,\"cport\":8081,\"name\":\"Real\"}]";
        var rejected = new ScriptedHandler(Json(placeholder), Json(placeholder), Json(placeholder));
        using var failed = new HttpLobbyDirectory(rejected);
        var failure = await failed.LoadListAsync(CancellationToken.None);
        Assert.False(failure.Ok);
        Assert.Contains("placeholder", failure.Error, StringComparison.OrdinalIgnoreCase);

        var accepted = new ScriptedHandler(Json(mixed));
        using var directory = new HttpLobbyDirectory(accepted);
        var result = await directory.LoadListAsync(CancellationToken.None);
        Assert.True(result.Ok);
        Assert.Equal(2, result.Servers.Count);
        Assert.Equal("1.2.3.4", result.Servers[1].Ip);
    }

    [Fact]
    public async Task Direct_info_uses_the_address_that_answered()
    {
        const string info = "{\"ip\":\"127.0.0.1\",\"port\":9700,\"tport\":9600,\"cport\":8081,\"name\":\"Home\",\"track\":\"monza\",\"pass\":false,\"cars\":[\"ks_mazda_miata\"]}";
        var handler = new ScriptedHandler(Json(info));
        using var directory = new HttpLobbyDirectory(handler);
        var result = await directory.LoadServerAsync("10.1.2.3", 8081, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(["http://10.1.2.3:8081/INFO"], handler.Uris);
        Assert.Equal("10.1.2.3", result.Servers[0].Ip);
        Assert.Equal(9600, result.Servers[0].RacePort);
        Assert.Equal(8081, result.Servers[0].HttpPort);

        var idle = new ScriptedHandler();
        using var rejected = new HttpLobbyDirectory(idle);
        var bad = await rejected.LoadServerAsync("bad host", 8081, CancellationToken.None);
        Assert.False(bad.Ok);
        Assert.Empty(idle.Uris);
    }

    [Fact]
    public async Task Details_request_uses_the_http_port_and_can_send_a_steam_id()
    {
        const string steamId = "76561198000000001";
        var handler = new ScriptedHandler(Json("{\"content\":{\"cars\":{}}}"));
        using var directory = new HttpLobbyDirectory(handler, () => steamId);
        var result = await directory.LoadDetailsAsync("170.39.194.209", 3722, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(
            "http://170.39.194.209:3722/api/details?guid=" + steamId,
            handler.Uris[0]);
        Assert.DoesNotContain(steamId, result.Json ?? "", StringComparison.Ordinal);

        var missing = new ScriptedHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        using var failed = new HttpLobbyDirectory(missing, () => steamId);
        var failure = await failed.LoadDetailsAsync("170.39.194.209", 3722, CancellationToken.None);
        Assert.False(failure.Ok);
        Assert.Equal("The server did not publish downloads.", failure.Error);
        Assert.DoesNotContain(steamId, failure.Error, StringComparison.Ordinal);

        var idle = new ScriptedHandler();
        using var rejected = new HttpLobbyDirectory(idle);
        var bad = await rejected.LoadDetailsAsync("bad host", 3722, CancellationToken.None);
        Assert.False(bad.Ok);
        Assert.Empty(idle.Uris);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage Html() =>
        new(HttpStatusCode.OK) {
            Content = new StringContent("<html><title>Object moved</title></html>", Encoding.UTF8, "text/html"),
        };

    private static HttpResponseMessage Redirect()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri("http://lobby.assettocorsa.net/");
        response.Content = new StringContent("<html><title>Object moved</title></html>", Encoding.UTF8, "text/html");
        return response;
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public ScriptedHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> Uris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri == null ? "" : request.RequestUri.ToString());
            if (_responses.Count == 0) {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
