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
        Assert.Contains("VERSION=2\r\n", text, StringComparison.Ordinal);
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
        Assert.Contains("TIME_MULT=1\r\n", text, StringComparison.Ordinal);
        Assert.Contains("[REPLAY]\r\nFILENAME=\r\nACTIVE=0\r\n", text, StringComparison.Ordinal);
        Assert.Contains("[REMOTE]\r\nACTIVE=0\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Hotlap_race_and_track_day_use_the_content_manager_session_types()
    {
        var shared = new PracticeSession {
            CarId = "ks_mazda_mx5",
            SkinId = "0_official",
            TrackId = "ks_barcelona",
            TrackLayoutId = "layout_gp",
            WeatherId = "3_clear",
            Opponents = 2,
            RaceLaps = 3,
            AiLevel = 80,
            DurationMinutes = 30,
        };

        var hotlap = PracticeRaceIni.Build(shared with { Mode = QuickDriveMode.Hotlap });
        Assert.Equal("4", hotlap["SESSION_0"].Get("TYPE"));
        Assert.Equal("Hotlap", hotlap["SESSION_0"].Get("NAME"));
        Assert.Equal("HOTLAP_START", hotlap["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("0", hotlap["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("1", hotlap["GHOST_CAR"].Get("PLAYING"));
        Assert.Equal("1", hotlap["GHOST_CAR"].Get("RECORDING"));
        Assert.Equal("1", hotlap["GROOVE"].Get("MAX_LAPS"));
        Assert.Equal("1", hotlap["RACE"].Get("CARS"));

        var race = PracticeRaceIni.Build(shared with { Mode = QuickDriveMode.Race });
        Assert.Equal("3", race["SESSION_0"].Get("TYPE"));
        Assert.Equal("Quick Race", race["SESSION_0"].Get("NAME"));
        Assert.Equal("START", race["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("3", race["SESSION_0"].Get("LAPS"));
        Assert.Equal("0", race["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("3", race["RACE"].Get("CARS"));
        Assert.Equal("3", race["RACE"].Get("RACE_LAPS"));
        Assert.Equal("80", race["RACE"].Get("AI_LEVEL"));
        Assert.Equal("ks_mazda_mx5", race["CAR_1"].Get("MODEL"));
        Assert.Equal("ks_mazda_mx5", race["CAR_2"].Get("MODEL"));
        Assert.Equal("0_official", race["CAR_1"].Get("SKIN"));
        Assert.Equal("AI 2", race["CAR_2"].Get("DRIVER_NAME"));
        Assert.Equal("0", race["GHOST_CAR"].Get("PLAYING"));

        var trackDay = PracticeRaceIni.Build(shared with { Mode = QuickDriveMode.TrackDay });
        Assert.Equal("2", trackDay["SESSION_0"].Get("TYPE"));
        Assert.Equal("Track Day", trackDay["SESSION_0"].Get("NAME"));
        Assert.Equal("PIT", trackDay["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("720", trackDay["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("0", trackDay["SESSION_0"].Get("__SPEED_LIMIT"));
        Assert.Equal("3", trackDay["RACE"].Get("CARS"));
        Assert.Null(trackDay["RACE"].Get("RACE_LAPS"));
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
