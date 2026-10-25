using AcTools.Core.Data;

namespace AcTools.Core.Sessions;

/// <summary>
/// A single-car practice session written in the same race.ini shape as
/// <c>Game.PracticeProperties</c> in the Windows app.
/// </summary>
public sealed class PracticeSession
{
    public required string CarId { get; init; }

    public required string SkinId { get; init; }

    public required string TrackId { get; init; }

    public string TrackLayoutId { get; init; } = "";

    public string WeatherId { get; init; } = "3_clear";

    public string DriverName { get; init; } = "";

    public double DurationMinutes { get; init; } = 30;

    /// <summary>PIT or START. Matches Content Manager's spawn set ids.</summary>
    public string SpawnSet { get; init; } = "PIT";

    public double SunAngle { get; init; } = -48;
}

public static class PracticeRaceIni
{
    public static IniFile Build(PracticeSession session)
    {
        var file = new IniFile();

        file["HEADER"].Set("__CM_FEATURE_SET", 2);

        var race = file["RACE"];
        race.SetId("MODEL", session.CarId);
        race.SetId("MODEL_CONFIG", "");
        race.SetId("SKIN", session.SkinId);
        race.SetId("TRACK", session.TrackId);
        race.SetId("CONFIG_TRACK", session.TrackLayoutId);
        race.Set("CARS", 1);
        race.Set("AI_LEVEL", 100);
        race.Set("DRIFT_MODE", false);
        race.Set("FIXED_SETUP", false);
        race.Set("PENALTIES", true);
        race.Set("JUMP_START_PENALTY", 0);

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

        var ghost = file["GHOST_CAR"];
        ghost.Set("RECORDING", false);
        ghost.Set("PLAYING", false);
        ghost.Set("SECONDS_ADVANTAGE", 0);
        ghost.Set("LOAD", false);
        ghost.Set("FILE", "");
        ghost.Set("ENABLED", false);

        var groove = file["GROOVE"];
        groove.Set("VIRTUAL_LAPS", 10);
        groove.Set("MAX_LAPS", 30);
        groove.Set("STARTING_LAPS", 0);

        var sessionSection = file["SESSION_0"];
        sessionSection.Set("NAME", "Practice");
        sessionSection.Set("TYPE", 1);
        sessionSection.Set("DURATION_MINUTES", session.DurationMinutes);
        sessionSection.Set("SPAWN_SET", session.SpawnSet);

        file["WEATHER"].Set("NAME", session.WeatherId);
        file["LIGHTING"].Set("SUN_ANGLE", session.SunAngle);
        file["LIGHTING"].Set("CLOUD_SPEED", 0.2);
        file["OPTIONS"].Set("USE_MPH", false);

        return file;
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
