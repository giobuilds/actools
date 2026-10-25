using AcManager.App.ViewModels;
using AcTools.Core.Data;
using AcTools.Platform;
using Xunit;

namespace AcManager.App.Tests;

public class DriveFlowTests
{
    [Fact]
    public void Detect_save_and_drive_write_a_practice_race_ini()
    {
        var root = Directory.CreateTempSubdirectory("actools-ui-").FullName;
        try {
            var game = WriteGame(Path.Combine(root, "library"));
            var documents = Path.Combine(root, "Documents", "Assetto Corsa");
            Directory.CreateDirectory(documents);
            WriteCar(game, "ks_ferrari_458", "Ferrari 458 Italia", "Ferrari");
            WriteCar(game, "ks_mazda_mx5_cup", "Mazda MX-5 Cup", "Mazda");
            WriteTrack(game, "ks_silverstone", "Silverstone");

            var layout = new AcInstallLayout(game, documents, null, "Steam");
            var launcher = new RecordingLauncher();
            var drive = new DriveViewModel(launcher);
            AcInstallLayout? published = null;
            var install = new InstallViewModel(
                new AcTools.Platform.Linux.InstallSettingsStore(new AcTools.Platform.Linux.XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(layout),
                seen => {
                    published = seen;
                    drive.UseLayout(seen);
                });

            install.DetectCommand.Execute(null);
            Assert.Equal(game, install.GameRoot);
            Assert.Equal(documents, install.DocumentsDirectory);
            Assert.True(install.GameRootValid);
            Assert.True(install.AcsPresent);

            install.SaveCommand.Execute(null);
            Assert.Equal(game, published?.GameRoot);
            Assert.Equal("ks_silverstone", Assert.Single(drive.Tracks).Id);

            Assert.Equal(2, drive.FilteredCars.Count);
            drive.Filter = "mazda";
            var car = Assert.Single(drive.FilteredCars);
            Assert.Equal("ks_mazda_mx5_cup", car.Id);
            Assert.Equal("ks_silverstone", drive.SelectedTrack?.Id);

            Assert.True(drive.DriveCommand.CanExecute(null));
            drive.DriveCommand.Execute(null);

            Assert.Equal(1, launcher.Launches);
            Assert.Equal(game, launcher.Seen?.GameRoot);
            var race = IniFile.Load(Path.Combine(documents, "cfg", "race.ini"));
            Assert.Equal("ks_mazda_mx5_cup", race["RACE"].Get("MODEL"));
            Assert.Equal("ks_silverstone", race["RACE"].Get("TRACK"));
            Assert.Equal("1", race["SESSION_0"].Get("TYPE"));
            Assert.Contains("Could not find Proton", drive.Status, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static string WriteGame(string library)
    {
        var game = Path.Combine(library, "steamapps", "common", "assettocorsa");
        Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
        Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
        File.WriteAllText(Path.Combine(game, "acs.exe"), "");
        return game;
    }

    private static void WriteCar(string game, string id, string name, string brand)
    {
        var car = Path.Combine(game, "content", "cars", id);
        Directory.CreateDirectory(Path.Combine(car, "ui"));
        Directory.CreateDirectory(Path.Combine(car, "skins", "default"));
        File.WriteAllText(Path.Combine(car, "ui", "ui_car.json"), $$"""
            { "name": "{{name}}", "brand": "{{brand}}" }
            """);
        File.WriteAllText(Path.Combine(car, "skins", "default", "ui_skin.json"), """{ "skinname": "Default" }""");
    }

    private static void WriteTrack(string game, string id, string name)
    {
        var track = Path.Combine(game, "content", "tracks", id, "ui");
        Directory.CreateDirectory(track);
        File.WriteAllText(Path.Combine(track, "ui_track.json"), $$"""{ "name": "{{name}}" }""");
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
        public int Launches { get; private set; }

        public AcInstallLayout? Seen { get; private set; }

        public GameLaunchStatus LaunchSession(AcInstallLayout layout)
        {
            Launches++;
            Seen = layout;
            return GameLaunchStatus.ProtonNotFound;
        }

        public bool IsGameRunning() => false;
    }
}
