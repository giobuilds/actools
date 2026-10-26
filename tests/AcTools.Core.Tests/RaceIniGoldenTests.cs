using AcTools.Core.Sessions;
using AcTools.Processes;
using Xunit;
using OriginalIni = AcTools.DataFile.IniFile;
using SessionIni = AcTools.Core.Data.IniFile;

namespace AcTools.Core.Tests;

public class RaceIniGoldenTests
{
    [Fact]
    public void Hotlap_matches_the_property_writer_key_by_key()
    {
        var session = new PracticeSession {
            Mode = QuickDriveMode.Hotlap,
            CarId = "KS_Mazda_MX5_ND",
            SkinId = "0_Official",
            TrackId = "ks_barcelona",
            TrackLayoutId = "layout_gp",
            WeatherId = "3_clear",
            DurationMinutes = 45,
            Opponents = 4,
            RaceLaps = 9,
            AiLevel = 10,
        };

        var original = Properties(session, new Game.HotlapProperties()).BuildRaceIni();
        var linux = PracticeRaceIni.Build(session);

        AssertMatches(original, linux);
        Assert.Equal("2", linux["HEADER"].Get("__CM_FEATURE_SET"));
        Assert.Equal("4", linux["SESSION_0"].Get("TYPE"));
        Assert.Equal("0", linux["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("HOTLAP_START", linux["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("1", linux["RACE"].Get("CARS"));
        Assert.Equal("100", linux["RACE"].Get("AI_LEVEL"));
        Assert.Null(linux["RACE"].Get("RACE_LAPS"));
        Assert.Equal("0", linux["GHOST_CAR"].Get("SECONDS_ADVANTAGE"));
        Assert.Equal("1", linux["GHOST_CAR"].Get("PLAYING"));
        Assert.Equal("1", linux["GHOST_CAR"].Get("RECORDING"));
        Assert.Equal("1", linux["GROOVE"].Get("MAX_LAPS"));
        Assert.Equal("1", linux["GROOVE"].Get("STARTING_LAPS"));
        Assert.False(linux.Contains("CAR_1"));
        Assert.Null(linux["CAR_0"].Get("DRIVER_NAME"));
        Assert.Equal("0", linux["__PREVIEW_GENERATION"].Get("ACTIVE"));
    }

    [Fact]
    public void Race_matches_the_property_writer_key_by_key()
    {
        var session = Grid("ks_ferrari_458", "Red_00");
        var original = Properties(session, new Game.RaceProperties {
            AiLevel = 80,
            RaceLaps = 3,
            BotCars = Bots(session, 2, 80),
        }).BuildRaceIni();
        var linux = PracticeRaceIni.Build(session);

        AssertMatches(original, linux);
        Assert.Null(linux["HEADER"].Get("__CM_FEATURE_SET"));
        Assert.Equal("3", linux["RACE"].Get("CARS"));
        Assert.Equal("3", linux["RACE"].Get("RACE_LAPS"));
        Assert.Equal("80", linux["RACE"].Get("AI_LEVEL"));
        Assert.Equal("Quick Race", linux["SESSION_0"].Get("NAME"));
        Assert.Equal("3", linux["SESSION_0"].Get("TYPE"));
        Assert.Equal("3", linux["SESSION_0"].Get("LAPS"));
        Assert.Equal("0", linux["SESSION_0"].Get("STARTING_POSITION"));
        Assert.Equal("START", linux["SESSION_0"].Get("SPAWN_SET"));
        Assert.Null(linux["GHOST_CAR"].Get("SECONDS_ADVANTAGE"));
        Assert.Equal("30", linux["GROOVE"].Get("MAX_LAPS"));
        Assert.Equal("ks_ferrari_458", linux["CAR_1"].Get("MODEL"));
        Assert.Equal("red_00", linux["CAR_1"].Get("SKIN"));
        Assert.Equal("AI 1", linux["CAR_1"].Get("DRIVER_NAME"));
        Assert.Equal("80", linux["CAR_2"].Get("AI_LEVEL"));
        Assert.Equal("", linux["CAR_1"].Get("NATION_CODE"));
        Assert.Equal("", linux["CAR_2"].Get("NATIONALITY"));
        Assert.Null(linux["CAR_1"].Get("EXT_AI_RESTRICTIONS"));
    }

    [Fact]
    public void Track_day_matches_the_property_writer_key_by_key()
    {
        var session = Grid("ks_mazda_mx5", "0_official") with { Mode = QuickDriveMode.TrackDay };
        var original = Properties(session, new Game.TrackdayProperties {
            AiLevel = 80,
            RaceLaps = 3,
            BotCars = Bots(session, 2, 80),
        }).BuildRaceIni();
        var linux = PracticeRaceIni.Build(session);

        AssertMatches(original, linux);
        Assert.Equal(Game.TrackDaySessionName, linux["SESSION_0"].Get("NAME"));
        Assert.Equal("2", linux["SESSION_0"].Get("TYPE"));
        Assert.Equal("720", linux["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("PIT", linux["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("0", linux["SESSION_0"].Get("__SPEED_LIMIT"));
        Assert.Equal("3", linux["RACE"].Get("CARS"));
        Assert.Equal("3", linux["RACE"].Get("RACE_LAPS"));
        Assert.Equal("80", linux["RACE"].Get("AI_LEVEL"));
        Assert.Null(linux["HEADER"].Get("__CM_FEATURE_SET"));
        Assert.Equal("30", linux["GROOVE"].Get("MAX_LAPS"));
        Assert.Equal("0", linux["GROOVE"].Get("STARTING_LAPS"));
        Assert.Equal("", linux["CAR_2"].Get("NATION_CODE"));
    }

    [Fact]
    public void Custom_weather_matches_the_property_writer_key_by_key()
    {
        var session = new PracticeSession {
            Mode = QuickDriveMode.Practice,
            CarId = "ks_lamborghini_huracan",
            SkinId = "00_white",
            TrackId = "ks_nurburgring",
            TrackLayoutId = "layout_gp",
            WeatherId = "7_Heavy_Rain",
            DriverName = "Ada",
            DurationMinutes = 25,
            SpawnSet = "START",
            SunAngle = 16,
            AmbientTemperature = 11,
            RoadTemperature = 7,
            TimeMultiplier = 4,
            CloudSpeed = 0.05,
            WindSpeedMin = 3,
            WindSpeedMax = 18,
            WindDirection = 90,
            TrackSessionStart = 86,
            TrackRandomness = 1,
            TrackLapGain = 30,
            TrackSessionTransfer = 50,
        };
        var original = Properties(session, new Game.PracticeProperties {
            Duration = 25,
            StartType = Game.StartType.RegularStart,
        }).BuildRaceIni();
        var linux = PracticeRaceIni.Build(session);

        AssertMatches(original, linux);
        Assert.Equal("7_heavy_rain", linux["WEATHER"].Get("NAME"));
        Assert.Equal("16.00", linux["LIGHTING"].Get("SUN_ANGLE"));
        Assert.Equal("4.0", linux["LIGHTING"].Get("TIME_MULT"));
        Assert.Equal("0.050", linux["LIGHTING"].Get("CLOUD_SPEED"));
        Assert.Equal("11", linux["TEMPERATURE"].Get("AMBIENT"));
        Assert.Equal("7", linux["TEMPERATURE"].Get("ROAD"));
        Assert.Equal("3", linux["WIND"].Get("SPEED_KMH_MIN"));
        Assert.Equal("18", linux["WIND"].Get("SPEED_KMH_MAX"));
        Assert.Equal("90", linux["WIND"].Get("DIRECTION_DEG"));
        Assert.Equal("86", linux["DYNAMIC_TRACK"].Get("SESSION_START"));
        Assert.Equal("1", linux["DYNAMIC_TRACK"].Get("RANDOMNESS"));
        Assert.Equal("30", linux["DYNAMIC_TRACK"].Get("LAP_GAIN"));
        Assert.Equal("50", linux["DYNAMIC_TRACK"].Get("SESSION_TRANSFER"));
        Assert.Null(linux["DYNAMIC_TRACK"].Get("PRESET"));
        Assert.Equal("Ada", linux["CAR_0"].Get("DRIVER_NAME"));
        Assert.Equal("25", linux["SESSION_0"].Get("DURATION_MINUTES"));
        Assert.Equal("START", linux["SESSION_0"].Get("SPAWN_SET"));
        Assert.Equal("1", linux["SESSION_0"].Get("TYPE"));
        Assert.Equal("2", linux["HEADER"].Get("__CM_FEATURE_SET"));
    }

    private static PracticeSession Grid(string carId, string skinId) => new() {
        Mode = QuickDriveMode.Race,
        CarId = carId,
        SkinId = skinId,
        TrackId = "ks_silverstone",
        TrackLayoutId = "gp",
        WeatherId = "3_clear",
        Opponents = 2,
        RaceLaps = 3,
        AiLevel = 80,
        DurationMinutes = 30,
    };

    private static List<Game.AiCar> Bots(PracticeSession session, int count, int ai)
    {
        var bots = new List<Game.AiCar>(count);
        for (var index = 1; index <= count; index++) {
            bots.Add(new Game.AiCar {
                CarId = session.CarId,
                SkinId = session.SkinId,
                DriverName = "AI " + index,
                AiLevel = ai,
            });
        }

        return bots;
    }

    private static Game.StartProperties Properties(PracticeSession session, Game.BaseModeProperties mode) => new() {
        BasicProperties = new Game.BasicProperties {
            CarId = session.CarId,
            CarSkinId = session.SkinId,
            TrackId = session.TrackId,
            TrackConfigurationId = session.TrackLayoutId,
            DriverName = string.IsNullOrWhiteSpace(session.DriverName) ? null : session.DriverName,
        },
        ModeProperties = mode,
        ConditionProperties = new Game.ConditionProperties {
            SunAngle = session.SunAngle,
            TimeMultiplier = session.TimeMultiplier,
            CloudSpeed = session.CloudSpeed,
            RoadTemperature = session.RoadTemperature,
            AmbientTemperature = session.AmbientTemperature,
            WindDirectionDeg = session.WindDirection,
            WindSpeedMin = session.WindSpeedMin,
            WindSpeedMax = session.WindSpeedMax,
            WeatherName = session.WeatherId,
        },
        TrackProperties = new Game.TrackProperties {
            SessionStart = session.TrackSessionStart,
            Randomness = session.TrackRandomness,
            LapGain = session.TrackLapGain,
            SessionTransfer = session.TrackSessionTransfer,
        },
    };

    private static void AssertMatches(OriginalIni original, SessionIni linux)
    {
        var extras = new List<string>();
        foreach (var name in linux.SectionNames) {
            foreach (var pair in linux[name].Pairs) {
                if (!Has(original, name, pair.Key)) {
                    extras.Add(name + "." + pair.Key + "=" + pair.Value);
                }
            }
        }

        Assert.Equal(
            Shell.OrderBy(item => item, StringComparer.Ordinal),
            extras.OrderBy(item => item, StringComparer.Ordinal));

        foreach (var section in original) {
            foreach (var pair in section.Value) {
                Assert.True(linux.Contains(section.Key), section.Key);
                Assert.True(linux[section.Key].Contains(pair.Key), section.Key + "." + pair.Key);
                Assert.Equal(pair.Value, linux[section.Key].Get(pair.Key));
            }
        }
    }

    private static bool Has(OriginalIni file, string section, string key) =>
        file.ContainsKey(section) && file[section].ContainsKey(key);

    private static readonly string[] Shell = [
        "REPLAY.FILENAME=",
        "REMOTE.SERVER_IP=",
        "REMOTE.SERVER_PORT=",
        "REMOTE.NAME=",
        "REMOTE.TEAM=",
        "REMOTE.GUID=",
        "REMOTE.REQUESTED_CAR=",
        "REMOTE.PASSWORD=",
    ];
}
