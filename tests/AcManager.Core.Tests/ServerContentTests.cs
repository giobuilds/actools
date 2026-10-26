using System.Diagnostics;
using System.Net;
using System.Net.Http;
using AcManager.Core.Content;
using Xunit;

namespace AcManager.Core.Tests;

public class ServerContentTests
{
    [Fact]
    public void Track_folder_id_drops_the_csp_build_gate()
    {
        Assert.Equal("dr_patreon_longbeach", ServerContentJson.TrackFolderId("csp/3424/../dr_patreon_longbeach"));
        Assert.Equal("ks_laguna_seca", ServerContentJson.TrackFolderId("ks_laguna_seca"));
        Assert.Null(ServerContentJson.TrackFolderId("../.."));
    }

    [Fact]
    public void Details_read_car_objects_string_urls_and_the_track()
    {
        const string json =
            "{\"content\":{\"cars\":{"
            + "\"NForce_180sx\":{\"url\":\"https://cdn.example/cars/NForce_180sx.7z\",\"version\":\"1\"},"
            + "\"NForce_s15\":\"https://cdn.example/cars/NForce_s15.7z\""
            + "},\"track\":{\"url\":\"https://cdn.example/tracks/dr_patreon_longbeach.7z\"}}}";

        Assert.True(ServerContentJson.TryRead(json, "csp/3424/../dr_patreon_longbeach", out var offers, out var error));
        Assert.Equal("", error);
        Assert.Equal(
            ["NForce_180sx", "NForce_s15", "dr_patreon_longbeach"],
            offers.Select(offer => offer.Id).ToArray());
        Assert.Equal(InstalledContentKind.Track, offers[2].Kind);
        Assert.Equal("https://cdn.example/tracks/dr_patreon_longbeach.7z", offers[2].Url);
    }

    [Fact]
    public void Details_refuse_a_private_address_password_and_steam_auth()
    {
        const string priv =
            "{\"content\":{\"cars\":{\"NForce_180sx\":\"http://192.168.0.9/car.7z\"}}}";
        Assert.False(ServerContentJson.TryRead(priv, "spa", out _, out var refused));
        Assert.Contains("not a public web address", refused, StringComparison.Ordinal);

        const string password = "{\"content\":{\"password\":true,\"cars\":{\"NForce_180sx\":\"https://cdn.example/a.7z\"}}}";
        Assert.False(ServerContentJson.TryRead(password, "spa", out _, out var locked));
        Assert.Contains("password", locked, StringComparison.OrdinalIgnoreCase);

        const string steam =
            "{\"content\":{\"cars\":{\"NForce_180sx\":{\"url\":\"https://cdn.example/a.7z\",\"steamAuth\":true}}}}";
        Assert.False(ServerContentJson.TryRead(steam, "spa", out _, out var auth));
        Assert.Contains("Steam", auth, StringComparison.Ordinal);

        Assert.False(ContentDownloadUrl.IsAllowed("http://127.0.0.1/car.7z"));
        Assert.False(ContentDownloadUrl.IsAllowed("http://10.1.2.3/car.7z"));
        Assert.False(ContentDownloadUrl.IsAllowed("http://169.254.1.1/car.7z"));
        Assert.False(ContentDownloadUrl.IsAllowed("http://localhost/car.7z"));
        Assert.False(ContentDownloadUrl.IsAllowed("http://user:secret@cdn.example/car.7z"));
        Assert.False(ContentDownloadUrl.IsAllowed("file:///tmp/car.7z"));
        Assert.True(ContentDownloadUrl.IsAllowed("https://cdn.example/cars/NForce_180sx.7z"));
        Assert.True(ContentDownloadUrl.IsPublicAddress(IPAddress.Parse("8.8.8.8")));
        Assert.False(ContentDownloadUrl.IsPublicAddress(IPAddress.Parse("172.16.0.1")));
    }

    [Fact]
    public async Task SevenZip_installs_a_car_and_leaves_csp_surface_values_in_place()
    {
        var root = Directory.CreateTempSubdirectory("actools-7z-").FullName;
        try {
            var game = Game(root);
            var source = Path.Combine(root, "src");
            Directory.CreateDirectory(Path.Combine(source, "dr_patreon_longbeach", "ui"));
            Directory.CreateDirectory(Path.Combine(source, "dr_patreon_longbeach", "data"));
            File.WriteAllText(Path.Combine(source, "dr_patreon_longbeach", "ui", "ui_track.json"), "{\"name\":\"Long Beach\"}");
            const string surfaces = "[SURFACE_0]\r\nWAV_PITCH=extended-0\r\nKEY=ksPerPixelAT_NM_emissive\r\n";
            File.WriteAllText(Path.Combine(source, "dr_patreon_longbeach", "data", "surfaces.ini"), surfaces);
            var archive = Path.Combine(root, "dr_patreon_longbeach.7z");
            Pack(source, archive, "dr_patreon_longbeach");

            using var installer = new ServerContentInstaller(new IdleHandler(), "/usr/bin/7z");
            var first = await installer.InstallLocalAsync(archive, game, "dr_patreon_longbeach", InstalledContentKind.Track, CancellationToken.None);
            var installed = Path.Combine(game, "content", "tracks", "dr_patreon_longbeach", "data", "surfaces.ini");
            Assert.Equal(["dr_patreon_longbeach"], first.Installed.Select(piece => piece.Id).ToArray());
            Assert.Equal(surfaces, File.ReadAllText(installed));
            Assert.False(File.Exists(installed + ".bak"));
            var sanitized = TrackSurfaces.Sanitize(File.ReadAllText(installed), out var changed);
            Assert.True(changed > 0);
            Assert.NotEqual(sanitized, File.ReadAllText(installed));

            File.WriteAllText(installed, surfaces + "KEEP=1\r\n");
            var second = await installer.InstallLocalAsync(archive, game, "dr_patreon_longbeach", InstalledContentKind.Track, CancellationToken.None);
            Assert.Contains("already exists", string.Join(" ", second.Skipped), StringComparison.Ordinal);
            Assert.Contains("KEEP=1", File.ReadAllText(installed), StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SevenZip_names_a_root_archive_and_a_mismatched_folder_from_the_offer()
    {
        var root = Directory.CreateTempSubdirectory("actools-7z-id-").FullName;
        try {
            var game = Game(root);
            var rootCar = Path.Combine(root, "rootcar");
            Directory.CreateDirectory(Path.Combine(rootCar, "ui"));
            Directory.CreateDirectory(Path.Combine(rootCar, "data"));
            File.WriteAllText(Path.Combine(rootCar, "ui", "ui_car.json"), "{\"name\":\"180SX\"}");
            File.WriteAllText(Path.Combine(rootCar, "data", "car.ini"), "id=root");
            var rootArchive = Path.Combine(root, "root.7z");
            Pack(rootCar, rootArchive, "ui", "data");

            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(Path.Combine(nested, "weird", "ui"));
            File.WriteAllText(Path.Combine(nested, "weird", "ui", "ui_car.json"), "{\"name\":\"S15\"}");
            var nestedArchive = Path.Combine(root, "nested.7z");
            Pack(nested, nestedArchive, "weird");

            using var installer = new ServerContentInstaller(new IdleHandler(), "/usr/bin/7z");
            var rooted = await installer.InstallLocalAsync(rootArchive, game, "NForce_180sx", InstalledContentKind.Car, CancellationToken.None);
            var renamed = await installer.InstallLocalAsync(nestedArchive, game, "NForce_s15", InstalledContentKind.Car, CancellationToken.None);

            Assert.Equal("NForce_180sx", Assert.Single(rooted.Installed).Id);
            Assert.Equal("id=root", File.ReadAllText(Path.Combine(game, "content", "cars", "NForce_180sx", "data", "car.ini")));
            Assert.Equal("NForce_s15", Assert.Single(renamed.Installed).Id);
            Assert.True(File.Exists(Path.Combine(game, "content", "cars", "NForce_s15", "ui", "ui_car.json")));
            Assert.False(Directory.Exists(Path.Combine(game, "content", "cars", "weird")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SevenZip_refuses_a_symbolic_link()
    {
        var root = Directory.CreateTempSubdirectory("actools-7z-link-").FullName;
        try {
            var game = Game(root);
            var source = Path.Combine(root, "src");
            Directory.CreateDirectory(Path.Combine(source, "ui"));
            File.WriteAllText(Path.Combine(source, "ui", "ui_car.json"), "{}");
            File.CreateSymbolicLink(Path.Combine(source, "ui", "escape"), "/etc/hostname");
            var archive = Path.Combine(root, "link.7z");
            Pack(source, archive, "ui", symlink: true);

            using var installer = new ServerContentInstaller(new IdleHandler(), "/usr/bin/7z");
            var result = await installer.InstallLocalAsync(archive, game, "NForce_180sx", InstalledContentKind.Car, CancellationToken.None);

            Assert.Empty(result.Installed);
            Assert.Contains("symbolic link", string.Join(" ", result.Skipped), StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(Path.Combine(game, "content", "cars", "NForce_180sx")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Download_extracts_a_public_archive_and_refuses_a_private_redirect()
    {
        var root = Directory.CreateTempSubdirectory("actools-7z-http-").FullName;
        try {
            var game = Game(root);
            var source = Path.Combine(root, "src");
            Directory.CreateDirectory(Path.Combine(source, "ui"));
            Directory.CreateDirectory(Path.Combine(source, "data"));
            File.WriteAllText(Path.Combine(source, "ui", "ui_car.json"), "{}");
            File.WriteAllText(Path.Combine(source, "data", "car.ini"), "ok");
            var archive = Path.Combine(root, "NForce_180sx.7z");
            Pack(source, archive, "ui", "data");

            var ok = new ScriptedHandler(
                Redirect("https://cdn.example/files/NForce_180sx.7z"),
                Bytes(archive));
            using (var installer = new ServerContentInstaller(ok, "/usr/bin/7z")) {
                var result = await installer.InstallAsync(
                    [new ServerContentOffer("NForce_180sx", InstalledContentKind.Car, "https://cdn.example/cars/NForce_180sx.7z")],
                    game,
                    null,
                    CancellationToken.None);
                Assert.Equal("NForce_180sx", Assert.Single(result.Installed).Id);
                Assert.Equal(
                    ["https://cdn.example/cars/NForce_180sx.7z", "https://cdn.example/files/NForce_180sx.7z"],
                    ok.Uris);
            }

            var blocked = new ScriptedHandler(Redirect("http://127.0.0.1/secret.7z"));
            using (var installer = new ServerContentInstaller(blocked, "/usr/bin/7z")) {
                var result = await installer.InstallAsync(
                    [new ServerContentOffer("NForce_s15", InstalledContentKind.Car, "https://cdn.example/cars/NForce_s15.7z")],
                    game,
                    null,
                    CancellationToken.None);
                Assert.Empty(result.Installed);
                Assert.Contains("public web address", string.Join(" ", result.Skipped), StringComparison.Ordinal);
                Assert.Equal(["https://cdn.example/cars/NForce_s15.7z"], blocked.Uris);
                Assert.False(Directory.Exists(Path.Combine(game, "content", "cars", "NForce_s15")));
            }
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Download_stops_for_a_missing_7zip_a_huge_body_and_a_non_archive()
    {
        var root = Directory.CreateTempSubdirectory("actools-7z-guard-").FullName;
        try {
            var game = Game(root);
            var offer = new ServerContentOffer("NForce_180sx", InstalledContentKind.Car, "https://cdn.example/cars/NForce_180sx.7z");
            var idle = new ScriptedHandler();
            using (var missing = new ServerContentInstaller(idle, "/no/such/7z")) {
                var result = await missing.InstallAsync([offer], game, null, CancellationToken.None);
                Assert.Contains("7-Zip", string.Join(" ", result.Skipped), StringComparison.Ordinal);
                Assert.Empty(idle.Uris);
            }

            var huge = new ScriptedHandler(BytesOf(new byte[64]));
            using (var installer = new ServerContentInstaller(huge, "/usr/bin/7z", maxArchiveBytes: 32)) {
                var result = await installer.InstallAsync([offer], game, null, CancellationToken.None);
                Assert.Contains("larger than 2 GB", string.Join(" ", result.Skipped), StringComparison.Ordinal);
                Assert.False(Directory.Exists(Path.Combine(game, "content", "cars", "NForce_180sx")));
            }

            var text = new ScriptedHandler(BytesOf("this is not a 7z archive"u8.ToArray()));
            using (var installer = new ServerContentInstaller(text, "/usr/bin/7z")) {
                var result = await installer.InstallAsync([offer], game, null, CancellationToken.None);
                Assert.Contains("not a 7z archive", string.Join(" ", result.Skipped), StringComparison.Ordinal);
            }
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static string Game(string root)
    {
        var game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
        Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
        File.WriteAllText(Path.Combine(game, "acs.exe"), "");
        return game;
    }

    private static void Pack(string workingDirectory, string archive, params string[] names) =>
        Pack(workingDirectory, archive, names, symlink: false);

    private static void Pack(string workingDirectory, string archive, string name, bool symlink) =>
        Pack(workingDirectory, archive, [name], symlink);

    private static void Pack(string workingDirectory, string archive, string[] names, bool symlink)
    {
        var tool = ServerContentInstaller.FindSevenZip();
        Assert.False(string.IsNullOrEmpty(tool));
        var start = new ProcessStartInfo {
            FileName = tool,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("a");
        start.ArgumentList.Add("-bd");
        start.ArgumentList.Add("-y");
        if (symlink) {
            start.ArgumentList.Add("-snl");
        }

        start.ArgumentList.Add(archive);
        foreach (var name in names) {
            start.ArgumentList.Add(name);
        }

        using var process = Process.Start(start);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(20000));
        var error = stderr.GetAwaiter().GetResult();
        _ = stdout.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, error);
    }

    private static HttpResponseMessage Bytes(string path) => BytesOf(File.ReadAllBytes(path));

    private static HttpResponseMessage BytesOf(byte[] body) =>
        new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(body),
        };

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private sealed class IdleHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public ScriptedHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

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
