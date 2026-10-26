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
    public void Guids_file_marks_the_listed_car_as_kunos()
    {
        var game = Directory.CreateTempSubdirectory("actools-guids-").FullName;
        try {
            var cars = Path.Combine(game, "content", "cars");
            WriteCar(cars, "ks_cup", """{ "name": "Cup", "author": "Someone" }""");
            WriteCar(cars, "mod_claim", """{ "name": "Claim", "author": "Kunos" }""");
            WriteCar(cars, "ks_short", """{ "name": "Short" }""");
            WriteCar(cars, "KS_Mixed", """{ "name": "Mixed" }""");
            Directory.CreateDirectory(Path.Combine(game, "content", "sfx"));
            File.WriteAllText(
                Path.Combine(game, "content", "sfx", "GUIDs.txt"),
                """
                {00000000-0000-0000-0000-000000000001} event:/cars/ks_cup/engine
                not a car line
                {00000000-0000-0000-0000-000000000002} event:/cars/ks_short
                {00000000-0000-0000-0000-000000000003} event:/cars/KS_Mixed/engine
                """);

            var scanned = ContentLibrary.ScanCars(cars, OfficialCarIds.FromGameRoot(game));

            Assert.Equal(CarAuthors.Kunos, scanned.Single(car => car.Id == "ks_cup").Author);
            Assert.Null(scanned.Single(car => car.Id == "mod_claim").Author);
            Assert.Null(scanned.Single(car => car.Id == "ks_short").Author);
            Assert.Null(scanned.Single(car => car.Id == "KS_Mixed").Author);
            Assert.Empty(OfficialCarIds.FromGameRoot(Path.Combine(game, "missing")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Skins_follow_the_content_manager_id_order()
    {
        var root = Directory.CreateTempSubdirectory("actools-skins-").FullName;
        try {
            WriteCar(root, "mod_skins", """{ "name": "Skins" }""");
            var skins = Path.Combine(root, "mod_skins", "skins");
            foreach (var id in new[] { "skin_10", "skin_2", "02_white", "2_red" }) {
                Directory.CreateDirectory(Path.Combine(skins, id));
            }

            var car = Assert.Single(ContentLibrary.ScanCars(root));
            Assert.Equal(["2_red", "02_white", "skin_2", "skin_10"], car.Skins.Select(skin => skin.Id).ToArray());
            Assert.Equal("2_red", car.DefaultSkin?.Id);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Alphanum_order_matches_the_content_manager_cases()
    {
        AssertBefore("abc", "def");
        AssertBefore("abc", "abcc");
        AssertBefore("17-abc", "17-abcc");
        AssertBefore("17-abcc", "17-abcd");
        AssertBefore("ab99c", "abc0c");
        AssertBefore("ab99c", "abc0000c");
        AssertBefore("test-5-q", "test-15-q");
        AssertBefore("test-005-q", "test-15-q");
        AssertBefore("test-15-q", "test-25-q");
        Assert.Equal(0, AlphanumOrder.Compare("test-15-q", "test-15-q"));
        AssertBefore("test-15-q-7", "test-15-q-14");
        AssertBefore("test-15-q-14", "test-15-q-70");
        AssertBefore(
            "AC-01 (www.assetto-fr.tk) French Nordschleife Endurance (SOL) (KMR)x:xdn566",
            "AC-02 (www.assetto-fr.tk) French GT2 (Track Rotation) (SOL) (KMR)x:VAJezh");
        AssertBefore(
            "AC-02 (www.assetto-fr.tk) French GT2 (Track Rotation) (SOL) (KMR)x:VAJezh",
            "AC-03 (www.assetto-fr.tk) French GT3 (Track Rotation) (SOL) (KMR)x:JdWnGb");
    }

    private static void AssertBefore(string earlier, string later)
    {
        Assert.True(AlphanumOrder.Compare(earlier, later) < 0);
        Assert.True(AlphanumOrder.Compare(later, earlier) > 0);
    }

    [Fact]
    public void A_trailing_version_leaves_the_car_name()
    {
        var root = Directory.CreateTempSubdirectory("actools-car-version-").FullName;
        try {
            WriteCar(root, "mod_version", """{ "name": "  Cup Car v1.2.3  " }""");
            WriteCar(root, "mod_locked", """{ "name": "Locked v9.9", "version": "2" }""");
            WriteCar(root, "mod_number", """{ "name": "Numbered v9.9", "version": 1.5 }""");
            WriteCar(root, "mod_plain", """{ "name": "Plain v1" }""");
            WriteCar(root, "mod_year", """{ "name": "Heritage '16 v2.0" }""");

            var cars = ContentLibrary.ScanCars(root);
            var versioned = cars.Single(car => car.Id == "mod_version");
            Assert.Equal("Cup Car", versioned.Name);
            Assert.Equal("1.2.3", versioned.Version);

            var locked = cars.Single(car => car.Id == "mod_locked");
            Assert.Equal("Locked v9.9", locked.Name);
            Assert.Equal("2", locked.Version);

            var numbered = cars.Single(car => car.Id == "mod_number");
            Assert.Equal("Numbered v9.9", numbered.Name);
            Assert.Equal("1.5", numbered.Version);

            var plain = cars.Single(car => car.Id == "mod_plain");
            Assert.Equal("Plain v1", plain.Name);
            Assert.Null(plain.Version);

            var year = cars.Single(car => car.Id == "mod_year");
            Assert.Equal("Heritage '16", year.Name);
            Assert.Equal("2.0", year.Version);
            Assert.Null(year.Year);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_trailing_version_leaves_the_track_name()
    {
        var root = Directory.CreateTempSubdirectory("actools-track-version-").FullName;
        try {
            WriteTrack(root, "mod_spa", """{ "name": "  Spa v1.2.3  " }""");
            WriteTrack(root, "mod_locked", """{ "name": "Locked v9.9", "version": "2" }""");
            WriteTrack(root, "mod_number", """{ "name": "Numbered v9.9", "version": 1.5 }""");
            WriteTrack(root, "mod_plain", """{ "name": "Plain v1" }""");
            WriteTrack(root, "mod_sepang", """{ "name": "Sepang 2026" }""");
            WriteTrack(root, "mod_cup", """{ "name": "Cup Circuit v1.2" }""");
            WriteLayout(root, "mod_cup", "gp", """{ "name": "Cup Circuit GP v1.2" }""");
            WriteLayout(root, "mod_brands", "gp", """{ "name": "Brands Hatch v1.0" }""");
            WriteLayout(root, "mod_brands", "club", """{ "name": "Brands Hatch v2.0" }""");

            var tracks = ContentLibrary.ScanTracks(root);
            var spa = tracks.Single(track => track.Id == "mod_spa");
            Assert.Equal("Spa", spa.Name);
            Assert.Equal("", spa.Layouts[0].Id);
            Assert.Equal("Spa", spa.Layouts[0].Name);
            Assert.Equal("1.2.3", spa.Layouts[0].Version);

            var locked = tracks.Single(track => track.Id == "mod_locked");
            Assert.Equal("Locked v9.9", locked.Name);
            Assert.Equal("2", locked.Layouts[0].Version);

            var numbered = tracks.Single(track => track.Id == "mod_number");
            Assert.Equal("Numbered v9.9", numbered.Name);
            Assert.Equal("1.5", numbered.Layouts[0].Version);

            var plain = tracks.Single(track => track.Id == "mod_plain");
            Assert.Equal("Plain v1", plain.Name);
            Assert.Null(plain.Layouts[0].Version);

            Assert.Equal("Sepang 2026", tracks.Single(track => track.Id == "mod_sepang").Name);

            var cup = tracks.Single(track => track.Id == "mod_cup");
            Assert.Equal("Cup Circuit", cup.Name);
            Assert.Equal(["", "gp"], cup.Layouts.Select(layout => layout.Id).ToArray());
            Assert.Equal("Cup Circuit", cup.Layouts[0].Name);
            Assert.Equal("1.2", cup.Layouts[0].Version);
            Assert.Equal("Cup Circuit GP", cup.Layouts[1].Name);
            Assert.Equal("1.2", cup.Layouts[1].Version);

            var brands = tracks.Single(track => track.Id == "mod_brands");
            Assert.Equal("Brands Hatch", brands.Name);
            Assert.Equal(["gp", "club"], brands.Layouts.Select(layout => layout.Id).ToArray());
            Assert.Equal("Brands Hatch", brands.Layouts[0].Name);
            Assert.Equal("1.0", brands.Layouts[0].Version);
            Assert.Equal("2.0", brands.Layouts[1].Version);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_track_description_can_carry_the_version()
    {
        var root = Directory.CreateTempSubdirectory("actools-track-description-").FullName;
        try {
            WriteTrack(root, "mod_notes", """{ "name": "Spa", "description": "Layout notes v1.4" }""");
            WriteTrack(root, "mod_locked", """{ "name": "Spa", "version": "2", "description": "Notes v9.9" }""");
            WriteTrack(root, "mod_named", """{ "name": "Spa v1.2", "description": "Notes v9.9" }""");
            WriteTrack(root, "mod_html", """{ "name": "Html", "description": "<b>Fast circuit</b> v3.0<br>" }""");
            WriteTrack(root, "mod_entity", """{ "name": "Entity", "description": "Fish &amp; chips v1.0" }""");
            WriteTrack(root, "mod_plain", """{ "name": "Plain", "description": "Just a circuit" }""");
            WriteTrack(root, "mod_lines", """{ "name": "Lines", "description": "Line one\nLine two v1.2" }""");
            WriteTrack(root, "mod_tab", """{ "name": "Tab", "description": "Fast\tcircuit v1.2" }""");
            WriteTrack(root, "mod_blank", """{ "name": "Blank", "description": "<br>" }""");
            WriteTrack(root, "mod_cup", """{ "name": "Cup Circuit" }""");
            WriteLayout(root, "mod_cup", "gp", """{ "name": "Grand Prix", "description": "Grand Prix notes v2.5" }""");

            var tracks = ContentLibrary.ScanTracks(root);
            var notes = tracks.Single(track => track.Id == "mod_notes");
            Assert.Equal("Spa", notes.Name);
            Assert.Equal("1.4", notes.Layouts[0].Version);
            Assert.Equal("Layout notes", notes.Layouts[0].Description);

            var locked = tracks.Single(track => track.Id == "mod_locked");
            Assert.Equal("2", locked.Layouts[0].Version);
            Assert.Equal("Notes v9.9", locked.Layouts[0].Description);

            var named = tracks.Single(track => track.Id == "mod_named");
            Assert.Equal("Spa", named.Name);
            Assert.Equal("1.2", named.Layouts[0].Version);
            Assert.Equal("Notes v9.9", named.Layouts[0].Description);

            var html = tracks.Single(track => track.Id == "mod_html");
            Assert.Equal("3.0", html.Layouts[0].Version);
            Assert.Equal("Fast circuit", html.Layouts[0].Description);

            var entity = tracks.Single(track => track.Id == "mod_entity");
            Assert.Equal("1.0", entity.Layouts[0].Version);
            Assert.Equal("Fish & chips", entity.Layouts[0].Description);

            var plain = tracks.Single(track => track.Id == "mod_plain");
            Assert.Null(plain.Layouts[0].Version);
            Assert.Equal("Just a circuit", plain.Layouts[0].Description);

            var lines = tracks.Single(track => track.Id == "mod_lines");
            Assert.Null(lines.Layouts[0].Version);
            Assert.Equal("Line one\nLine two v1.2", lines.Layouts[0].Description);

            var tab = tracks.Single(track => track.Id == "mod_tab");
            Assert.Equal("1.2", tab.Layouts[0].Version);
            Assert.Equal("Fastcircuit", tab.Layouts[0].Description);

            var blank = tracks.Single(track => track.Id == "mod_blank");
            Assert.Null(blank.Layouts[0].Version);
            Assert.Null(blank.Layouts[0].Description);

            var cup = tracks.Single(track => track.Id == "mod_cup");
            Assert.Equal("Cup Circuit", cup.Name);
            Assert.Null(cup.Layouts[0].Version);
            Assert.Null(cup.Layouts[0].Description);
            Assert.Equal("2.5", cup.Layouts[1].Version);
            Assert.Equal("Grand Prix notes", cup.Layouts[1].Description);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_car_description_does_not_set_the_version()
    {
        var root = Directory.CreateTempSubdirectory("actools-car-description-").FullName;
        try {
            WriteCar(root, "mod_notes", """{ "name": "Cup", "description": "Cup notes v1.2" }""");
            WriteCar(root, "mod_html", """{ "name": "Html", "description": "<b>Fast car</b> v3.0<br>" }""");
            WriteCar(root, "mod_entity", """{ "name": "Entity", "description": "Fish &amp; chips" }""");
            WriteCar(root, "mod_blank", """{ "name": "Blank", "description": "<br>" }""");

            var cars = ContentLibrary.ScanCars(root);
            var notes = cars.Single(car => car.Id == "mod_notes");
            Assert.Equal("Cup", notes.Name);
            Assert.Null(notes.Version);
            Assert.Equal("Cup notes v1.2", notes.Description);

            var html = cars.Single(car => car.Id == "mod_html");
            Assert.Equal("Html", html.Name);
            Assert.Null(html.Version);
            Assert.Equal("Fast car v3.0", html.Description);

            var entity = cars.Single(car => car.Id == "mod_entity");
            Assert.Equal("Fish & chips", entity.Description);
            Assert.Null(cars.Single(car => car.Id == "mod_blank").Description);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void A_car_url_is_trimmed()
    {
        var root = Directory.CreateTempSubdirectory("actools-car-url-").FullName;
        try {
            WriteCar(root, "mod_url", """{ "name": "Cup", "url": "  https://example.com/cars/cup  " }""");
            WriteCar(root, "mod_blank", """{ "name": "Blank", "url": "   " }""");
            WriteCar(root, "mod_plain", """{ "name": "Plain" }""");

            var cars = ContentLibrary.ScanCars(root);
            Assert.Equal("https://example.com/cars/cup", cars.Single(car => car.Id == "mod_url").Url);
            Assert.Null(cars.Single(car => car.Id == "mod_blank").Url);
            Assert.Null(cars.Single(car => car.Id == "mod_plain").Url);
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

    private static void WriteTrack(string root, string id, string json)
    {
        var ui = Path.Combine(root, id, "ui");
        Directory.CreateDirectory(ui);
        File.WriteAllText(Path.Combine(ui, "ui_track.json"), json);
    }

    private static void WriteLayout(string root, string id, string layout, string json)
    {
        var ui = Path.Combine(root, id, "ui", layout);
        Directory.CreateDirectory(ui);
        File.WriteAllText(Path.Combine(ui, "ui_track.json"), json);
    }
}
