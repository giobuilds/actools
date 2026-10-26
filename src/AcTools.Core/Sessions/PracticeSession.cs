using AcTools.Core.Data;

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
/// A single-car or small-grid session written in the same race.ini shape as the Windows app.
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
        var file = new IniFile();
        var mode = session.Mode;
        var grid = mode is QuickDriveMode.Race or QuickDriveMode.TrackDay or QuickDriveMode.Weekend;
        var opponents = grid ? Math.Clamp(session.Opponents, 0, 40) : 0;
        var aiLevel = Math.Clamp(session.AiLevel, 0, 100);
        var laps = Math.Max(1, session.RaceLaps);
        var usesChosenAi = grid || mode == QuickDriveMode.Drag;

        var header = file["HEADER"];
        header.Set("VERSION", 2);
        if (mode is QuickDriveMode.Practice or QuickDriveMode.Hotlap or QuickDriveMode.TimeAttack or QuickDriveMode.Drift) {
            header.Set("__CM_FEATURE_SET", 2);
        }

        var race = file["RACE"];
        race.SetId("MODEL", session.CarId);
        race.SetId("MODEL_CONFIG", "");
        race.SetId("SKIN", session.SkinId);
        race.SetId("TRACK", session.TrackId);
        race.SetId("CONFIG_TRACK", session.TrackLayoutId);
        race.Set("CARS", mode == QuickDriveMode.Drag ? 2 : opponents + 1);
        race.Set("AI_LEVEL", usesChosenAi ? aiLevel : 100);
        if (mode is QuickDriveMode.Race or QuickDriveMode.Weekend) {
            race.Set("RACE_LAPS", laps);
        }

        race.Set("DRIFT_MODE", false);
        race.Set("FIXED_SETUP", false);
        race.Set("PENALTIES", true);
        race.Set("JUMP_START_PENALTY", 0);

        WritePlayer(file, session);
        if (mode == QuickDriveMode.Drag) {
            WriteBot(file, 1, session, aiLevel, "Bot");
        } else {
            for (var index = 1; index <= opponents; index++) {
                WriteBot(file, index, session, aiLevel, "AI " + index);
            }
        }

        WriteGhost(file, mode == QuickDriveMode.Hotlap);
        WriteGroove(file, mode is QuickDriveMode.Hotlap or QuickDriveMode.TimeAttack or QuickDriveMode.Drift or QuickDriveMode.Drag);
        WriteSession(file, session, mode, laps);
        WriteConditions(file, session);

        file["OPTIONS"].Set("USE_MPH", false);
        file["BENCHMARK"].Set("ACTIVE", false);
        file["RESTART"].Set("ACTIVE", false);

        var replay = file["REPLAY"];
        replay.Set("FILENAME", "");
        replay.Set("ACTIVE", false);

        var remote = file["REMOTE"];
        remote.Set("ACTIVE", false);
        remote.Set("SERVER_IP", "");
        remote.Set("SERVER_PORT", "");
        remote.Set("NAME", "");
        remote.Set("TEAM", "");
        remote.Set("GUID", "");
        remote.Set("REQUESTED_CAR", "");
        remote.Set("PASSWORD", "");

        file["LAP_INVALIDATOR"].Set("ALLOWED_TYRES_OUT", -1);
        return file;
    }

    private static void WritePlayer(IniFile file, PracticeSession session)
    {
        var car = file["CAR_0"];
        car.Set("SETUP", "");
        car.SetId("SKIN", session.SkinId);
        car.Set("MODEL", "-");
        car.Set("MODEL_CONFIG", "");
        car.Set("BALLAST", 0);
        car.Set("RESTRICTOR", 0);
        if (!string.IsNullOrEmpty(session.DriverName)) {
            car.Set("DRIVER_NAME", session.DriverName);
        }
    }

    private static void WriteBot(IniFile file, int index, PracticeSession session, int aiLevel, string driverName)
    {
        var car = file["CAR_" + index];
        car.SetId("MODEL", session.CarId);
        car.SetId("SKIN", session.SkinId);
        car.Set("SETUP", "");
        car.Set("MODEL_CONFIG", "");
        car.Set("AI_LEVEL", aiLevel);
        car.Set("AI_AGGRESSION", 0);
        car.Set("DRIVER_NAME", driverName);
        car.Set("BALLAST", 0);
        car.Set("RESTRICTOR", 0);
    }

    private static void WriteGhost(IniFile file, bool hotlap)
    {
        var ghost = file["GHOST_CAR"];
        ghost.Set("RECORDING", hotlap);
        ghost.Set("PLAYING", hotlap);
        if (hotlap) {
            ghost.Set("SECONDS_ADVANTAGE", 0d, "0.###");
        }

        ghost.Set("LOAD", hotlap);
        ghost.Set("FILE", "");
        ghost.Set("ENABLED", false);
    }

    private static void WriteGroove(IniFile file, bool hotlap)
    {
        var groove = file["GROOVE"];
        groove.Set("VIRTUAL_LAPS", 10);
        groove.Set("MAX_LAPS", hotlap ? 1 : 30);
        groove.Set("STARTING_LAPS", hotlap ? 1 : 0);
    }

    private static void WriteSession(IniFile file, PracticeSession session, QuickDriveMode mode, int laps)
    {
        if (mode == QuickDriveMode.Weekend) {
            WriteWeekend(file, session, laps);
            return;
        }

        var section = file["SESSION_0"];
        switch (mode) {
            case QuickDriveMode.Hotlap:
                section.Set("NAME", "Hotlap");
                section.Set("TYPE", 4);
                section.Set("DURATION_MINUTES", 0);
                section.Set("SPAWN_SET", "HOTLAP_START");
                break;
            case QuickDriveMode.Race:
                section.Set("NAME", "Quick Race");
                section.Set("DURATION_MINUTES", 0);
                section.Set("SPAWN_SET", "START");
                section.Set("TYPE", 3);
                section.Set("LAPS", laps);
                section.Set("STARTING_POSITION", 0);
                break;
            case QuickDriveMode.TrackDay:
                section.Set("NAME", "Track Day");
                section.Set("DURATION_MINUTES", 720);
                section.Set("SPAWN_SET", "PIT");
                section.Set("TYPE", 2);
                section.Set("__SPEED_LIMIT", 0);
                break;
            case QuickDriveMode.TimeAttack:
                section.Set("NAME", "Time Attack");
                section.Set("TYPE", 5);
                section.Set("DURATION_MINUTES", session.DurationMinutes);
                section.Set("SPAWN_SET", "START");
                break;
            case QuickDriveMode.Drift:
                section.Set("NAME", "Drift Session");
                section.Set("TYPE", 6);
                section.Set("DURATION_MINUTES", session.DurationMinutes);
                section.Set("SPAWN_SET", string.IsNullOrWhiteSpace(session.SpawnSet) ? "PIT" : session.SpawnSet);
                break;
            case QuickDriveMode.Drag:
                section.Set("NAME", "Drag Race");
                section.Set("TYPE", 7);
                section.Set("SPAWN_SET", "START");
                section.Set("MATCHES", Math.Max(1, session.DragMatches));
                break;
            default:
                section.Set("NAME", "Practice");
                section.Set("TYPE", 1);
                section.Set("DURATION_MINUTES", session.DurationMinutes);
                section.Set("SPAWN_SET", string.IsNullOrWhiteSpace(session.SpawnSet) ? "PIT" : session.SpawnSet);
                break;
        }
    }

    private static void WriteWeekend(IniFile file, PracticeSession session, int laps)
    {
        var index = 0;
        if (session.PracticeMinutes > 0) {
            var practice = file["SESSION_" + index];
            practice.Set("NAME", "Practice");
            practice.Set("DURATION_MINUTES", session.PracticeMinutes);
            practice.Set("SPAWN_SET", "PIT");
            practice.Set("TYPE", 1);
            index++;
        }

        if (session.QualificationMinutes > 0) {
            var qualifying = file["SESSION_" + index];
            qualifying.Set("NAME", "Qualifying");
            qualifying.Set("DURATION_MINUTES", session.QualificationMinutes);
            qualifying.Set("SPAWN_SET", "PIT");
            qualifying.Set("TYPE", 2);
            index++;
        }

        var race = file["SESSION_" + index];
        race.Set("NAME", "Race");
        race.Set("DURATION_MINUTES", 0);
        race.Set("SPAWN_SET", "START");
        race.Set("TYPE", 3);
        race.Set("LAPS", laps);
    }

    private static void WriteConditions(IniFile file, PracticeSession session)
    {
        var temperature = file["TEMPERATURE"];
        temperature.Set("AMBIENT", session.AmbientTemperature, "0");
        temperature.Set("ROAD", session.RoadTemperature, "0");

        var lighting = file["LIGHTING"];
        lighting.Set("SUN_ANGLE", session.SunAngle, "0.00");
        lighting.Set("TIME_MULT", session.TimeMultiplier, "0.0");
        lighting.Set("CLOUD_SPEED", session.CloudSpeed, "0.000");

        file["WEATHER"].SetId("NAME", session.WeatherId);

        var wind = file["WIND"];
        wind.Set("SPEED_KMH_MIN", session.WindSpeedMin);
        wind.Set("SPEED_KMH_MAX", session.WindSpeedMax);
        wind.Set("DIRECTION_DEG", session.WindDirection);

        var track = file["DYNAMIC_TRACK"];
        track.Set("SESSION_START", session.TrackSessionStart);
        track.Set("RANDOMNESS", session.TrackRandomness);
        track.Set("LAP_GAIN", session.TrackLapGain);
        track.Set("SESSION_TRANSFER", session.TrackSessionTransfer);
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
