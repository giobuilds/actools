using AcManager.Core.Content;
using Xunit;
using AcManager.Core.Sessions;
using AcTools.Core.Sessions;
using AcTools.Platform;
using AcTools.Platform.Linux;

namespace AcManager.Core.Tests;

public class LibraryAndInstallTests
{
    [Fact]
    public void Library_vdf_reads_path_entries_and_legacy_numbered_paths()
    {
        const string current = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"/home/keeper/.local/share/Steam"
                    "apps"
                    {
                        "244210"		"42000"
                    }
                }
                "1"
                {
                    "path"		"/mnt/games/SteamLibrary"
                }
            }
            """;

        var paths = SteamLibraryFolders.ReadPaths(current);
        Assert.Equal([
            "/home/keeper/.local/share/Steam",
            "/mnt/games/SteamLibrary",
        ], paths);

        const string legacy = """
            "LibraryFolders"
            {
                "TimeNextStatsReport"		"123"
                "ContentStatsID"		"9"
                "1"		"/mnt/legacy/Steam"
            }
            """;

        Assert.Equal(["/mnt/legacy/Steam"], SteamLibraryFolders.ReadPaths(legacy));
    }

    [Fact]
    public void Discovery_finds_a_proton_prefix_from_libraryfolders()
    {
        var root = Directory.CreateTempSubdirectory("actools-steam-").FullName;
        try {
            var steam = Path.Combine(root, "Steam");
            var library = Path.Combine(root, "Library");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
            WriteGame(library);
            File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
                "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + library + "\"\n\t}\n}\n");

            var layout = SteamAcDiscovery.FindInRoots([steam]);

            Assert.Equal(Path.Combine(library, "steamapps", "common", "assettocorsa"), layout.GameRoot);
            Assert.Equal(Path.Combine(library, "steamapps", "compatdata", "244210", "pfx"), layout.ProtonPrefix);
            Assert.EndsWith(Path.Combine("steamuser", "Documents", "Assetto Corsa"), layout.DocumentsDirectory);
            Assert.True(layout.HasGameRoot);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Saved_game_root_infers_the_prefix_and_overrides_discovery()
    {
        var root = Directory.CreateTempSubdirectory("actools-settings-").FullName;
        try {
            var game = WriteGame(Path.Combine(root, "Library"));
            var store = new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config")));
            store.Save(new InstallSettings { GameRoot = game });

            var layout = store.Resolve(new FixedLocator(new AcInstallLayout(null, null, null, "Steam")));

            Assert.Equal(game, layout.GameRoot);
            Assert.Equal(Path.Combine(root, "Library", "steamapps", "compatdata", "244210", "pfx"), layout.ProtonPrefix);
            Assert.Equal("Saved settings", layout.Source);
            Assert.False(string.IsNullOrWhiteSpace(layout.DocumentsDirectory));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scanner_reads_cars_tracks_and_weather_including_commented_json()
    {
        var root = Directory.CreateTempSubdirectory("actools-content-").FullName;
        try {
            var cars = Path.Combine(root, "cars");
            var car = Path.Combine(cars, "ks_ferrari_458");
            Directory.CreateDirectory(Path.Combine(car, "ui"));
            Directory.CreateDirectory(Path.Combine(car, "skins", "red"));
            File.WriteAllText(Path.Combine(car, "ui", "ui_car.json"), """
                {
                    // kunos file
                    "name": "Ferrari 458 Italia",
                    "brand": "Ferrari",
                    "class": "street",
                }
                """);
            File.WriteAllText(Path.Combine(car, "skins", "red", "ui_skin.json"), """{ "skinname": "Rosso" }""");
            File.WriteAllText(Path.Combine(car, "skins", "red", "preview.jpg"), "not-a-real-jpeg");

            var tracks = Path.Combine(root, "tracks");
            var track = Path.Combine(tracks, "ks_silverstone");
            Directory.CreateDirectory(Path.Combine(track, "ui", "gp"));
            File.WriteAllText(Path.Combine(track, "ui", "ui_track.json"), """{ "name": "Silverstone" }""");
            File.WriteAllText(Path.Combine(track, "ui", "gp", "ui_track.json"), """{ "name": "Grand Prix" }""");

            var weather = Path.Combine(root, "weather", "3_clear");
            Directory.CreateDirectory(weather);
            File.WriteAllText(Path.Combine(weather, "weather.ini"), "[LAUNCHER]\r\nNAME=Clear\r\n");

            var scannedCars = ContentLibrary.ScanCars(cars);
            var scannedTracks = ContentLibrary.ScanTracks(tracks);
            var scannedWeather = ContentLibrary.ScanWeather(Path.Combine(root, "weather"));

            var found = Assert.Single(scannedCars);
            Assert.Equal("Ferrari 458 Italia", found.Name);
            Assert.Equal("Ferrari", found.Brand);
            Assert.Equal("street", found.ClassName);
            var skin = Assert.Single(found.Skins);
            Assert.Equal("red", skin.Id);
            Assert.Equal("Rosso", skin.Name);
            Assert.NotNull(skin.PreviewPath);

            var foundTrack = Assert.Single(scannedTracks);
            Assert.Equal("Silverstone", foundTrack.Name);
            Assert.Equal(["", "gp"], foundTrack.Layouts.Select(layout => layout.Id).ToArray());

            var foundWeather = Assert.Single(scannedWeather);
            Assert.Equal("3_clear", foundWeather.Id);
            Assert.Equal("Clear", foundWeather.Name);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Practice_launch_writes_into_the_layout_documents_directory()
    {
        var documents = Directory.CreateTempSubdirectory("actools-launch-").FullName;
        try {
            var layout = new AcInstallLayout("/tmp/assettocorsa", documents, null, "test");
            var path = PracticeLaunch.WriteRaceIni(layout, new PracticeSession {
                CarId = "ks_mazda_mx5",
                SkinId = "0",
                TrackId = "ks_vallelunga",
                WeatherId = "3_clear",
            });

            Assert.True(File.Exists(path));
            Assert.Contains("TRACK=ks_vallelunga", File.ReadAllText(path), StringComparison.Ordinal);
        } finally {
            Directory.Delete(documents, true);
        }
    }

    private static string WriteGame(string library)
    {
        var game = Path.Combine(library, "steamapps", "common", "assettocorsa");
        Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
        Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
        File.WriteAllText(Path.Combine(game, "acs.exe"), "");
        Directory.CreateDirectory(Path.Combine(library, "steamapps", "compatdata", "244210", "pfx"));
        return game;
    }

    private sealed class FixedLocator : IInstallLocator
    {
        private readonly AcInstallLayout _layout;

        public FixedLocator(AcInstallLayout layout) => _layout = layout;

        public AcInstallLayout Locate() => _layout;

        public IReadOnlyList<string> DefaultSteamRoots => [];
    }
}
