using AcManager.Core.Settings;
using Xunit;

namespace AcManager.Core.Tests;

public class CspSettingsTests
{
    [Fact]
    public void Read_reports_the_installed_patch_version()
    {
        var game = Directory.CreateTempSubdirectory("actools-csp-info-").FullName;
        try {
            Assert.Equal(PatchInfo.Missing, PatchInfoFile.Read(game));

            File.WriteAllText(Path.Combine(game, "dwrite.dll"), "");
            var config = Path.Combine(game, "extension", "config");
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "data_manifest.ini"), "[VERSION]\r\nSHADERS_PATCH=0.2.11\r\nSHADERS_PATCH_BUILD=3465\r\n");

            var installed = PatchInfoFile.Read(game);
            Assert.True(installed.Installed);
            Assert.Equal("0.2.11", installed.Version);
            Assert.Equal(3465, installed.Build);
            Assert.Equal("0.2.11 (build 3465)", installed.Label);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Write_overrides_weather_fx_in_the_user_file_only()
    {
        var root = Directory.CreateTempSubdirectory("actools-csp-flag-").FullName;
        try {
            var game = Path.Combine(root, "game");
            var documents = Path.Combine(root, "documents");
            var shipped = Path.Combine(game, "extension", "config", "weather_fx.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(shipped)!);
            const string original = "[BASIC]\r\nENABLED=1 ; Active (set to 0 to disable WeatherFX); 1 or 0\r\nCONTROLLER=sol\r\n";
            File.WriteAllText(shipped, original);
            var user = ExtensionFlagFile.UserPath(documents, "weather_fx.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(user)!);
            File.WriteAllText(user, "[AUDIO]\r\nLEVEL=1\r\n");

            Assert.True(ExtensionFlagFile.ReadEnabled(game, documents, "weather_fx.ini"));

            ExtensionFlagFile.WriteEnabled(documents, "weather_fx.ini", false);

            Assert.Equal(original, File.ReadAllText(shipped));
            Assert.Equal("[AUDIO]\r\nLEVEL=1\r\n\r\n[BASIC]\r\nENABLED=0\r\n", File.ReadAllText(user));
            Assert.False(ExtensionFlagFile.ReadEnabled(game, documents, "weather_fx.ini"));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Write_updates_force_feedback_post_processing_and_keeps_the_header()
    {
        var documents = Directory.CreateTempSubdirectory("actools-ffb-").FullName;
        try {
            var path = FfbPostProcessFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(Path.Combine(documents, "cfg", "crazy1.lut"), "0|0\n");
            File.WriteAllText(Path.Combine(documents, "cfg", "other.lut"), "0|0\n");
            const string original = "[HEADER]\r\nVERSION=1\r\nTYPE=GAMMA\r\nENABLED=0\r\n\r\n[GAMMA]\r\nVALUE=0.5\r\n\r\n[LUT]\r\nCURVE=crazy1.lut\r\n";
            File.WriteAllText(path, original);

            var loaded = FfbPostProcessFile.Read(documents);
            Assert.False(loaded.Enabled);
            Assert.Equal(FfbPostProcess.GammaType, loaded.Type);
            Assert.Equal(0.5, loaded.Gamma);
            Assert.Equal("crazy1.lut", loaded.Curve);
            Assert.Equal(["crazy1.lut", "other.lut"], FfbPostProcessFile.Curves(documents));

            FfbPostProcessFile.Write(documents, loaded with { Enabled = true, Type = "lut", Gamma = 3, Curve = "other.lut" });

            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[HEADER]\r\nVERSION=1\r\nTYPE=LUT\r\nENABLED=1\r\n\r\n[GAMMA]\r\nVALUE=2\r\n\r\n[LUT]\r\nCURVE=other.lut\r\n",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Missing_post_process_file_uses_defaults()
    {
        var documents = Directory.CreateTempSubdirectory("actools-ffb-missing-").FullName;
        try {
            Assert.Equal(FfbPostProcess.Default, FfbPostProcessFile.Read(documents));
            FfbPostProcessFile.Write(documents, FfbPostProcess.Default with { Enabled = true, Type = FfbPostProcess.LutType, Gamma = 1.25, Curve = "crazy1.lut" });
            Assert.Equal(
                "[HEADER]\r\nENABLED=1\r\nTYPE=LUT\r\n\r\n[GAMMA]\r\nVALUE=1.25\r\n\r\n[LUT]\r\nCURVE=crazy1.lut\r\n",
                File.ReadAllText(FfbPostProcessFile.PathFor(documents)));
        } finally {
            Directory.Delete(documents, true);
        }
    }
}
