using AcTools.Core.Data;
using AcTools.Processes;
using OriginalIni = AcTools.DataFile.IniFile;

namespace AcTools.Core.Sessions;

/// <summary>
/// Quick Drive modes from Content Manager's <c>Game.*Properties</c> session writers.
/// </summary>
public enum QuickDriveMode
{
    Practice,
    Hotlap,
    Race,
    TrackDay,
    TimeAttack,
    Drift,
    Drag,
    Weekend,
}

/// <summary>
/// A single-car or small-grid session. <see cref="PracticeRaceIni"/> turns it into race.ini
/// through the Content Manager property writer.
/// </summary>
public sealed record PracticeSession
{
    public QuickDriveMode Mode { get; init; } = QuickDriveMode.Practice;

    public required string CarId { get; init; }

    public required string SkinId { get; init; }

    public required string TrackId { get; init; }

    public string TrackLayoutId { get; init; } = "";

    public string WeatherId { get; init; } = "3_clear";

    public string DriverName { get; init; } = "";

    public double DurationMinutes { get; init; } = 30;

    /// <summary>PIT or START. Practice only. Other modes use the spawn Content Manager writes.</summary>
    public string SpawnSet { get; init; } = "PIT";

    public double SunAngle { get; init; } = -48;

    public int RaceLaps { get; init; } = 5;

    /// <summary>Extra cars using the same model. Race, track day, and weekend.</summary>
    public int Opponents { get; init; }

    public int AiLevel { get; init; } = 90;

    /// <summary>Drag matches. Content Manager's drag writer defaults to 10.</summary>
    public int DragMatches { get; init; } = 10;

    /// <summary>Weekend practice length. Zero omits that session.</summary>
    public int PracticeMinutes { get; init; } = 15;

    /// <summary>Weekend qualifying length. Zero omits that session.</summary>
    public int QualificationMinutes { get; init; } = 30;

    /// <summary>Content Manager's quick-drive temperature default.</summary>
    public double AmbientTemperature { get; init; } = 26;

    /// <summary>Fresh race.ini road temperature from Content Manager's default config.</summary>
    public double RoadTemperature { get; init; } = 32;

    public double TimeMultiplier { get; init; } = 1;

    public double CloudSpeed { get; init; } = 0.2;

    public double WindSpeedMin { get; init; } = 10;

    public double WindSpeedMax { get; init; } = 10;

    public double WindDirection { get; init; }

    /// <summary>Dynamic track from Content Manager's fresh race.ini template.</summary>
    public double TrackSessionStart { get; init; } = 100;

    public double TrackRandomness { get; init; }

    public double TrackLapGain { get; init; } = 1;

    public double TrackSessionTransfer { get; init; } = 50;
}

public static class PracticeRaceIni
{
    public static IniFile Build(PracticeSession session)
    {
        var file = Copy(Create(session).BuildRaceIni());
        ApplyLocalShell(file);
        return file;
    }

    private static Game.StartProperties Create(PracticeSession session)
    {
        var ai = Math.Clamp(session.AiLevel, 0, 100);
        var laps = Math.Max(1, session.RaceLaps);
        var opponents = Math.Clamp(session.Opponents, 0, 40);
        return new Game.StartProperties {
            BasicProperties = new Game.BasicProperties {
                CarId = session.CarId,
                CarSkinId = session.SkinId,
                TrackId = session.TrackId,
                TrackConfigurationId = session.TrackLayoutId,
                DriverName = string.IsNullOrWhiteSpace(session.DriverName) ? null : session.DriverName,
            },
            ModeProperties = Mode(session, ai, laps, opponents),
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
    }

    private static Game.BaseModeProperties Mode(PracticeSession session, int ai, int laps, int opponents)
    {
        return session.Mode switch {
            QuickDriveMode.Practice => new Game.PracticeProperties {
                Duration = session.DurationMinutes,
                StartType = Spawn(session.SpawnSet),
            },
            QuickDriveMode.Hotlap => new Game.HotlapProperties(),
            QuickDriveMode.TimeAttack => new Game.TimeAttackProperties {
                Duration = session.DurationMinutes,
            },
            QuickDriveMode.Drift => new Game.DriftProperties {
                Duration = session.DurationMinutes,
                StartType = Spawn(session.SpawnSet),
            },
            QuickDriveMode.Drag => new Game.DragProperties {
                AiLevel = ai,
                MatchesCount = Math.Max(1, session.DragMatches),
                BotCar = new Game.AiCar {
                    CarId = session.CarId,
                    SkinId = session.SkinId,
                    DriverName = "Bot",
                    AiLevel = ai,
                },
            },
            QuickDriveMode.Race => new Game.RaceProperties {
                AiLevel = ai,
                RaceLaps = laps,
                BotCars = Bots(session, opponents, ai),
            },
            QuickDriveMode.TrackDay => new Game.TrackdayProperties {
                AiLevel = ai,
                RaceLaps = laps,
                BotCars = Bots(session, opponents, ai),
            },
            QuickDriveMode.Weekend => new Game.WeekendProperties {
                AiLevel = ai,
                RaceLaps = laps,
                PracticeDuration = session.PracticeMinutes,
                QualificationDuration = session.QualificationMinutes,
                BotCars = Bots(session, opponents, ai),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(session)),
        };
    }

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

    private static Game.StartType Spawn(string spawnSet)
    {
        if (string.Equals(spawnSet, Game.StartType.RegularStart.Id, StringComparison.Ordinal)) {
            return Game.StartType.RegularStart;
        }

        if (string.Equals(spawnSet, Game.StartType.HotlapStart.Id, StringComparison.Ordinal)) {
            return Game.StartType.HotlapStart;
        }

        return Game.StartType.Pit;
    }

    private static IniFile Copy(OriginalIni source)
    {
        var dest = new IniFile();
        foreach (var section in source) {
            IniSection? target = null;
            foreach (var pair in section.Value) {
                if (pair.Value == null) {
                    continue;
                }

                target ??= dest[section.Key];
                target.SetDirect(pair.Key, pair.Value);
            }
        }

        return dest;
    }

    /// <summary>
    /// Quick Drive replaces race.ini. A fresh <see cref="Game.StartProperties.BuildRaceIni"/>
    /// leaves the replay name and remote identity keys out, so they are written here.
    /// </summary>
    private static void ApplyLocalShell(IniFile file)
    {
        var replay = file["REPLAY"];
        if (replay.Get("FILENAME") == null) {
            replay.Set("FILENAME", "");
        }

        var remote = file["REMOTE"];
        SetIfMissing(remote, "SERVER_IP");
        SetIfMissing(remote, "SERVER_PORT");
        SetIfMissing(remote, "NAME");
        SetIfMissing(remote, "TEAM");
        SetIfMissing(remote, "GUID");
        SetIfMissing(remote, "REQUESTED_CAR");
        SetIfMissing(remote, "PASSWORD");
    }

    private static void SetIfMissing(IniSection section, string key)
    {
        if (section.Get(key) == null) {
            section.Set(key, "");
        }
    }
}

public static class RaceFiles
{
    public static string WriteRaceIni(string documentsDirectory, IniFile ini)
    {
        var path = Path.Combine(documentsDirectory, "cfg", "race.ini");
        if (File.Exists(path)) {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        ini.Save(path);
        return path;
    }
}
