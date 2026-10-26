using AcTools.DataFile;
using AcTools.Processes;
using Xunit;

namespace AcTools.Core.Tests;

public class GamePropertiesTests
{
    [Fact]
    public void Practice_writes_the_content_manager_session_keys()
    {
        var ini = new Game.StartProperties {
            BasicProperties = Player(),
            ModeProperties = new Game.PracticeProperties { Duration = 30 },
            ConditionProperties = Weather(),
            TrackProperties = Track(),
        }.BuildRaceIni();

        Assert.Equal("2", Value(ini, "HEADER", "VERSION"));
        Assert.Equal("2", Value(ini, "HEADER", "__CM_FEATURE_SET"));
        Assert.Equal("ks_ferrari_458", Value(ini, "RACE", "MODEL"));
        Assert.Equal("0", Value(ini, "RACE", "SKIN"));
        Assert.Equal("ks_silverstone", Value(ini, "RACE", "TRACK"));
        Assert.Equal("", Value(ini, "RACE", "CONFIG_TRACK"));
        Assert.Equal("1", Value(ini, "RACE", "CARS"));
        Assert.Equal("100", Value(ini, "RACE", "AI_LEVEL"));
        Assert.Equal("0", Value(ini, "RACE", "DRIFT_MODE"));
        Assert.Equal("0", Value(ini, "RACE", "FIXED_SETUP"));
        Assert.Equal("1", Value(ini, "RACE", "PENALTIES"));
        Assert.Equal("0", Value(ini, "RACE", "JUMP_START_PENALTY"));
        Assert.Equal("-", Value(ini, "CAR_0", "MODEL"));
        Assert.Equal("0", Value(ini, "CAR_0", "SKIN"));
        Assert.Equal("", Value(ini, "CAR_0", "SETUP"));
        Assert.Equal("0", Value(ini, "CAR_0", "BALLAST"));
        Assert.Equal("Keeper", Value(ini, "CAR_0", "DRIVER_NAME"));
        Assert.Equal("Practice", Value(ini, "SESSION_0", "NAME"));
        Assert.Equal("1", Value(ini, "SESSION_0", "TYPE"));
        Assert.Equal("30", Value(ini, "SESSION_0", "DURATION_MINUTES"));
        Assert.Equal("PIT", Value(ini, "SESSION_0", "SPAWN_SET"));
        Assert.Equal("0", Value(ini, "GHOST_CAR", "PLAYING"));
        Assert.Equal("0", Value(ini, "GHOST_CAR", "RECORDING"));
        Assert.Equal("10", Value(ini, "GROOVE", "VIRTUAL_LAPS"));
        Assert.Equal("30", Value(ini, "GROOVE", "MAX_LAPS"));
        Assert.Equal("0", Value(ini, "GROOVE", "STARTING_LAPS"));
        Assert.Equal("-48.00", Value(ini, "LIGHTING", "SUN_ANGLE"));
        Assert.Equal("1.0", Value(ini, "LIGHTING", "TIME_MULT"));
        Assert.Equal("0.200", Value(ini, "LIGHTING", "CLOUD_SPEED"));
        Assert.Equal("26", Value(ini, "TEMPERATURE", "AMBIENT"));
        Assert.Equal("32", Value(ini, "TEMPERATURE", "ROAD"));
        Assert.Equal("3_clear", Value(ini, "WEATHER", "NAME"));
        Assert.Equal("10", Value(ini, "WIND", "SPEED_KMH_MIN"));
        Assert.Equal("10", Value(ini, "WIND", "SPEED_KMH_MAX"));
        Assert.Equal("0", Value(ini, "WIND", "DIRECTION_DEG"));
        Assert.Equal("100", Value(ini, "DYNAMIC_TRACK", "SESSION_START"));
        Assert.Equal("0", Value(ini, "DYNAMIC_TRACK", "RANDOMNESS"));
        Assert.Equal("1", Value(ini, "DYNAMIC_TRACK", "LAP_GAIN"));
        Assert.Equal("50", Value(ini, "DYNAMIC_TRACK", "SESSION_TRANSFER"));
        Assert.Equal("-1", Value(ini, "LAP_INVALIDATOR", "ALLOWED_TYRES_OUT"));
        Assert.Equal("0", Value(ini, "REPLAY", "ACTIVE"));
        Assert.Equal("0", Value(ini, "REMOTE", "ACTIVE"));
        Assert.Null(Value(ini, "CAR_1", "MODEL"));
    }

    [Fact]
    public void Hotlap_records_a_ghost_from_the_hotlap_spawn()
    {
        var ini = new Game.StartProperties {
            BasicProperties = Player(),
            ModeProperties = new Game.HotlapProperties { Duration = 0 },
        }.BuildRaceIni();

        Assert.Equal("2", Value(ini, "HEADER", "__CM_FEATURE_SET"));
        Assert.Equal("4", Value(ini, "SESSION_0", "TYPE"));
        Assert.Equal("HOTLAP_START", Value(ini, "SESSION_0", "SPAWN_SET"));
        Assert.Equal("1", Value(ini, "GHOST_CAR", "PLAYING"));
        Assert.Equal("1", Value(ini, "GHOST_CAR", "RECORDING"));
        Assert.Equal("1", Value(ini, "GHOST_CAR", "LOAD"));
        Assert.Equal("0", Value(ini, "GHOST_CAR", "ENABLED"));
        Assert.Equal("10", Value(ini, "GROOVE", "VIRTUAL_LAPS"));
        Assert.Equal("1", Value(ini, "GROOVE", "MAX_LAPS"));
        Assert.Equal("1", Value(ini, "GROOVE", "STARTING_LAPS"));
    }

    [Fact]
    public void Race_keeps_the_grid_and_leaves_the_feature_set_unset()
    {
        var ini = new Game.StartProperties {
            BasicProperties = Player(),
            ModeProperties = new Game.RaceProperties {
                RaceLaps = 5,
                AiLevel = 90,
                BotCars = [
                    new Game.AiCar {
                        CarId = "ks_ferrari_458",
                        SkinId = "0",
                        DriverName = "AI 1",
                        AiLevel = 90,
                    },
                ],
            },
        }.BuildRaceIni();

        Assert.Null(Value(ini, "HEADER", "__CM_FEATURE_SET"));
        Assert.Equal("2", Value(ini, "RACE", "CARS"));
        Assert.Equal("90", Value(ini, "RACE", "AI_LEVEL"));
        Assert.Equal("5", Value(ini, "RACE", "RACE_LAPS"));
        Assert.Equal("3", Value(ini, "SESSION_0", "TYPE"));
        Assert.Equal("5", Value(ini, "SESSION_0", "LAPS"));
        Assert.Equal("START", Value(ini, "SESSION_0", "SPAWN_SET"));
        Assert.Equal("0", Value(ini, "SESSION_0", "STARTING_POSITION"));
        Assert.Equal("ks_ferrari_458", Value(ini, "CAR_1", "MODEL"));
        Assert.Equal("0", Value(ini, "CAR_1", "SKIN"));
        Assert.Equal("AI 1", Value(ini, "CAR_1", "DRIVER_NAME"));
        Assert.Equal("90", Value(ini, "CAR_1", "AI_LEVEL"));
        Assert.Null(Value(ini, "CAR_1", "EXT_AI_RESTRICTIONS"));
    }

    [Fact]
    public void Track_day_is_a_long_pit_session()
    {
        var ini = new Game.StartProperties {
            BasicProperties = Player(),
            ModeProperties = new Game.TrackdayProperties { BotCars = [] },
        }.BuildRaceIni();

        Assert.Equal(Game.TrackDaySessionName, Value(ini, "SESSION_0", "NAME"));
        Assert.Equal("720", Value(ini, "SESSION_0", "DURATION_MINUTES"));
        Assert.Equal("PIT", Value(ini, "SESSION_0", "SPAWN_SET"));
        Assert.Equal("2", Value(ini, "SESSION_0", "TYPE"));
        Assert.Equal("0", Value(ini, "SESSION_0", "__SPEED_LIMIT"));
        Assert.Null(Value(ini, "CAR_1", "MODEL"));
    }

    [Fact]
    public void Weekend_writes_practice_qualifying_and_race()
    {
        var ini = new Game.StartProperties {
            BasicProperties = Player(),
            ModeProperties = new Game.WeekendProperties {
                PracticeDuration = 10,
                QualificationDuration = 15,
                RaceLaps = 5,
                BotCars = [],
            },
        }.BuildRaceIni();

        Assert.Equal("Practice", Value(ini, "SESSION_0", "NAME"));
        Assert.Equal("1", Value(ini, "SESSION_0", "TYPE"));
        Assert.Equal("10", Value(ini, "SESSION_0", "DURATION_MINUTES"));
        Assert.Equal("Qualifying", Value(ini, "SESSION_1", "NAME"));
        Assert.Equal("2", Value(ini, "SESSION_1", "TYPE"));
        Assert.Equal("15", Value(ini, "SESSION_1", "DURATION_MINUTES"));
        Assert.Equal("Race", Value(ini, "SESSION_2", "NAME"));
        Assert.Equal("3", Value(ini, "SESSION_2", "TYPE"));
        Assert.Equal("5", Value(ini, "SESSION_2", "LAPS"));
        Assert.Equal("START", Value(ini, "SESSION_2", "SPAWN_SET"));
        Assert.Null(Value(ini, "SESSION_3", "NAME"));
    }

    [Fact]
    public void Clear_up_removes_previous_opponents_and_sessions()
    {
        var ini = new IniFile();
        ini["CAR_1"].Set("MODEL", "old");
        ini["SESSION_0"].Set("NAME", "old");
        ini["REMOTE"].Set("__FEATURES", "keep");
        ini["EVENT"].Set("NAME", "old");

        Game.ClearUpIniFile(ini);

        Assert.Null(Value(ini, "CAR_1", "MODEL"));
        Assert.Null(Value(ini, "SESSION_0", "NAME"));
        Assert.Null(Value(ini, "REMOTE", "__FEATURES"));
        Assert.False(ini.ContainsKey("EVENT"));
        Assert.Equal("0", Value(ini, "REPLAY", "ACTIVE"));
    }

    [Fact]
    public void Sun_angle_is_zero_at_13_00_and_16_at_14_00()
    {
        Assert.Equal(0, Game.ConditionProperties.GetSunAngle(13 * 60 * 60));
        Assert.Equal(16, Game.ConditionProperties.GetSunAngle(14 * 60 * 60));
        Assert.Equal(13 * 60 * 60, Game.ConditionProperties.GetSeconds(0));
    }

    [Fact]
    public void Assists_save_under_the_given_documents_directory()
    {
        var root = Directory.CreateTempSubdirectory("actools-assists-writer-").FullName;
        try {
            Directory.CreateDirectory(Path.Combine(root, "cfg"));
            var assists = new Game.AssistsProperties {
                IdealLine = true,
                Abs = AssistState.Factory,
                TractionControl = AssistState.On,
            };
            assists.Set(root);

            var saved = new IniFile(Path.Combine(root, "cfg", "assists.ini"));
            Assert.Equal("1", saved["ASSISTS"].GetPossiblyEmpty("IDEAL_LINE"));
            Assert.Equal("1", saved["ASSISTS"].GetPossiblyEmpty("ABS"));
            Assert.Equal("2", saved["ASSISTS"].GetPossiblyEmpty("TRACTION_CONTROL"));
            Assert.Equal("0", saved["ASSISTS"].GetPossiblyEmpty("AUTO_BRAKE"));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Build_without_a_mode_does_not_write_a_file()
    {
        Assert.Throws<NotSupportedException>(() => new Game.StartProperties().BuildRaceIni());
    }

    private static Game.BasicProperties Player()
    {
        return new Game.BasicProperties {
            CarId = "ks_ferrari_458",
            CarSkinId = "0",
            TrackId = "ks_silverstone",
            DriverName = "Keeper",
        };
    }

    private static Game.ConditionProperties Weather()
    {
        return new Game.ConditionProperties {
            SunAngle = -48,
            TimeMultiplier = 1,
            CloudSpeed = 0.2,
            AmbientTemperature = 26,
            RoadTemperature = 32,
            WeatherName = "3_clear",
            WindSpeedMin = 10,
            WindSpeedMax = 10,
            WindDirectionDeg = 0,
        };
    }

    private static Game.TrackProperties Track()
    {
        return new Game.TrackProperties {
            SessionStart = 100,
            Randomness = 0,
            LapGain = 1,
            SessionTransfer = 50,
        };
    }

    private static string Value(IniFile file, string section, string key)
    {
        return file[section].GetPossiblyEmpty(key);
    }
}
