using AcManager.App.ViewModels;
using AcManager.Core.Settings;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class CspPageTests
{
    [Fact]
    public void Publish_loads_csp_and_save_leaves_the_shipped_config()
    {
        var root = Directory.CreateTempSubdirectory("actools-csp-ui-").FullName;
        try {
            var documents = Path.Combine(root, "Documents", "Assetto Corsa");
            var cfg = Path.Combine(documents, "cfg");
            Directory.CreateDirectory(cfg);
            var ffb = FfbPostProcessFile.PathFor(documents);
            var race = Path.Combine(cfg, "race.ini");
            File.WriteAllText(ffb, "[HEADER]\r\nVERSION=1\r\nTYPE=GAMMA\r\nENABLED=0\r\n\r\n[GAMMA]\r\nVALUE=0.5\r\n\r\n[LUT]\r\nCURVE=crazy1.lut\r\n");
            File.WriteAllText(Path.Combine(cfg, "crazy1.lut"), "");
            File.WriteAllText(race, "[RACE]\r\nMODEL=stay\r\n");

            var game = Path.Combine(root, "game");
            var config = Path.Combine(game, "extension", "config");
            Directory.CreateDirectory(config);
            Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");
            File.WriteAllText(Path.Combine(game, "dwrite.dll"), "");
            File.WriteAllText(Path.Combine(config, "data_manifest.ini"), "[VERSION]\r\nSHADERS_PATCH=0.2.11\r\nSHADERS_PATCH_BUILD=3465\r\n");
            const string general = "[BASIC]\r\nENABLED=1 ; Active; 1 or 0\r\n";
            const string weather = "[BASIC]\r\nENABLED=1 ; Active (set to 0 to disable WeatherFX); 1 or 0\r\nCONTROLLER=sol\r\n";
            const string adjustments = "[BASIC]\r\nENABLED=1 ; Active; 1 or 0\r\n\r\n[RENDER_SCALE]\r\nSCALE=1 ; Density\r\n";
            const string grass = "[BASIC]\r\nENABLED=1\r\nQUALITY=2\r\nSKIP_LOADING_OLD_MESHES=1\r\n";
            File.WriteAllText(Path.Combine(config, "general.ini"), general);
            File.WriteAllText(Path.Combine(config, "weather_fx.ini"), weather);
            File.WriteAllText(Path.Combine(config, GraphicsSettings.AdjustmentsFile), adjustments);
            File.WriteAllText(Path.Combine(config, GraphicsSettings.GrassFile), grass);

            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
                new RecordingLauncher());

            Assert.True(window.Csp.Installed);
            Assert.Equal("0.2.11 (build 3465)", window.Csp.VersionLabel);
            Assert.True(window.Csp.PatchEnabled);
            Assert.True(window.Csp.WeatherFxEnabled);
            Assert.True(window.Csp.Adjustments);
            Assert.Equal(100m, window.Csp.PixelDensity);
            Assert.Equal(2, window.Csp.GrassQuality.Value);
            Assert.Equal("Medium", window.Csp.GrassQuality.Name);
            Assert.False(window.Csp.ExtraFx);
            Assert.False(window.Csp.FfbEnabled);
            Assert.Equal(FfbPostProcess.GammaType, window.Csp.FfbMode.Id);
            Assert.Equal(0.5m, window.Csp.Gamma);
            Assert.Equal("crazy1.lut", window.Csp.Curve);
            Assert.StartsWith("Custom Shaders Patch ", window.Csp.Status, StringComparison.Ordinal);

            window.Csp.WeatherFxEnabled = false;
            window.Csp.PixelDensity = 50;
            window.Csp.GrassQuality = CspViewModel.GrassLevels[3];
            window.Csp.ExtraFx = true;
            window.Csp.FfbEnabled = true;
            window.Csp.Gamma = 0.8m;
            window.Csp.SaveCommand.Execute(null);

            Assert.Equal(general, File.ReadAllText(Path.Combine(config, "general.ini")));
            Assert.Equal(weather, File.ReadAllText(Path.Combine(config, "weather_fx.ini")));
            Assert.Equal(adjustments, File.ReadAllText(Path.Combine(config, GraphicsSettings.AdjustmentsFile)));
            Assert.Equal(grass, File.ReadAllText(Path.Combine(config, GraphicsSettings.GrassFile)));
            Assert.Contains("SCALE=0.5", File.ReadAllText(ExtensionFlagFile.UserPath(documents, GraphicsSettings.AdjustmentsFile)), StringComparison.Ordinal);
            Assert.Contains("QUALITY=3", File.ReadAllText(ExtensionFlagFile.UserPath(documents, GraphicsSettings.GrassFile)), StringComparison.Ordinal);
            Assert.Contains("ENABLED=1", File.ReadAllText(ExtensionFlagFile.UserPath(documents, GraphicsSettings.ExtraFxFile)), StringComparison.Ordinal);
            Assert.Equal("[RACE]\r\nMODEL=stay\r\n", File.ReadAllText(race));
            Assert.Contains("ENABLED=0", File.ReadAllText(ExtensionFlagFile.UserPath(documents, CspViewModel.WeatherFile)), StringComparison.Ordinal);
            Assert.Contains("ENABLED=1", File.ReadAllText(ExtensionFlagFile.UserPath(documents, CspViewModel.GeneralFile)), StringComparison.Ordinal);
            var ffbText = File.ReadAllText(ffb);
            Assert.Contains("VERSION=1", ffbText, StringComparison.Ordinal);
            Assert.Contains("ENABLED=1", ffbText, StringComparison.Ordinal);
            Assert.Contains("VALUE=0.8", ffbText, StringComparison.Ordinal);
            Assert.Contains("CURVE=crazy1.lut", ffbText, StringComparison.Ordinal);
            Assert.StartsWith("Saved ", window.Csp.Status, StringComparison.Ordinal);

            File.WriteAllText(ExtensionFlagFile.UserPath(documents, CspViewModel.WeatherFile), "[BASIC]\r\nENABLED=1\r\n");
            window.ShowCspCommand.Execute(null);
            Assert.True(window.IsCspPage);
            Assert.True(window.Csp.WeatherFxEnabled);
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
