using System.IO.Compression;
using AcManager.App.ViewModels;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class ContentPageTests
{
    [Fact]
    public async Task Install_adds_the_car_and_refreshes_drive_without_clearing_the_session_status()
    {
        var root = Directory.CreateTempSubdirectory("actools-content-ui-").FullName;
        try {
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(game, "content", "cars", "ks_existing", "ui"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks", "ks_silverstone", "ui", "gp"));
            Directory.CreateDirectory(Path.Combine(game, "content", "weather", "3_clear"));
            Directory.CreateDirectory(Path.Combine(game, "content", "weather", "2_light_drizzle"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");
            File.WriteAllText(Path.Combine(game, "content", "cars", "ks_existing", "ui", "ui_car.json"), "{\"name\":\"Existing\"}");
            File.WriteAllText(Path.Combine(game, "content", "tracks", "ks_silverstone", "ui", "ui_track.json"), "{\"name\":\"Silverstone\"}");
            File.WriteAllText(Path.Combine(game, "content", "tracks", "ks_silverstone", "ui", "gp", "ui_track.json"), "{\"name\":\"GP\"}");
            File.WriteAllText(Path.Combine(game, "content", "weather", "3_clear", "weather.ini"), "[LAUNCHER]\r\nNAME=Clear\r\n");
            File.WriteAllText(Path.Combine(game, "content", "weather", "2_light_drizzle", "weather.ini"), "[LAUNCHER]\r\nNAME=Drizzle\r\n");
            var documents = Path.Combine(root, "docs");
            Directory.CreateDirectory(documents);

            var zip = Path.Combine(root, "bonus_car.zip");
            WriteZip(zip, ("ui/ui_car.json", "{\"name\":\"Bonus\"}"), ("data/car.ini", "[CAR]"));

            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
                new RecordingLauncher());

            var refreshes = 0;
            var rescan = window.Content.Installed;
            window.Content.Installed = () => {
                refreshes++;
                rescan?.Invoke();
            };

            window.ShowContentCommand.Execute(null);
            Assert.True(window.IsContentPage);
            window.Drive.SelectedLayout = window.Drive.Layouts.First(item => item.Id == "gp");
            window.Drive.SelectedWeather = window.Drive.Weather.First(item => item.Id == "2_light_drizzle");
            window.Drive.Status = "Assetto Corsa exited.";

            window.Content.ArchivePath = zip;
            await window.Content.InstallCommand.ExecuteAsync(null);

            Assert.Equal(1, refreshes);
            Assert.Contains("Installed car bonus_car.", window.Content.Status, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(game, "content", "cars", "bonus_car", "ui", "ui_car.json")));
            Assert.Contains(window.Drive.FilteredCars, car => car.Id == "bonus_car");
            Assert.Equal("ks_existing", window.Drive.SelectedCar?.Id);
            Assert.Equal("ks_silverstone", window.Drive.SelectedTrack?.Id);
            Assert.Equal("gp", window.Drive.SelectedLayout?.Id);
            Assert.Equal("2_light_drizzle", window.Drive.SelectedWeather?.Id);
            Assert.Equal("Assetto Corsa exited.", window.Drive.Status);

            var marker = Path.Combine(game, "content", "cars", "bonus_car", "marker.txt");
            File.WriteAllText(marker, "keep");
            await window.Content.InstallCommand.ExecuteAsync(null);

            Assert.Equal(1, refreshes);
            Assert.Contains("already exists", window.Content.Status, StringComparison.Ordinal);
            Assert.Equal("keep", File.ReadAllText(marker));
            Assert.Equal("Assetto Corsa exited.", window.Drive.Status);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Install_rejects_a_non_zip_and_a_missing_game_folder()
    {
        var root = Directory.CreateTempSubdirectory("actools-content-reject-").FullName;
        try {
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");
            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, Path.Combine(root, "docs"), null, "Steam")),
                new RecordingLauncher());
            var refreshes = 0;
            window.Content.Installed = () => refreshes++;

            window.Content.ArchivePath = "   ";
            await window.Content.InstallCommand.ExecuteAsync(null);
            Assert.Equal("Choose a .zip file.", window.Content.Status);

            window.Content.ArchivePath = Path.Combine(root, "mod.7z");
            await window.Content.InstallCommand.ExecuteAsync(null);
            Assert.Equal("Only .zip archives can be installed. 7z is not supported yet.", window.Content.Status);
            Assert.Equal(0, refreshes);

            var missing = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config-missing"))),
                new FixedLocator(new AcInstallLayout(null, null, null, "Missing")),
                new RecordingLauncher());
            missing.Content.ArchivePath = Path.Combine(root, "car.zip");
            await missing.Content.InstallCommand.ExecuteAsync(null);
            Assert.Equal("Set the game folder on the Install page before installing content.", missing.Content.Status);
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static void WriteZip(string path, params (string Name, string Text)[] files)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in files) {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(text);
        }
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
        public GameLaunchStatus LaunchSession(AcInstallLayout layout) => GameLaunchStatus.ProtonNotFound;

        public bool IsGameRunning() => false;
    }
}
