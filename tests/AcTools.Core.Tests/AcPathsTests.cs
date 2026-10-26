using AcTools.Utils;
using Xunit;

namespace AcTools.Core.Tests;

public class AcPathsTests
{
    [Fact]
    public void Documents_paths_stay_under_the_given_directory()
    {
        const string documents = "/prefix/drive_c/users/steamuser/Documents/Assetto Corsa";

        Assert.Equal(Path.Combine(documents, "cfg", "race.ini"), AcPaths.GetRaceIniFilename(documents));
        Assert.Equal(Path.Combine(documents, "cfg", "video.ini"), AcPaths.GetCfgVideoFilename(documents));
        Assert.Equal(Path.Combine(documents, "cfg", "controls.ini"), AcPaths.GetCfgControlsFilename(documents));
        Assert.Equal(Path.Combine(documents, "cfg", "python.ini"), AcPaths.GetCfgAppsFilename(documents));
        Assert.Equal(Path.Combine(documents, "cfg", "assists.ini"), AcPaths.GetAssistsIniFilename(documents));
        Assert.Equal(Path.Combine(documents, "cfg", "showroom_start.ini"), AcPaths.GetCfgShowroomFilename(documents));
        Assert.Equal(Path.Combine(documents, "replay"), AcPaths.GetReplaysDirectory(documents));
        Assert.Equal(Path.Combine(documents, "out", "race_out.json"), AcPaths.GetResultJsonFilename(documents));
        Assert.Equal(Path.Combine(documents, "screens"), AcPaths.GetDocumentsScreensDirectory(documents));
        Assert.Equal(Path.Combine(documents, "setups"), AcPaths.GetCarSetupsDirectory(documents));
        Assert.Equal(Path.Combine(documents, "setups", "ks_ferrari_458"), AcPaths.GetCarSetupsDirectory(documents, "ks_ferrari_458"));
        Assert.Equal(Path.Combine(documents, "logs", "log.txt"), AcPaths.GetLogFilename(documents));
        Assert.Equal(Path.Combine(documents, "logs", "custom_shaders_patch.log"), AcPaths.GetLogFilename(documents, "custom_shaders_patch.log"));
        Assert.Equal(
            Path.Combine(documents, "launcherdata", "filestore", "career.ini"),
            AcPaths.GetKunosCareerProgressFilename(documents));
    }

    [Fact]
    public void Game_paths_stay_under_the_game_root()
    {
        const string root = "/steam/steamapps/common/assettocorsa";

        Assert.Equal(Path.Combine(root, "content", "cars"), AcPaths.GetCarsDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "cars", "ks_ferrari_458"), AcPaths.GetCarDirectory(root, "ks_ferrari_458"));
        Assert.Equal(Path.Combine(root, "content", "cars", "ks_ferrari_458", "skins"), AcPaths.GetCarSkinsDirectory(root, "ks_ferrari_458"));
        Assert.Equal(
            Path.Combine(root, "content", "cars", "ks_ferrari_458", "skins", "00_red"),
            AcPaths.GetCarSkinDirectory(root, "ks_ferrari_458", "00_red"));
        Assert.Equal(Path.Combine(root, "content", "tracks"), AcPaths.GetTracksDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "weather"), AcPaths.GetWeatherDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "fonts"), AcPaths.GetFontsDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "showroom"), AcPaths.GetShowroomsDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "showroom", "showroom"), AcPaths.GetShowroomDirectory(root, "showroom"));
        Assert.Equal(Path.Combine(root, "content", "driver"), AcPaths.GetDriverModelsDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "sfx"), AcPaths.GetSfxDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "sfx", "GUIDs.txt"), AcPaths.GetSfxGuidsFilename(root));
        Assert.Equal(Path.Combine(root, "content", "career"), AcPaths.GetKunosCareerDirectory(root));
        Assert.Equal(Path.Combine(root, "content", "gui", "logo_ac_app.png"), AcPaths.GetAcLogoFilename(root));
        Assert.Equal(Path.Combine(root, "content", "gui", "icons"), AcPaths.GetGuiIconsFilename(root));
        Assert.Equal(Path.Combine(root, "system", "cfg"), AcPaths.GetSystemCfgDirectory(root));
        Assert.Equal(Path.Combine(root, "system", "cfg", "ppfilters"), AcPaths.GetPpFiltersDirectory(root));
        Assert.Equal(Path.Combine(root, "apps", "python"), AcPaths.GetPythonAppsDirectory(root));
        Assert.Equal(Path.Combine(root, "AssettoCorsa.exe"), AcPaths.GetAcLauncherFilename(root));
    }

    [Fact]
    public void Is_ac_root_requires_cars_apps_and_an_executable()
    {
        var root = Directory.CreateTempSubdirectory("actools-acpaths-root-").FullName;
        try {
            Assert.False(AcPaths.IsAcRoot(root));

            Directory.CreateDirectory(Path.Combine(root, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(root, "content", "tracks"));
            File.WriteAllBytes(Path.Combine(root, "acs.exe"), []);
            Assert.False(AcPaths.IsAcRoot(root));

            Directory.CreateDirectory(Path.Combine(root, "apps"));
            Assert.True(AcPaths.IsAcRoot(root));

            File.Delete(Path.Combine(root, "acs.exe"));
            Assert.False(AcPaths.IsAcRoot(root));
            File.WriteAllBytes(Path.Combine(root, "acs_pro.exe"), []);
            Assert.True(AcPaths.IsAcRoot(root));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Main_car_filename_reads_a_loose_lods_ini()
    {
        var car = Directory.CreateTempSubdirectory("actools-acpaths-lods-").FullName;
        try {
            Directory.CreateDirectory(Path.Combine(car, "data"));
            File.WriteAllText(Path.Combine(car, "data", "lods.ini"), "[LOD_0]\nFILE=body.kn5\n[LOD_HR]\nFILE=body_hr.kn5\n");
            File.WriteAllBytes(Path.Combine(car, "data.acd"), "encrypted"u8.ToArray());

            Assert.Equal(Path.Combine(car, "body.kn5"), AcPaths.GetMainCarFilename(car, false));
            Assert.Equal(Path.Combine(car, "body_hr.kn5"), AcPaths.GetMainCarFilename(car, true));
        } finally {
            Directory.Delete(car, true);
        }
    }

    [Fact]
    public void Main_car_filename_uses_the_largest_kn5_when_lods_are_missing()
    {
        var car = Directory.CreateTempSubdirectory("actools-acpaths-kn5-").FullName;
        try {
            File.WriteAllBytes(Path.Combine(car, "data.acd"), "encrypted"u8.ToArray());
            File.WriteAllBytes(Path.Combine(car, "small.kn5"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(car, "large.kn5"), [1, 2, 3, 4, 5]);

            Assert.Equal(Path.Combine(car, "large.kn5"), AcPaths.GetMainCarFilename(car, true));
        } finally {
            Directory.Delete(car, true);
        }
    }

    [Fact]
    public void Main_car_filename_rejects_a_missing_car_directory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "actools-acpaths-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => AcPaths.GetMainCarFilename(missing, false));
    }
}
