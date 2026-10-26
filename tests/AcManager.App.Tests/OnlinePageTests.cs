using AcManager.App.ViewModels;
using AcManager.Core.Content;
using AcManager.Core.Online;
using AcTools.Core.Data;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class OnlinePageTests
{
    private const string FixtureSteamId = "76561198000000001";

    [Fact]
    public async Task Refresh_filters_and_join_writes_the_remote_session()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-ui-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var directory = new FakeDirectory {
                List = LobbyFetch.Success([
                    Server("1.2.3.4", 8081, 9600, 9000, "Spa Race", "spa", true, ["ks_mazda_miata", "ks_porsche"]),
                    Server("5.6.7.8", 8082, 9601, 9100, "Quiet", "imola", false, []),
                ]),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId);

            Assert.Equal(0, directory.ListCalls);
            window.ShowOnlineCommand.Execute(null);
            Assert.True(window.IsOnlinePage);
            Assert.Equal(0, directory.ListCalls);

            await window.Online.RefreshCommand.ExecuteAsync(null);
            Assert.Equal(2, window.Online.FilteredServers.Count);
            Assert.Equal("Spa Race", window.Online.SelectedServer?.Name);
            Assert.Equal("2 servers.", window.Online.Status);

            window.Online.Filter = "imola";
            Assert.Equal(["Quiet"], window.Online.FilteredServers.Select(server => server.Name).ToArray());

            window.Online.Filter = "spa";
            Assert.Equal("Spa Race", window.Online.SelectedServer?.Title);
            Assert.Equal(9600, window.Online.SelectedServer?.RacePort);
            Assert.Equal("ks_mazda_miata", window.Online.SelectedCar?.Id);
            window.Online.Password = "letmein";
            window.Online.DriverName = "Ada";
            await window.Online.JoinCommand.ExecuteAsync(null);

            var text = File.ReadAllText(race);
            var remote = IniFile.Parse(text)["REMOTE"];
            Assert.Equal("1", remote.Get("ACTIVE"));
            Assert.Equal("1.2.3.4", remote.Get("SERVER_IP"));
            Assert.Equal("9600", remote.Get("SERVER_PORT"));
            Assert.Equal("8081", remote.Get("SERVER_HTTP_PORT"));
            Assert.Equal("Spa Race", remote.Get("SERVER_NAME"));
            Assert.Equal("ks_mazda_miata", remote.Get("REQUESTED_CAR"));
            Assert.Equal("letmein", remote.Get("PASSWORD"));
            Assert.Equal("Ada", remote.Get("NAME"));
            Assert.Equal(FixtureSteamId, remote.Get("GUID"));
            Assert.Equal("", remote.Get("TEAM"));
            Assert.Equal("0", remote.Get("__CM_EXTENDED"));
            Assert.Null(remote.Get("__FEATURES"));
            Assert.Contains("MODEL=stay", text, StringComparison.Ordinal);
            Assert.Contains("TRACK=imola", text, StringComparison.Ordinal);
            Assert.Contains("NAME=Practice", text, StringComparison.Ordinal);
            Assert.Contains("FILENAME=keep.acreplay", text, StringComparison.Ordinal);
            Assert.Equal("0", IniFile.Parse(text)["REPLAY"].Get("ACTIVE"));
            Assert.Equal(originalRace, File.ReadAllText(race + ".bak"));
            Assert.Single(launcher.Launches);
            Assert.DoesNotContain(FixtureSteamId, window.Online.Status, StringComparison.Ordinal);
            Assert.Contains("Could not find Proton", window.Online.Status, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task A_failed_list_leaves_race_ini_and_does_not_launch()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-fail-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var before = File.ReadAllText(race);
            var directory = new FakeDirectory {
                List = LobbyFetch.Failure("The lobby list redirected away from the server list."),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId);

            await window.Online.RefreshCommand.ExecuteAsync(null);

            Assert.Equal("The lobby list redirected away from the server list.", window.Online.Status);
            Assert.Empty(window.Online.FilteredServers);
            Assert.Equal(before, File.ReadAllText(race));
            Assert.Empty(launcher.Launches);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Add_by_address_joins_with_the_info_race_port()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-add-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var directory = new FakeDirectory {
                Server = LobbyFetch.Success([
                    Server("10.9.8.7", 8081, 9605, 1111, "Added", "monza", false, ["ks_mazda_miata"]),
                ]),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, null);
            window.Online.Address = "10.9.8.7:8081";

            await window.Online.AddCommand.ExecuteAsync(null);

            Assert.Equal([("10.9.8.7", 8081)], directory.ServerCalls);
            Assert.Equal("Added", window.Online.SelectedServer?.Name);
            Assert.Equal(9605, window.Online.SelectedServer?.RacePort);
            await window.Online.JoinCommand.ExecuteAsync(null);

            var remote = IniFile.Parse(File.ReadAllText(race))["REMOTE"];
            Assert.Equal("10.9.8.7", remote.Get("SERVER_IP"));
            Assert.Equal("9605", remote.Get("SERVER_PORT"));
            Assert.Equal("8081", remote.Get("SERVER_HTTP_PORT"));
            Assert.Equal("", remote.Get("GUID"));
            Assert.Equal("", remote.Get("PASSWORD"));
            Assert.Equal("Player", remote.Get("NAME"));
            Assert.Equal("", window.Online.Password);
            Assert.Contains("No Steam ID was found", window.Online.Status, StringComparison.Ordinal);
            Assert.DoesNotContain("1111", File.ReadAllText(race), StringComparison.Ordinal);
            Assert.Single(launcher.Launches);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Join_uses_the_local_steam_persona_and_leaves_the_password_blank()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-persona-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var directory = new FakeDirectory {
                Server = LobbyFetch.Success([
                    Server("10.9.8.7", 8081, 9605, 1111, "Added", "monza", false, ["ks_mazda_miata"]),
                ]),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId, persona: "Local\nDriver");

            Assert.Equal("LocalDriver", window.Online.DriverName);
            Assert.Equal("", window.Online.Password);
            window.Online.Address = "10.9.8.7:8081";
            await window.Online.AddCommand.ExecuteAsync(null);
            await window.Online.JoinCommand.ExecuteAsync(null);

            var remote = IniFile.Parse(File.ReadAllText(race))["REMOTE"];
            Assert.Equal(FixtureSteamId, remote.Get("GUID"));
            Assert.Equal("LocalDriver", remote.Get("NAME"));
            Assert.Equal("", remote.Get("PASSWORD"));
            Assert.DoesNotContain(FixtureSteamId, window.Online.Status, StringComparison.Ordinal);
            Assert.Single(launcher.Launches);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Invite_link_opens_the_online_page_and_loads_the_server()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-invite-").FullName;
        try {
            PrepareGame(root, out var documents, out var game);
            var directory = new FakeDirectory {
                Server = LobbyFetch.Success([
                    Server("170.39.194.209", 3722, 9810, 9810, "Long Beach", "dr_patreon_longbeach", false, ["NForce_180sx"]),
                ]),
            };
            var window = Open(game, documents, root, directory, new RecordingLauncher(), FixtureSteamId);
            var invite = InviteLink.TryParse("acmanager://race/online/join?ip=170.39.194.209&httpPort=3722");
            Assert.NotNull(invite);

            await window.OpenInviteAsync(invite);

            Assert.True(window.IsOnlinePage);
            Assert.Equal([("170.39.194.209", 3722)], directory.ServerCalls);
            Assert.Equal("Long Beach", window.Online.SelectedServer?.Name);
            Assert.Equal(9810, window.Online.SelectedServer?.RacePort);
            Assert.Contains("None of this server's cars are installed.", window.Online.JoinHint, StringComparison.Ordinal);
            Assert.Contains("Missing track dr_patreon_longbeach.", window.Online.JoinHint, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Install_missing_content_enables_join_without_launching()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-install-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var before = File.ReadAllText(race);
            var directory = new FakeDirectory {
                Server = LobbyFetch.Success([
                    Server(
                        "170.39.194.209",
                        3722,
                        9810,
                        9810,
                        "Long Beach",
                        "csp/3424/../dr_patreon_longbeach",
                        false,
                        ["NForce_180sx"]),
                ]),
                Details = DetailsFetch.Success(
                    "{\"content\":{\"cars\":{\"NForce_180sx\":{\"url\":\"https://cdn.example/cars/NForce_180sx.7z\",\"version\":\"1\"},"
                    + "\"NForce_s15\":\"https://cdn.example/cars/NForce_s15.7z\"},"
                    + "\"track\":{\"url\":\"https://cdn.example/tracks/dr_patreon_longbeach.7z\"}}}"),
            };
            var launcher = new RecordingLauncher();
            var installer = new FakeInstaller(game);
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId, installer);
            window.Online.Address = "170.39.194.209:3722";

            await window.Online.AddCommand.ExecuteAsync(null);

            var guids = Path.Combine(game, "content", "sfx");
            Directory.CreateDirectory(guids);
            File.WriteAllText(
                Path.Combine(guids, "GUIDs.txt"),
                "event:/cars/ks_mazda_miata/engine\nevent:/cars/ks_porsche/engine\n");
            window.Content.Reload();

            Assert.Contains("None of this server's cars are installed.", window.Online.JoinHint, StringComparison.Ordinal);
            Assert.Contains("Missing car NForce_180sx.", window.Online.JoinHint, StringComparison.Ordinal);
            Assert.Contains("Missing track dr_patreon_longbeach.", window.Online.JoinHint, StringComparison.Ordinal);
            Assert.False(window.Online.JoinCommand.CanExecute(null));
            Assert.True(window.Online.InstallMissingCommand.CanExecute(null));
            Assert.Equal(1, directory.DetailsCalls);
            Assert.DoesNotContain(window.Drive.FilteredCars, car => car.Id == "NForce_180sx");
            Assert.DoesNotContain(window.Drive.Tracks, track => track.Id == "dr_patreon_longbeach");
            Assert.DoesNotContain(window.Content.Cars, car => car.Id == "NForce_180sx");
            Assert.DoesNotContain(window.Content.Tracks, track => track.Id == "dr_patreon_longbeach");

            await window.Online.InstallMissingCommand.ExecuteAsync(null);

            Assert.Equal(["NForce_180sx", "dr_patreon_longbeach"], installer.Ids);
            Assert.Equal("NForce_180sx", window.Online.SelectedCar?.Id);
            Assert.Contains(window.Drive.FilteredCars, car => car.Id == "NForce_180sx");
            Assert.Contains(window.Drive.Tracks, track => track.Id == "dr_patreon_longbeach");
            Assert.Contains(window.Drive.FilteredCars, car => car.Id == "ks_mazda_miata");
            Assert.Contains(window.Content.Cars, car => car.Id == "NForce_180sx");
            Assert.Contains(window.Content.Tracks, track => track.Id == "dr_patreon_longbeach");
            Assert.True(window.Online.JoinCommand.CanExecute(null));
            Assert.False(window.Online.InstallMissingCommand.CanExecute(null));
            Assert.Contains("Installed", window.Online.Status, StringComparison.Ordinal);
            Assert.DoesNotContain("Checking this server", window.Online.JoinHint, StringComparison.Ordinal);
            Assert.Empty(launcher.Launches);
            Assert.Equal(before, File.ReadAllText(race));
            Assert.DoesNotContain(FixtureSteamId, window.Online.Status, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Join_writes_csp_features_from_the_server_details()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-features-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var directory = new FakeDirectory {
                List = LobbyFetch.Success([
                    Server("64.74.160.34", 1162, 1376, 1162, "Shadow Valley", "sr_shadowvalley", false, ["ks_mazda_miata"]),
                ]),
                Details = DetailsFetch.Success(
                    "{\"currentWeatherId\":\"Clear\",\"features\":[\"WEATHERFX_V1\",\"CLIENT_MESSAGES\",\"bad name\"],\"content\":{}}"),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId);

            await window.Online.RefreshCommand.ExecuteAsync(null);
            await window.Online.JoinCommand.ExecuteAsync(null);

            var remote = IniFile.Parse(File.ReadAllText(race))["REMOTE"];
            Assert.Equal("1", remote.Get("__CM_EXTENDED"));
            Assert.Equal("WEATHERFX_V1,CLIENT_MESSAGES", remote.Get("__FEATURES"));
            Assert.Equal(FixtureSteamId, remote.Get("GUID"));
            Assert.Equal("", remote.Get("PASSWORD"));
            Assert.DoesNotContain(FixtureSteamId, window.Online.Status, StringComparison.Ordinal);
            Assert.DoesNotContain("WEATHERFX_V1", window.Online.Status, StringComparison.Ordinal);
            Assert.Single(launcher.Launches);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Join_stays_disabled_when_the_race_port_is_missing()
    {
        var root = Directory.CreateTempSubdirectory("actools-online-norace-").FullName;
        try {
            var race = PrepareGame(root, out var documents, out var game);
            var before = File.ReadAllText(race);
            var directory = new FakeDirectory {
                List = LobbyFetch.Success([
                    Server("8.8.8.8", 8081, 0, 9000, "No race", "spa", false, ["ks_mazda_miata"]),
                ]),
            };
            var launcher = new RecordingLauncher();
            var window = Open(game, documents, root, directory, launcher, FixtureSteamId);

            await window.Online.RefreshCommand.ExecuteAsync(null);

            Assert.False(window.Online.JoinCommand.CanExecute(null));
            await window.Online.JoinCommand.ExecuteAsync(null);
            Assert.Equal(before, File.ReadAllText(race));
            Assert.Empty(launcher.Launches);
            Assert.Contains("no race port", window.Online.JoinHint, StringComparison.OrdinalIgnoreCase);
        } finally {
            Directory.Delete(root, true);
        }
    }

    private const string originalRace =
        "[RACE]\r\nMODEL=stay\r\nTRACK=imola\r\n\r\n"
        + "[REMOTE]\r\nACTIVE=0\r\nSERVER_IP=old\r\nSERVER_PORT=1\r\nPASSWORD=secret\r\nTEAM=oldteam\r\n"
        + "GUID=oldguid\r\n__CM_EXTENDED=1\r\n\r\n"
        + "[SESSION_0]\r\nNAME=Practice\r\nTYPE=1\r\n\r\n"
        + "[REPLAY]\r\nFILENAME=keep.acreplay\r\nACTIVE=1\r\n\r\n"
        + "[GHOST_CAR]\r\nENABLED=1\r\n";

    private static string PrepareGame(string root, out string documents, out string game)
    {
        documents = Path.Combine(root, "Documents", "Assetto Corsa");
        var cfg = Path.Combine(documents, "cfg");
        Directory.CreateDirectory(cfg);
        var race = Path.Combine(cfg, "race.ini");
        File.WriteAllText(race, originalRace);
        game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "content", "cars", "ks_mazda_miata", "data"));
        Directory.CreateDirectory(Path.Combine(game, "content", "cars", "ks_porsche", "data"));
        Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
        File.WriteAllText(Path.Combine(game, "acs.exe"), "");
        return race;
    }

    private static MainWindowViewModel Open(
        string game,
        string documents,
        string root,
        FakeDirectory directory,
        RecordingLauncher launcher,
        string? steamId,
        FakeInstaller? installer = null,
        string? persona = null)
    {
        return new MainWindowViewModel(
            new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
            new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
            launcher,
            servers: directory,
            steamIds: new FakeSteam(steamId, persona),
            contentInstaller: installer);
    }

    private static LobbyServer Server(
        string ip,
        int httpPort,
        int racePort,
        int ignoredLobbyPort,
        string name,
        string track,
        bool password,
        string[] cars)
    {
        _ = ignoredLobbyPort;
        return new LobbyServer(ip, httpPort, racePort, name, track, 2, 16, password, cars);
    }

    private sealed class FakeDirectory : IOnlineDirectory
    {
        public LobbyFetch List { get; set; } = LobbyFetch.Success([]);

        public LobbyFetch Server { get; set; } = LobbyFetch.Failure("The server did not return session info.");

        public DetailsFetch Details { get; set; } = DetailsFetch.Success("{\"content\":{}}");

        public int ListCalls { get; private set; }

        public int DetailsCalls { get; private set; }

        public List<(string Ip, int Port)> ServerCalls { get; } = [];

        public Task<LobbyFetch> LoadListAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult(List);
        }

        public Task<LobbyFetch> LoadServerAsync(string ip, int httpPort, CancellationToken cancellationToken)
        {
            ServerCalls.Add((ip, httpPort));
            return Task.FromResult(Server);
        }

        public Task<DetailsFetch> LoadDetailsAsync(string ip, int httpPort, CancellationToken cancellationToken)
        {
            DetailsCalls++;
            return Task.FromResult(Details);
        }
    }

    private sealed class FakeInstaller : IServerContentInstaller
    {
        private readonly string _gameRoot;

        public FakeInstaller(string gameRoot) => _gameRoot = gameRoot;

        public List<string> Ids { get; } = [];

        public Task<ContentInstallResult> InstallAsync(
            IReadOnlyList<ServerContentOffer> offers,
            string gameRoot,
            Action<ServerContentProgress>? progress,
            CancellationToken cancellationToken)
        {
            Assert.Equal(_gameRoot, gameRoot);
            var installed = new List<InstalledPiece>();
            var index = 0;
            foreach (var offer in offers) {
                index++;
                progress?.Invoke(new ServerContentProgress(index, offers.Count, offer.Id, "Downloading"));
                Ids.Add(offer.Id);
                if (offer.Kind == InstalledContentKind.Car) {
                    Directory.CreateDirectory(Path.Combine(gameRoot, "content", "cars", offer.Id, "data"));
                } else {
                    var ui = Path.Combine(gameRoot, "content", "tracks", offer.Id, "ui");
                    Directory.CreateDirectory(ui);
                    File.WriteAllText(Path.Combine(ui, "ui_track.json"), "{}");
                }

                installed.Add(new InstalledPiece(offer.Kind == InstalledContentKind.Car ? "car" : "track", offer.Id, offer.Id));
            }

            return Task.FromResult(new ContentInstallResult(installed, []));
        }
    }

    private sealed class FakeSteam : ISteamIdLookup
    {
        private readonly string? _id;
        private readonly string? _persona;

        public FakeSteam(string? id, string? persona = null)
        {
            _id = id;
            _persona = persona;
        }

        public string? MostRecentId() => _id;

        public string? PersonaName() => _persona;
    }

    private sealed class FixedLocator : IInstallLocator
    {
        private readonly AcInstallLayout _layout;

        public FixedLocator(AcInstallLayout layout) => _layout = layout;

        public AcInstallLayout Locate() => _layout;

        public IReadOnlyList<string> DefaultSteamRoots => ["test-root"];
    }

    private sealed class RecordingLauncher : IGameLauncher
    {
        public List<AcInstallLayout> Launches { get; } = [];

        public GameLaunchStatus LaunchSession(AcInstallLayout layout)
        {
            Launches.Add(layout);
            return GameLaunchStatus.ProtonNotFound;
        }

        public bool IsGameRunning() => false;
    }
}
