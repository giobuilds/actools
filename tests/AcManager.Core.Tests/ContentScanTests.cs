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
    public void Ui_car_json_fields_are_read_for_search()
    {
        var root = Directory.CreateTempSubdirectory("actools-car-json-").FullName;
        try {
            WriteCar(root, "mod_full", """
                {
                  "name": "Full Car '16",
                  "brand": "Mazda",
                  "country": " Japan ",
                  "year": 1991,
                  "parent": "ks_mazda_mx5",
                  "author": "Reboot Team",
                  "tags": ["street", 2],
                  "specs": { "bhp": 180, "torque": "200nm", "weight": "1050kg", "topspeed": 210, "acceleration": "8.5", "pwratio": "6.77" }
                }
                """);
            WriteCar(root, "ks_listed", """{ "name": "Official Cup", "brand": "Ferrari", "author": "Kunos" }""");
            WriteCar(root, "mod_claim", """{ "name": "Claimed", "author": "Kunos" }""");
            WriteCar(root, "mod_kept", """{ "name": "Kept", "author": " Someone " }""");
            WriteCar(root, "mod_cup", """{ "name": "Cup '16" }""");
            WriteCar(root, "mod_future", """{ "name": "Future 2026" }""");
            WriteCar(root, "mod_stringyear", """{ "name": "Old '16", "year": "2005" }""");
            var brokenUi = Path.Combine(root, "mod_broken", "ui");
            Directory.CreateDirectory(brokenUi);
            File.WriteAllText(Path.Combine(brokenUi, "ui_car.json"), """{ "name": "Legacy '99", "brand": "Subaru" """);

            var cars = ContentLibrary.ScanCars(root);
            var full = cars.Single(car => car.Id == "mod_full");
            Assert.Equal(["street", "2"], full.Tags);
            Assert.Equal("Japan", full.Country);
            Assert.Equal(1991, full.Year);
            Assert.Equal("ks_mazda_mx5", full.ParentId);
            Assert.Equal("Reboot Team", full.Author);
            Assert.Equal("180", full.SpecsBhp);
            Assert.Equal("200nm", full.SpecsTorque);
            Assert.Equal("1050kg", full.SpecsWeight);
            Assert.Equal("210", full.SpecsTopSpeed);
            Assert.Equal("8.5", full.SpecsAcceleration);
            Assert.Equal("6.77", full.SpecsPwRatio);

            Assert.Null(cars.Single(car => car.Id == "ks_listed").Author);
            Assert.Null(cars.Single(car => car.Id == "ks_listed").Country);
            Assert.Null(cars.Single(car => car.Id == "mod_claim").Author);
            Assert.Equal("Someone", cars.Single(car => car.Id == "mod_kept").Author);
            Assert.Equal(2016, cars.Single(car => car.Id == "mod_cup").Year);
            Assert.Null(cars.Single(car => car.Id == "mod_future").Year);
            Assert.Equal(2005, cars.Single(car => car.Id == "mod_stringyear").Year);
            Assert.Equal(1999, cars.Single(car => car.Id == "mod_broken").Year);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Name_year_follows_the_content_manager_cutoff()
    {
        Assert.Equal(2016, CarYear.FromName("Cup '16"));
        Assert.Equal(2017, CarYear.FromName("Cup '17"));
        Assert.Equal(1918, CarYear.FromName("Cup '18"));
        Assert.Equal(1967, CarYear.FromName("Cup '67"));
        Assert.Equal(1995, CarYear.FromName("Alfa ’95"));
        Assert.Null(CarYear.FromName("Cup 2016"));
        Assert.Equal(2014, CarYear.FromId("Road Car 2014"));
        Assert.Equal(2019, CarYear.FromId("Road Car 2019"));
        Assert.Equal(1920, CarYear.FromId("Road Car 1920"));
        Assert.Null(CarYear.FromId("Road Car 1919"));
        Assert.Null(CarYear.FromId("Road Car 2026"));
        Assert.Null(CarYear.FromId("Roadx2014"));
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
