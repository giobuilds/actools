using AcManager.Core.Content;
using Xunit;

namespace AcManager.Core.Tests;

public class ContentRepairTests
{
    [Fact]
    public void Scan_suggests_loose_file_repairs_and_apply_keeps_the_rest_of_the_ini()
    {
        var root = Directory.CreateTempSubdirectory("actools-repair-").FullName;
        try {
            var game = Game(root);
            var surfaces = Path.Combine(game, "content", "tracks", "cup", "data", "surfaces.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(surfaces)!);
            File.WriteAllText(surfaces, "[SURFACE_0]\r\nWAV_PITCH=extended-0 ; keep\r\nKEY=ROAD\r\n");

            var car = Path.Combine(game, "content", "cars", "loose_car", "data");
            Directory.CreateDirectory(car);
            File.WriteAllText(Path.Combine(car, "car.ini"), "[HEADER]\r\nVERSION=99 ; mod\r\nPOWER=10\r\n");
            File.WriteAllText(Path.Combine(car, "tyres.ini"), "[FRONT]\r\nNAME=Street\r\n[FRONT_1]\r\nNAME=Sport\r\n[COMPOUND_DEFAULT]\r\nINDEX=5\r\n");

            var packed = Path.Combine(game, "content", "cars", "packed_car", "data");
            Directory.CreateDirectory(packed);
            File.WriteAllText(Path.Combine(packed, "car.ini"), "[HEADER]\r\nVERSION=99\r\n");
            File.WriteAllText(Path.Combine(game, "content", "cars", "packed_car", "data.acd"), "packed");

            var fine = Path.Combine(game, "content", "cars", "fine_car", "data");
            Directory.CreateDirectory(fine);
            File.WriteAllText(Path.Combine(fine, "car.ini"), "[HEADER]\r\nVERSION=extended-2\r\n");
            File.WriteAllText(Path.Combine(fine, "tyres.ini"), "[FRONT]\r\nNAME=Street\r\n[COMPOUND_DEFAULT]\r\nINDEX=0\r\n");

            var found = ContentRepairs.Scan(game);
            Assert.Equal(3, found.Count);
            Assert.Contains(found, item => item.Kind == ContentRepairs.WavPitch && item.RelativePath.Contains("cup"));
            Assert.Contains(found, item => item.Kind == ContentRepairs.CarVersion && item.Title.Contains("loose_car"));
            Assert.Contains(found, item => item.Kind == ContentRepairs.TyreIndex && item.Title.Contains("loose_car"));
            Assert.DoesNotContain(found, item => item.Title.Contains("packed_car") || item.Title.Contains("fine_car"));

            foreach (var repair in found) {
                ContentRepairs.Apply(game, repair);
            }

            var surfacesText = File.ReadAllText(surfaces);
            Assert.Contains("WAV_PITCH=0", surfacesText, StringComparison.Ordinal);
            Assert.Contains("; keep", surfacesText, StringComparison.Ordinal);
            Assert.Contains("KEY=ROAD", surfacesText, StringComparison.Ordinal);
            Assert.Contains("WAV_PITCH=extended-0", File.ReadAllText(surfaces + ".bak"), StringComparison.Ordinal);

            var carText = File.ReadAllText(Path.Combine(car, "car.ini"));
            Assert.Contains("VERSION=2 ; mod", carText, StringComparison.Ordinal);
            Assert.Contains("POWER=10", carText, StringComparison.Ordinal);
            Assert.Contains("INDEX=0", File.ReadAllText(Path.Combine(car, "tyres.ini")), StringComparison.Ordinal);
            Assert.Equal("[HEADER]\r\nVERSION=99\r\n", File.ReadAllText(Path.Combine(packed, "car.ini")));
            Assert.Empty(ContentRepairs.Scan(game));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Apply_rejects_a_path_that_leaves_the_game_folder()
    {
        var root = Directory.CreateTempSubdirectory("actools-repair-escape-").FullName;
        try {
            var game = Game(root);
            var repair = new ContentRepair(ContentRepairs.WavPitch, "outside", "outside", "../outside.ini");
            var error = Assert.Throws<InvalidOperationException>(() => ContentRepairs.Apply(game, repair));
            Assert.Contains("outside the game folder", error.Message, StringComparison.Ordinal);
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
}
