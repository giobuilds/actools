using AcManager.Core.Content;
using Xunit;

namespace AcManager.Core.Tests;

public class ContentScanTests
{
    [Fact]
    public void Cars_skip_temporary_folders_and_official_cars_without_ui_json()
    {
        var root = Directory.CreateTempSubdirectory("actools-cars-").FullName;
        try {
            WriteCar(root, "__cm_tmp_ks_ferrari_458_ab", """{ "name": "Temp" }""");
            var official = Path.Combine(root, "ks_broken");
            Directory.CreateDirectory(official);
            File.WriteAllText(Path.Combine(official, "data.acd"), "packed");
            var loose = Path.Combine(root, "mod_loose");
            Directory.CreateDirectory(Path.Combine(loose, "data"));
            WriteCar(root, "mod_named", """{ "name": "Named Mod" }""");

            var cars = ContentLibrary.ScanCars(root);

            Assert.Equal(["mod_loose", "mod_named"], cars.Select(car => car.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            Assert.Equal("mod_loose", cars.Single(car => car.Id == "mod_loose").Name);
            Assert.Equal("Named Mod", cars.Single(car => car.Id == "mod_named").Name);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Tracks_skip_official_folders_without_ui_json_and_order_layouts()
    {
        var root = Directory.CreateTempSubdirectory("actools-tracks-").FullName;
        try {
            var broken = Path.Combine(root, "ks_missing");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "models.ini"), "");

            var mod = Path.Combine(root, "mod_oval");
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, "models.ini"), "");

            var layouts = Path.Combine(root, "mod_layouts", "ui");
            Directory.CreateDirectory(Path.Combine(layouts, "drift"));
            Directory.CreateDirectory(Path.Combine(layouts, "gp"));
            File.WriteAllText(Path.Combine(layouts, "drift", "ui_track.json"), """{ "name": "Drift" }""");
            File.WriteAllText(Path.Combine(layouts, "gp", "ui_track.json"), """{ "name": "Grand Prix" }""");

            var priority = Path.Combine(root, "mod_priority", "ui");
            Directory.CreateDirectory(priority);
            Directory.CreateDirectory(Path.Combine(priority, "national"));
            Directory.CreateDirectory(Path.Combine(priority, "gp"));
            File.WriteAllText(Path.Combine(priority, "ui_track.json"), """{ "name": "Imola" }""");
            File.WriteAllText(Path.Combine(priority, "national", "ui_track.json"), """{ "name": "Imola National", "priority": 0 }""");
            File.WriteAllText(Path.Combine(priority, "gp", "ui_track.json"), """{ "name": "Imola GP", "priority": 10 }""");

            var tracks = ContentLibrary.ScanTracks(root);

            Assert.DoesNotContain(tracks, track => track.Id == "ks_missing");
            Assert.Equal("", Assert.Single(tracks, track => track.Id == "mod_oval").Layouts[0].Id);

            var layoutTrack = Assert.Single(tracks, track => track.Id == "mod_layouts");
            Assert.Equal(["gp", "drift"], layoutTrack.Layouts.Select(layout => layout.Id).ToArray());
            Assert.Equal("Grand Prix", layoutTrack.Name);

            var imola = Assert.Single(tracks, track => track.Id == "mod_priority");
            Assert.Equal(["", "gp", "national"], imola.Layouts.Select(layout => layout.Id).ToArray());
            Assert.Equal("Imola", imola.Name);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Preferred_layout_follows_the_content_manager_names()
    {
        var layouts = TrackLayoutOrder.Arrange("monza", false, "monza", [
            new TrackLayoutInfo("drift", "Drift", null),
            new TrackLayoutInfo("gp", "Grand Prix", null),
        ]);

        Assert.Equal("gp", layouts[0].Id);
        Assert.Equal("drift", layouts[1].Id);
    }

    [Fact]
    public void Json_priority_outranks_the_name_heuristic()
    {
        var layouts = TrackLayoutOrder.Arrange("monza", false, "monza", [
            new TrackLayoutInfo("gp", "Grand Prix", 0),
            new TrackLayoutInfo("oval", "Oval", 1),
        ]);

        Assert.Equal("oval", layouts[0].Id);
    }

    [Fact]
    public void Common_name_keeps_the_shared_prefix()
    {
        Assert.Equal("Imola", TrackLayoutOrder.CommonName(["Imola GP", "Imola National"]));
        Assert.Null(TrackLayoutOrder.CommonName(["Silverstone", "Grand Prix"]));
    }

    private static void WriteCar(string root, string id, string json)
    {
        var ui = Path.Combine(root, id, "ui");
        Directory.CreateDirectory(ui);
        File.WriteAllText(Path.Combine(ui, "ui_car.json"), json);
    }
}
