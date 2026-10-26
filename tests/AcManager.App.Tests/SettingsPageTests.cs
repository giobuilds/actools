using AcManager.App.ViewModels;
using AcManager.Core.Settings;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class SettingsPageTests
{
    [Fact]
    public void Publish_loads_cfg_and_save_keeps_unrelated_keys()
    {
        var root = Directory.CreateTempSubdirectory("actools-settings-ui-").FullName;
        try {
            var documents = Path.Combine(root, "Documents", "Assetto Corsa");
            var cfg = Path.Combine(documents, "cfg");
            Directory.CreateDirectory(cfg);
            var assists = AssistSettingsFile.PathFor(documents);
            var audio = AudioSettingsFile.PathFor(documents);
            var controls = ControlSettingsFile.PathFor(documents);
            var python = PythonAppsFile.PathFor(documents);
            var race = Path.Combine(cfg, "race.ini");
            var video = Path.Combine(cfg, "video.ini");
            File.WriteAllText(assists, "[ASSISTS]\r\nIDEAL_LINE=1\r\nSTABILITY_CONTROL=100\t\t\t; 0-100 % of stability control\r\nABS=2\r\nCUSTOM=keep\r\n");
            File.WriteAllText(audio, "[HEADER]\r\nVERSION=1\r\n\r\n[LEVELS]\r\nMASTER=1\r\nWIND=0.4\r\n\r\n[SKIDS]\r\nENTRY_POINT=100\r\n");
            File.WriteAllText(controls, "[HEADER]\r\nINPUT_METHOD=X360\r\n\r\n[CONTROLLERS]\r\nCON0=Logitech G29 Driving Force Racing Wheel\r\n\r\n[STEER]\r\nFF_GAIN=1\r\nLOCK=899\r\n\r\n[GEAR_UP]\r\nBUTTON=12\r\n");
            File.WriteAllText(python, "[CHAT]\r\nACTIVE=1\r\nPOS=1\r\n\r\n[ACNOTIFY]\r\nACTIVE=0\r\n");
            File.WriteAllText(race, "[RACE]\r\nMODEL=stay\r\n");
            File.WriteAllText(video, "[VIDEO]\r\nWIDTH=1920\r\n");

            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "Chat"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "system"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "NewLap"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");

            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
                new RecordingLauncher());

            Assert.Equal("X360", window.Settings.InputMethod);
            Assert.Contains("0  Logitech G29 Driving Force Racing Wheel", window.Settings.Controllers);
            Assert.Equal(100m, window.Settings.ForceFeedback);
            Assert.Equal(899m, window.Settings.SteerLock);
            Assert.Equal(40m, window.Settings.Wind);
            Assert.True(window.Settings.IdealLine);
            Assert.Equal(2, window.Settings.Abs.Value);
            Assert.Equal("On", window.Settings.Abs.Name);
            var chat = Assert.Single(window.Settings.Apps, app => app.Name == "Chat");
            Assert.True(chat.Active);
            var created = Assert.Single(window.Settings.Apps, app => app.Name == "NewLap");
            Assert.False(created.Active);
            Assert.Contains(window.Settings.Apps, app => app.Section == "ACNOTIFY" && !app.Active);
            Assert.DoesNotContain(window.Settings.Apps, app => app.Name.Equals("system", StringComparison.OrdinalIgnoreCase));
            Assert.StartsWith("Using ", window.Settings.Status, StringComparison.Ordinal);

            window.Settings.ForceFeedback = 40;
            window.Settings.SteerLock = 540;
            window.Settings.Wind = 25;
            window.Settings.IdealLine = false;
            window.Settings.Abs = SettingsViewModel.Levels[0];
            chat.Active = false;
            created.Active = true;
            window.Settings.SaveCommand.Execute(null);

            var assistsText = File.ReadAllText(assists);
            Assert.Contains("IDEAL_LINE=0", assistsText, StringComparison.Ordinal);
            Assert.Contains("ABS=0", assistsText, StringComparison.Ordinal);
            Assert.Contains("STABILITY_CONTROL=100\t\t\t; 0-100 % of stability control", assistsText, StringComparison.Ordinal);
            Assert.Contains("CUSTOM=keep", assistsText, StringComparison.Ordinal);
            var audioText = File.ReadAllText(audio);
            Assert.Contains("WIND=0.25", audioText, StringComparison.Ordinal);
            Assert.Contains("VERSION=1", audioText, StringComparison.Ordinal);
            Assert.Contains("ENTRY_POINT=100", audioText, StringComparison.Ordinal);
            var controlsText = File.ReadAllText(controls);
            Assert.Contains("FF_GAIN=0.4", controlsText, StringComparison.Ordinal);
            Assert.Contains("LOCK=540", controlsText, StringComparison.Ordinal);
            Assert.Contains("INPUT_METHOD=X360", controlsText, StringComparison.Ordinal);
            Assert.Contains("CON0=Logitech G29 Driving Force Racing Wheel", controlsText, StringComparison.Ordinal);
            Assert.Contains("BUTTON=12", controlsText, StringComparison.Ordinal);
            var pythonText = File.ReadAllText(python);
            Assert.Contains("ACTIVE=0", pythonText, StringComparison.Ordinal);
            Assert.Contains("POS=1", pythonText, StringComparison.Ordinal);
            Assert.Contains("[NEWLAP]\r\nACTIVE=1", pythonText, StringComparison.Ordinal);
            Assert.Equal("[RACE]\r\nMODEL=stay\r\n", File.ReadAllText(race));
            Assert.Equal("[VIDEO]\r\nWIDTH=1920\r\n", File.ReadAllText(video));
            Assert.StartsWith("Saved ", window.Settings.Status, StringComparison.Ordinal);

            File.WriteAllText(controls, controlsText.Replace("FF_GAIN=0.4", "FF_GAIN=0.2", StringComparison.Ordinal));
            window.ShowSettingsCommand.Execute(null);
            Assert.True(window.IsSettingsPage);
            Assert.Equal(20m, window.Settings.ForceFeedback);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Reload_uses_defaults_when_the_cfg_files_are_missing()
    {
        var root = Directory.CreateTempSubdirectory("actools-settings-missing-").FullName;
        try {
            var documents = Path.Combine(root, "documents");
            var settings = new SettingsViewModel();
            settings.UseLayout(new AcInstallLayout(null, documents, null, "Steam"));

            Assert.Equal("Not set", settings.InputMethod);
            Assert.False(settings.HasControllers);
            Assert.False(settings.HasApps);
            Assert.Equal(100m, settings.ForceFeedback);
            Assert.Equal(900m, settings.SteerLock);
            Assert.Equal(100m, settings.Master);
            Assert.False(settings.IdealLine);
            Assert.Same(SettingsViewModel.Levels[1], settings.Abs);
            Assert.True(settings.CanSave);
        } finally {
            Directory.Delete(root, true);
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
