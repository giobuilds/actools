using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using AcManager.Core.Content;
using Xunit;

namespace AcManager.Core.Tests;

public class ContentArchiveTests
{
    [Fact]
    public void Plan_finds_a_nested_car_and_ignores_a_skin_json()
    {
        var plans = ContentArchiveInstaller.Plan([
            "pack/my_car/ui/ui_car.json",
            "pack/my_car/skins/red/ui_skin.json",
            "pack\\my_car\\data\\car.ini",
        ], "pack.zip");

        var plan = Assert.Single(plans);
        Assert.Equal(InstalledContentKind.Car, plan.Kind);
        Assert.Equal("my_car", plan.Id);
        Assert.Equal("pack/my_car/", plan.ArchivePrefix);
    }

    [Fact]
    public void Plan_folds_a_track_and_its_layout_into_one_item()
    {
        var plans = ContentArchiveInstaller.Plan([
            "cup/ui/ui_track.json",
            "cup/ui/gp/ui_track.json",
            "notes/ui_track.json",
        ], "cup.zip");

        var plan = Assert.Single(plans);
        Assert.Equal(InstalledContentKind.Track, plan.Kind);
        Assert.Equal("cup", plan.Id);
        Assert.Equal("cup/", plan.ArchivePrefix);
    }

    [Fact]
    public void Plan_names_a_root_car_from_the_zip_file()
    {
        var plans = ContentArchiveInstaller.Plan(["ui/ui_car.json", "data/car.ini"], "bonus_car.zip");

        var plan = Assert.Single(plans);
        Assert.Equal(InstalledContentKind.Car, plan.Kind);
        Assert.Equal("bonus_car", plan.Id);
        Assert.Equal("", plan.ArchivePrefix);
    }

    [Fact]
    public void Plan_ignores_a_json_that_is_not_the_car_or_track_marker()
    {
        var plans = ContentArchiveInstaller.Plan([
            "ks/ui/extra/ui_car.json",
            "notes/ui_track.json",
            "skins/red/ui_skin.json",
        ], "nope.zip");

        Assert.Empty(plans);
    }

    [Fact]
    public void Install_extracts_a_car_and_a_track_and_leaves_the_wrapper_behind()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "pack.zip");
            WriteZip(zip,
                ("pack/readme.txt", "leave me"),
                ("pack/my_car/ui/ui_car.json", "{\"name\":\"Mine\"}"),
                ("pack/my_car/data/car.ini", "[CAR]\r\n"),
                ("cup/ui/ui_track.json", "{\"name\":\"Cup\"}"),
                ("cup/ui/gp/ui_track.json", "{\"name\":\"GP\"}"),
                ("cup/models.ini", "MODEL=1"));

            var result = ContentArchiveInstaller.Install(zip, game);

            Assert.Equal(2, result.Installed.Count);
            Assert.Contains(result.Installed, piece => piece.Kind == "car" && piece.Id == "my_car");
            Assert.Contains(result.Installed, piece => piece.Kind == "track" && piece.Id == "cup");
            Assert.Equal("{\"name\":\"Mine\"}", File.ReadAllText(Path.Combine(game, "content", "cars", "my_car", "ui", "ui_car.json")));
            Assert.Equal("[CAR]\r\n", File.ReadAllText(Path.Combine(game, "content", "cars", "my_car", "data", "car.ini")));
            Assert.Equal("{\"name\":\"GP\"}", File.ReadAllText(Path.Combine(game, "content", "tracks", "cup", "ui", "gp", "ui_track.json")));
            Assert.False(File.Exists(Path.Combine(game, "content", "cars", "my_car", "readme.txt")));
            Assert.False(File.Exists(Path.Combine(game, "content", "readme.txt")));
            Assert.Empty(result.Skipped);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_uses_the_zip_name_when_the_car_sits_at_the_archive_root()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "bonus_car.zip");
            WriteZip(zip, ("ui/ui_car.json", "{\"name\":\"Bonus\"}"));

            var result = ContentArchiveInstaller.Install(zip, game);

            var piece = Assert.Single(result.Installed);
            Assert.Equal("car", piece.Kind);
            Assert.Equal("bonus_car", piece.Id);
            Assert.True(File.Exists(Path.Combine(game, "content", "cars", "bonus_car", "ui", "ui_car.json")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_skips_a_folder_that_already_exists_and_keeps_its_files()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "my_car.zip");
            WriteZip(zip, ("my_car/ui/ui_car.json", "{\"name\":\"Mine\"}"));
            ContentArchiveInstaller.Install(zip, game);
            var marker = Path.Combine(game, "content", "cars", "my_car", "marker.txt");
            File.WriteAllText(marker, "keep");

            var result = ContentArchiveInstaller.Install(zip, game);

            Assert.Empty(result.Installed);
            Assert.Contains(result.Skipped, line => line.Contains("already exists", StringComparison.Ordinal));
            Assert.Equal("keep", File.ReadAllText(marker));
            Assert.Equal("{\"name\":\"Mine\"}", File.ReadAllText(Path.Combine(game, "content", "cars", "my_car", "ui", "ui_car.json")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_skips_the_whole_car_when_an_entry_escapes_and_still_installs_the_track()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "slip.zip");
            WriteZip(zip,
                ("my_car/ui/ui_car.json", "{\"name\":\"Mine\"}"),
                ("my_car/../../evil.txt", "nope"),
                ("cup/ui/ui_track.json", "{\"name\":\"Cup\"}"));

            var result = ContentArchiveInstaller.Install(zip, game);

            Assert.Single(result.Installed);
            Assert.Equal("track", result.Installed[0].Kind);
            Assert.Equal("cup", result.Installed[0].Id);
            Assert.Contains(result.Skipped, line => line.Contains("escapes", StringComparison.Ordinal));
            Assert.False(Directory.Exists(Path.Combine(game, "content", "cars", "my_car")));
            Assert.Empty(Directory.GetFiles(game, "evil.txt", SearchOption.AllDirectories));
            Assert.True(File.Exists(Path.Combine(game, "content", "tracks", "cup", "ui", "ui_track.json")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_does_not_take_files_from_a_longer_folder_name()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "cars.zip");
            WriteZip(zip,
                ("ks_foo/ui/ui_car.json", "foo"),
                ("ks_foo_extra/ui/ui_car.json", "extra"),
                ("ks_foo_extra/data/secret.txt", "secret"));

            var result = ContentArchiveInstaller.Install(zip, game);

            Assert.Equal(2, result.Installed.Count);
            Assert.False(File.Exists(Path.Combine(game, "content", "cars", "ks_foo", "data", "secret.txt")));
            Assert.Equal("secret", File.ReadAllText(Path.Combine(game, "content", "cars", "ks_foo_extra", "data", "secret.txt")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_keeps_the_first_copy_when_two_folders_share_an_id()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "dup.zip");
            WriteZip(zip,
                ("a/my_car/ui/ui_car.json", "first"),
                ("b/my_car/ui/ui_car.json", "second"));

            var result = ContentArchiveInstaller.Install(zip, game);

            var piece = Assert.Single(result.Installed);
            Assert.Equal("my_car", piece.Id);
            Assert.Contains(result.Skipped, line => line.Contains("already exists", StringComparison.Ordinal));
            Assert.Equal("first", File.ReadAllText(Path.Combine(game, "content", "cars", "my_car", "ui", "ui_car.json")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Sanitize_rewrites_shader_patch_pitch_and_keeps_real_numbers()
    {
        const string original = "[SURFACE_0]\r\nWAV_PITCH=extended-0\r\nFF_EFFECT=NULL\r\n[SURFACE_1]\r\nWAV_PITCH=\r\nWAV_PITCH=1.5\r\nFRICTION=0.96\r\n";

        var updated = TrackSurfaces.Sanitize(original, out var changed);

        Assert.Equal(2, changed);
        Assert.Equal(
            "[SURFACE_0]\r\nWAV_PITCH=0\r\nFF_EFFECT=NULL\r\n[SURFACE_1]\r\nWAV_PITCH=0\r\nWAV_PITCH=1.5\r\nFRICTION=0.96\r\n",
            updated);
    }

    [Fact]
    public void Install_makes_a_track_surface_pitch_safe_for_the_original_game()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "cup.zip");
            WriteZip(zip,
                ("cup/ui/ui_track.json", "{\"name\":\"Cup\"}"),
                ("cup/data/surfaces.ini", "[SURFACE_0]\r\nKEY=ROAD\r\nWAV_PITCH=extended-0\r\nWAV=\r\nFF_EFFECT=NULL\r\n"));

            var result = ContentArchiveInstaller.Install(zip, game);

            var piece = Assert.Single(result.Installed);
            Assert.Equal("track", piece.Kind);
            Assert.Contains("surfaces.ini", piece.Note, StringComparison.Ordinal);
            var surfaces = Path.Combine(game, "content", "tracks", "cup", "data", "surfaces.ini");
            var text = File.ReadAllText(surfaces);
            Assert.Contains("WAV_PITCH=0\r\n", text, StringComparison.Ordinal);
            Assert.Contains("FF_EFFECT=NULL", text, StringComparison.Ordinal);
            Assert.Contains("WAV_PITCH=extended-0", File.ReadAllText(surfaces + ".bak"), StringComparison.Ordinal);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Rewrite_swaps_shader_patch_materials_and_leaves_the_rest_of_the_kn5()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("actools-kn5-").FullName, "track.kn5");
        try {
            var tail = new byte[] { 9, 8, 7, 6 };
            File.WriteAllBytes(path, SampleKn5("ksPerPixelAT_NM_emissive", tail));

            var changed = Kn5ShaderFallback.Rewrite(path);

            Assert.Equal(1, changed);
            var written = File.ReadAllBytes(path);
            var text = Encoding.ASCII.GetString(written);
            Assert.StartsWith("sc6969", text, StringComparison.Ordinal);
            Assert.Contains("ksPerPixelAT_NM", text, StringComparison.Ordinal);
            Assert.DoesNotContain("emissive", text, StringComparison.Ordinal);
            Assert.Equal(tail, written[^4..]);
            Assert.Equal(SampleKn5("ksPerPixelAT_NM_emissive", tail), File.ReadAllBytes(path + ".bak"));
            Assert.Equal(0, Kn5ShaderFallback.Rewrite(path));
        } finally {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void Install_rejects_a_file_that_is_not_a_zip()
    {
        var game = NewGameRoot();
        try {
            var file = Path.Combine(game, "car.zip");
            File.WriteAllText(file, "hello");

            var ex = Assert.Throws<InvalidDataException>(() => ContentArchiveInstaller.Install(file, game));

            Assert.Equal("That file is not a zip archive.", ex.Message);
            Assert.Empty(Directory.GetDirectories(Path.Combine(game, "content", "cars")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public async Task Install_makes_a_7z_track_surface_pitch_safe_for_the_original_game()
    {
        var game = NewGameRoot();
        try {
            var source = Path.Combine(game, "src");
            Directory.CreateDirectory(Path.Combine(source, "cup", "ui"));
            Directory.CreateDirectory(Path.Combine(source, "cup", "data"));
            File.WriteAllText(Path.Combine(source, "cup", "ui", "ui_track.json"), "{\"name\":\"Cup\"}");
            File.WriteAllText(
                Path.Combine(source, "cup", "data", "surfaces.ini"),
                "[SURFACE_0]\r\nKEY=ROAD\r\nWAV_PITCH=extended-0\r\nWAV=\r\nFF_EFFECT=NULL\r\n");
            File.WriteAllBytes(
                Path.Combine(source, "cup", "track.kn5"),
                SampleKn5("ksPerPixelAT_NM_emissive", [9, 8, 7, 6]));
            var archive = Path.Combine(game, "cup.7z");
            PackSevenZip(source, archive, "cup");

            var result = await ContentArchiveInstaller.InstallSevenZipAsync(archive, game);

            var piece = Assert.Single(result.Installed);
            Assert.Equal("track", piece.Kind);
            Assert.Equal("cup", piece.Id);
            Assert.Contains("surfaces.ini", piece.Note, StringComparison.Ordinal);
            Assert.Contains("shaders", piece.Note, StringComparison.Ordinal);
            var surfaces = Path.Combine(game, "content", "tracks", "cup", "data", "surfaces.ini");
            var text = File.ReadAllText(surfaces);
            Assert.Contains("WAV_PITCH=0\r\n", text, StringComparison.Ordinal);
            Assert.Contains("FF_EFFECT=NULL", text, StringComparison.Ordinal);
            Assert.Contains("WAV_PITCH=extended-0", File.ReadAllText(surfaces + ".bak"), StringComparison.Ordinal);
            var installedKn5 = Path.Combine(game, "content", "tracks", "cup", "track.kn5");
            var kn5 = Encoding.ASCII.GetString(File.ReadAllBytes(installedKn5));
            Assert.Contains("ksPerPixelAT_NM", kn5, StringComparison.Ordinal);
            Assert.DoesNotContain("emissive", kn5, StringComparison.Ordinal);
            Assert.Contains("emissive", Encoding.ASCII.GetString(File.ReadAllBytes(installedKn5 + ".bak")), StringComparison.Ordinal);

            var again = await ContentArchiveInstaller.InstallSevenZipAsync(archive, game);
            Assert.Contains(again.Skipped, line => line.Contains("already exists", StringComparison.Ordinal));
            Assert.Contains("WAV_PITCH=0\r\n", File.ReadAllText(surfaces), StringComparison.Ordinal);
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public async Task Install_rejects_a_file_that_is_not_a_7z()
    {
        var game = NewGameRoot();
        try {
            var file = Path.Combine(game, "car.7z");
            File.WriteAllText(file, "hello");

            var missing = await Assert.ThrowsAsync<FileNotFoundException>(() =>
                ContentArchiveInstaller.InstallSevenZipAsync(Path.Combine(game, "missing.7z"), game));
            Assert.Equal("Could not find that archive.", missing.Message);

            var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ContentArchiveInstaller.InstallSevenZipAsync(file, game));
            Assert.Equal("That file is not a 7z archive.", ex.Message);
            Assert.Empty(Directory.GetDirectories(Path.Combine(game, "content", "cars")));
            Assert.Empty(Directory.GetDirectories(Path.Combine(game, "content", "tracks")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    [Fact]
    public void Install_reports_when_the_zip_has_no_car_or_track()
    {
        var game = NewGameRoot();
        try {
            var zip = Path.Combine(game, "notes.zip");
            WriteZip(zip, ("readme.txt", "hello"));

            var result = ContentArchiveInstaller.Install(zip, game);

            Assert.Empty(result.Installed);
            Assert.Contains(result.Skipped, line => line.Contains("ui/ui_car.json", StringComparison.Ordinal));
            Assert.Empty(Directory.GetDirectories(Path.Combine(game, "content", "cars")));
        } finally {
            Directory.Delete(game, true);
        }
    }

    private static string NewGameRoot()
    {
        var root = Directory.CreateTempSubdirectory("actools-zip-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "content", "cars"));
        Directory.CreateDirectory(Path.Combine(root, "content", "tracks"));
        return root;
    }

    private static byte[] SampleKn5(string shader, byte[] tail)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("sc6969"));
        writer.Write(6);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1);
        WriteKn5String(writer, "paint");
        WriteKn5String(writer, shader);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(tail);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteKn5String(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void PackSevenZip(string workingDirectory, string archive, params string[] names)
    {
        var tool = ServerContentInstaller.FindSevenZip();
        Assert.False(string.IsNullOrEmpty(tool));
        var start = new ProcessStartInfo {
            FileName = tool,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("a");
        start.ArgumentList.Add("-bd");
        start.ArgumentList.Add("-y");
        start.ArgumentList.Add(archive);
        foreach (var name in names) {
            start.ArgumentList.Add(name);
        }

        using var process = Process.Start(start);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(20000));
        var error = stderr.GetAwaiter().GetResult();
        _ = stdout.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, error);
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
}
