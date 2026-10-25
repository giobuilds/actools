using AcTools.Core.Data;
using Xunit;
using AcTools.Core.Sessions;

namespace AcTools.Core.Tests;

public class IniAndPracticeTests
{
    [Fact]
    public void Parse_reads_sections_and_ignores_comments()
    {
        var ini = IniFile.Parse("""
            [RACE]
            MODEL=ks_ferrari_458 ; comment
            CARS=1
            // whole line
            TRACK=imola
            URL=http://example.test/car

            [SESSION_0]
            NAME=Practice
            TYPE=1
            EMPTY=
            """);

        Assert.Equal("ks_ferrari_458", ini["RACE"].Get("MODEL"));
        Assert.Equal("1", ini["RACE"].Get("CARS"));
        Assert.Equal("imola", ini["RACE"].Get("TRACK"));
        Assert.Equal("http://example.test/car", ini["RACE"].Get("URL"));
        Assert.Equal("", ini["SESSION_0"].Get("EMPTY"));
        Assert.Equal("Practice", ini["SESSION_0"].Get("NAME"));
    }

    [Fact]
    public void Stringify_uses_crlf_and_bools()
    {
        var ini = new IniFile();
        ini["RACE"].Set("DRIFT_MODE", false);
        ini["RACE"].Set("CARS", 1);
        var text = ini.Stringify();

        Assert.Contains("[RACE]\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DRIFT_MODE=0\r\n", text, StringComparison.Ordinal);
        Assert.Contains("CARS=1\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\nDRIFT", text.Replace("\r\n", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Practice_ini_matches_the_quick_drive_shape()
    {
        var text = PracticeRaceIni.Build(new PracticeSession {
            CarId = "KS_Ferrari_458",
            SkinId = "Red_00",
            TrackId = "ks_silverstone",
            TrackLayoutId = "GP",
            WeatherId = "3_clear",
            DriverName = "Keeper",
            DurationMinutes = 30,
            SpawnSet = "PIT",
        }).Stringify();

        Assert.Contains("__CM_FEATURE_SET=2\r\n", text, StringComparison.Ordinal);
        Assert.Contains("MODEL=ks_ferrari_458\r\n", text, StringComparison.Ordinal);
        Assert.Contains("SKIN=red_00\r\n", text, StringComparison.Ordinal);
        Assert.Contains("TRACK=ks_silverstone\r\n", text, StringComparison.Ordinal);
        Assert.Contains("CONFIG_TRACK=gp\r\n", text, StringComparison.Ordinal);
        Assert.Contains("CARS=1\r\n", text, StringComparison.Ordinal);
        Assert.Contains("TYPE=1\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DURATION_MINUTES=30\r\n", text, StringComparison.Ordinal);
        Assert.Contains("SPAWN_SET=PIT\r\n", text, StringComparison.Ordinal);
        Assert.Contains("NAME=3_clear\r\n", text, StringComparison.Ordinal);
        Assert.Contains("[CAR_0]\r\n", text, StringComparison.Ordinal);
        Assert.Contains("MODEL=-\r\n", text, StringComparison.Ordinal);
        Assert.Contains("DRIVER_NAME=Keeper\r\n", text, StringComparison.Ordinal);
        Assert.Contains("[GHOST_CAR]\r\n", text, StringComparison.Ordinal);
        Assert.Contains("ENABLED=0\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_writes_race_ini_under_documents_cfg()
    {
        var documents = Directory.CreateTempSubdirectory("actools-docs-").FullName;
        try {
            Directory.CreateDirectory(Path.Combine(documents, "cfg"));
            var path = Path.Combine(documents, "cfg", "race.ini");
            File.WriteAllText(path, "[RACE]\r\nMODEL=old\r\n");

            var written = RaceFiles.WriteRaceIni(documents, PracticeRaceIni.Build(new PracticeSession {
                CarId = "ks_mazda_mx5",
                SkinId = "0",
                TrackId = "ks_silverstone",
            }));

            Assert.Equal(path, written);
            Assert.Contains("MODEL=old", File.ReadAllText(path + ".bak"), StringComparison.Ordinal);
            var loaded = IniFile.Load(path);
            Assert.Equal("ks_mazda_mx5", loaded["RACE"].Get("MODEL"));
            Assert.Equal("1", loaded["SESSION_0"].Get("TYPE"));
        } finally {
            Directory.Delete(documents, true);
        }
    }
}
