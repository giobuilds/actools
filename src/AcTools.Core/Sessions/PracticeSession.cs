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

    /// <summary>Extra cars using the same model. Race and track day only.</summary>
    public int Opponents { get; init; }

    public int AiLevel { get; init; } = 90;
}

public static class PracticeRaceIni
{
    public static IniFile Build(PracticeSession session)
    {
        var file = new IniFile();
        var mode = session.Mode;
        var grid = mode is QuickDriveMode.Race or QuickDriveMode.TrackDay;
        var opponents = grid ? Math.Clamp(session.Opponents, 0, 40) : 0;
        var aiLevel = Math.Clamp(session.AiLevel, 0, 100);
        var laps = Math.Max(1, session.RaceLaps);

        var header = file["HEADER"];
        header.Set("__CM_FEATURE_SET", 2);
        header.Set("VERSION", 2);

        var race = file["RACE"];
        race.SetId("MODEL", session.CarId);
        race.SetId("MODEL_CONFIG", "");
        race.SetId("SKIN", session.SkinId);
        race.SetId("TRACK", session.TrackId);
        race.SetId("CONFIG_TRACK", session.TrackLayoutId);
        race.Set("CARS", opponents + 1);
        race.Set("AI_LEVEL", grid ? aiLevel : 100);
        if (mode == QuickDriveMode.Race) {
            race.Set("RACE_LAPS", laps);
        }

        race.Set("DRIFT_MODE", false);
        race.Set("FIXED_SETUP", false);
        race.Set("PENALTIES", true);
        race.Set("JUMP_START_PENALTY", 0);

        WritePlayer(file, session);
        for (var index = 1; index <= opponents; index++) {
            WriteBot(file, index, session, aiLevel);
        }

        WriteGhost(file, mode == QuickDriveMode.Hotlap);
        WriteGroove(file, mode == QuickDriveMode.Hotlap);
        WriteSession(file, session, mode, laps);

        file["WEATHER"].Set("NAME", session.WeatherId);
        var lighting = file["LIGHTING"];
        lighting.Set("SUN_ANGLE", session.SunAngle);
        lighting.Set("CLOUD_SPEED", 0.2);
        lighting.Set("TIME_MULT", 1);
        file["OPTIONS"].Set("USE_MPH", false);

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
        car.Set("DRIVER_NAME", session.DriverName);
        car.Set("NATION_CODE", "");
        car.Set("NATIONALITY", "");
    }

    private static void WriteBot(IniFile file, int index, PracticeSession session, int aiLevel)
    {
        var car = file["CAR_" + index];
        car.SetId("MODEL", session.CarId);
        car.SetId("SKIN", session.SkinId);
        car.Set("SETUP", "");
        car.Set("MODEL_CONFIG", "");
        car.Set("AI_LEVEL", aiLevel);
        car.Set("AI_AGGRESSION", 0);
        car.Set("DRIVER_NAME", "AI " + index);
        car.Set("BALLAST", 0);
        car.Set("RESTRICTOR", 0);
        car.Set("NATION_CODE", "");
        car.Set("NATIONALITY", "");
    }

    private static void WriteGhost(IniFile file, bool hotlap)
    {
        var ghost = file["GHOST_CAR"];
        ghost.Set("RECORDING", hotlap);
        ghost.Set("PLAYING", hotlap);
        ghost.Set("SECONDS_ADVANTAGE", 0);
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
            default:
                section.Set("NAME", "Practice");
                section.Set("TYPE", 1);
                section.Set("DURATION_MINUTES", session.DurationMinutes);
                section.Set("SPAWN_SET", string.IsNullOrWhiteSpace(session.SpawnSet) ? "PIT" : session.SpawnSet);
                break;
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
