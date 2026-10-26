using AcManager.Core.Content;
using Xunit;

namespace AcManager.Core.Tests;

public class ContentRemovalTests
{
    [Fact]
    public void List_keeps_original_cars_and_tracks_and_offers_added_ones()
    {
        var game = NewGame();
        try {
            WriteCar(game, "ks_existing", "Existing");
            WriteCar(game, "f1_2026_rb22", "F1 Red Bull RB22 2026");
            WriteCar(game, "Flowpoint Valley", "Valley Car");
            WriteTrack(game, "spa", "Spa", official: true);
            WriteTrack(game, "chq_sepang", "Sepang 2026", official: false, author: "CHiQuiFReaKy");
            WriteLayoutTrack(game, "ks_silverstone", "GP", official: true);
            File.WriteAllText(
                Path.Combine(game, "content", "sfx", "GUIDs.txt"),
                "{00000000-0000-0000-0000-000000000001} event:/cars/ks_existing/engine\n");

            var library = ContentRemoval.List(game);

            Assert.True(library.OfficialCarsKnown);
            var car = Assert.Single(library.Cars, item => item.Id == "f1_2026_rb22");
            Assert.Equal("F1 Red Bull RB22 2026", car.Name);
            Assert.Contains(library.Cars, item => item.Id == "Flowpoint Valley");
            Assert.DoesNotContain(library.Cars, item => item.Id == "ks_existing");
            var track = Assert.Single(library.Tracks);
            Assert.Equal("chq_sepang", track.Id);
            Assert.Equal("Sepang 2026", track.Name);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void List_includes_a_track_whose_description_breaks_the_json()
    {
        var game = NewGame();
        try {
            var ui = Path.Combine(game, "content", "tracks", "chq_sepang", "ui");
            Directory.CreateDirectory(ui);
            File.WriteAllText(Path.Combine(ui, "ui_track.json"), """
                {
                  "name": "Sepang 2026 (CHiQuiFReaKy)",
                  "description": "Sepang International Circuit
                Formula 1",
                  "tags": ["circuit", "Sepang"],
                  "author": "CHiQuiFReaKy",
                  "url": null
                }
                """);
            WriteTrack(game, "spa", "Spa", official: true);

            var library = ContentRemoval.List(game);

            var track = Assert.Single(library.Tracks);
            Assert.Equal("chq_sepang", track.Id);
            Assert.Equal("Sepang 2026 (CHiQuiFReaKy)", track.Name);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void List_hides_cars_when_the_sound_list_is_missing()
    {
        var game = NewGame();
        try {
            WriteCar(game, "f1_2026_rb22", "RB22");
            WriteTrack(game, "chq_sepang", "Sepang", official: false, author: "Mod");

            var library = ContentRemoval.List(game);

            Assert.False(library.OfficialCarsKnown);
            Assert.Empty(library.Cars);
            Assert.Single(library.Tracks);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Delete_removes_the_added_folder_and_leaves_the_original_game()
    {
        var game = NewGame();
        try {
            WriteCar(game, "ks_existing", "Existing");
            WriteCar(game, "ks_existing_extra", "Extra");
            WriteTrack(game, "spa", "Spa", official: true);
            WriteTrack(game, "chq_sepang", "Sepang", official: false, author: "Mod");
            File.WriteAllText(Path.Combine(game, "content", "tracks", "chq_sepang", "data", "surfaces.ini.bak"), "keep-me-inside");
            File.WriteAllText(
                Path.Combine(game, "content", "sfx", "GUIDs.txt"),
                "{00000000-0000-0000-0000-000000000001} event:/cars/ks_existing/engine\n");
            var official = Path.Combine(game, "content", "cars", "ks_existing", "ui", "ui_car.json");
            var sibling = Path.Combine(game, "content", "cars", "ks_existing_extra", "ui", "ui_car.json");

            ContentRemoval.Delete(game, "car", "ks_existing_extra");
            ContentRemoval.Delete(game, "track", "chq_sepang");

            Assert.False(File.Exists(sibling));
            Assert.False(Directory.Exists(Path.Combine(game, "content", "tracks", "chq_sepang")));
            Assert.True(File.Exists(official));
            Assert.True(File.Exists(Path.Combine(game, "content", "tracks", "spa", "ui", "ui_track.json")));
            Assert.Throws<InvalidOperationException>(() => ContentRemoval.Delete(game, "car", "ks_existing"));
            Assert.True(File.Exists(official));
            Assert.Throws<ArgumentException>(() => ContentRemoval.Delete(game, "car", ".."));
            Assert.True(Directory.Exists(Path.Combine(game, "content", "cars")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Delete_leaves_a_linked_folder_and_its_target()
    {
        var game = NewGame();
        try {
            WriteCar(game, "ks_existing", "Existing");
            var target = Path.Combine(game, "content", "cars", "ks_existing");
            var link = Path.Combine(game, "content", "cars", "linked_mod");
            Directory.CreateSymbolicLink(link, target);
            File.WriteAllText(
                Path.Combine(game, "content", "sfx", "GUIDs.txt"),
                "{00000000-0000-0000-0000-000000000001} event:/cars/ks_existing/engine\n");

            var library = ContentRemoval.List(game);
            Assert.DoesNotContain(library.Cars, item => item.Id == "linked_mod");
            Assert.Throws<InvalidOperationException>(() => ContentRemoval.Delete(game, "car", "linked_mod"));
            Assert.True(File.Exists(Path.Combine(target, "ui", "ui_car.json")));
            Assert.True(Directory.Exists(link));
        } finally {
            Directory.Delete(game, true);
        }
    }

    private static string NewGame()
    {
        var root = Directory.CreateTempSubdirectory("actools-remove-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "content", "cars"));
        Directory.CreateDirectory(Path.Combine(root, "content", "tracks"));
        Directory.CreateDirectory(Path.Combine(root, "content", "sfx"));
        return root;
    }

    private static void WriteCar(string game, string id, string name)
    {
        var ui = Path.Combine(game, "content", "cars", id, "ui");
        Directory.CreateDirectory(ui);
        File.WriteAllText(Path.Combine(ui, "ui_car.json"), "{\"name\":\"" + name + "\"}");
    }

    private static void WriteTrack(string game, string id, string name, bool official, string? author = null)
    {
        var ui = Path.Combine(game, "content", "tracks", id, "ui");
        Directory.CreateDirectory(ui);
        Directory.CreateDirectory(Path.Combine(game, "content", "tracks", id, "data"));
        var authorText = author == null ? "" : ",\"author\":\"" + author + "\"";
        var tags = official ? "[\"circuit\",\"original\"]" : "[\"circuit\"]";
        File.WriteAllText(Path.Combine(ui, "ui_track.json"), "{\"name\":\"" + name + "\"" + authorText + ",\"tags\":" + tags + "}");
    }

    private static void WriteLayoutTrack(string game, string id, string layout, bool official)
    {
        var ui = Path.Combine(game, "content", "tracks", id, "ui", layout);
        Directory.CreateDirectory(ui);
        var tags = official ? "[\"circuit\",\"original\"]" : "[\"circuit\"]";
        File.WriteAllText(Path.Combine(ui, "ui_track.json"), "{\"name\":\"" + layout + "\",\"tags\":" + tags + "}");
    }
}
