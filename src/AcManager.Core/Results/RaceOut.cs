using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AcManager.Core.Results;

public sealed record RaceOutPlayer(string? Name, string? CarId, string? SkinId);

public sealed record RaceOutLap(int LapId, int CarNumber, int Cuts, string? Tyre, TimeSpan Time, IReadOnlyList<TimeSpan> Sectors);

public sealed record RaceOutBestLap(int LapId, int CarNumber, TimeSpan Time);

public sealed record RaceOutSession(
    string? Name,
    int Type,
    int LapsCount,
    IReadOnlyList<RaceOutLap> Laps,
    IReadOnlyList<int> LapsTotalPerCar,
    IReadOnlyList<RaceOutBestLap> BestLaps,
    IReadOnlyList<int> RaceResult)
{
    public bool IsFinished => Type switch {
        0 or 1 => true,
        2 or 3 => LapsTotalPerCar.Contains(LapsCount),
        4 or 5 or 6 or 7 => LapsTotalPerCar.Count > 0 && LapsTotalPerCar[0] > 0,
        _ => false,
    };
}

public sealed record RaceOutExtra(string Name, bool IsFinished, string? Detail);

/// <summary>
/// The last session the game wrote to Documents/out/race_out.json.
/// Lap ids and car numbers start at 0. Times in the file are milliseconds.
/// </summary>
public sealed record RaceOut(
    string? TrackId,
    int NumberOfSessions,
    IReadOnlyList<RaceOutPlayer> Players,
    IReadOnlyList<RaceOutSession> Sessions,
    IReadOnlyList<RaceOutExtra> Extras)
{
    public bool IsFinished
    {
        get {
            if (Extras.Any(extra => extra.Name == "__custom_mode" && !extra.IsFinished)) {
                return false;
            }

            return NumberOfSessions == Sessions.Count
                && (Sessions.Any(session => session.IsFinished) || Extras.Any(extra => extra.IsFinished));
        }
    }

    public static RaceOut Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new RaceOut(
            Text(root, "track"),
            Int(root, "number_of_sessions"),
            ReadPlayers(root),
            ReadSessions(root),
            ReadExtras(root));
    }

    public IReadOnlyList<string> Lines()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(TrackId)) {
            lines.Add(TrackId);
        }

        if (!IsFinished) {
            lines.Add("This session was left unfinished.");
        }

        foreach (var session in Sessions) {
            lines.Add(Heading(session));
            foreach (var (place, car) in Places(session)) {
                lines.Add(PlaceLine(place, car, Player(car), Best(session, car)));
            }

            var focus = FocusCar(session);
            if (focus is int carNumber) {
                foreach (var lap in session.Laps.Where(lap => lap.CarNumber == carNumber).OrderBy(lap => lap.LapId)) {
                    lines.Add(LapLine(lap));
                }
            }
        }

        foreach (var extra in Extras) {
            if (!string.IsNullOrWhiteSpace(extra.Detail)) {
                lines.Add(extra.Detail);
            }
        }

        return lines;
    }

    private RaceOutPlayer? Player(int carNumber) =>
        carNumber >= 0 && carNumber < Players.Count ? Players[carNumber] : null;

    private static string Heading(RaceOutSession session)
    {
        var name = string.IsNullOrWhiteSpace(session.Name) ? TypeName(session.Type) : session.Name;
        return session.LapsCount > 0
            ? name + ", " + session.LapsCount.ToString(CultureInfo.InvariantCulture) + " laps"
            : name!;
    }

    private static string PlaceLine(int place, int car, RaceOutPlayer? player, TimeSpan? best)
    {
        var who = player == null
            ? "car " + car.ToString(CultureInfo.InvariantCulture)
            : JoinName(player);
        var line = "P" + (place + 1).ToString(CultureInfo.InvariantCulture) + " " + who;
        if (best is TimeSpan time && time > TimeSpan.Zero) {
            line += ", best " + Format(time);
        }

        return line;
    }

    private static string JoinName(RaceOutPlayer player)
    {
        var name = string.IsNullOrWhiteSpace(player.Name) ? null : player.Name.Trim();
        var car = string.IsNullOrWhiteSpace(player.CarId) ? null : player.CarId.Trim();
        if (name != null && car != null) {
            return name + ", " + car;
        }

        return name ?? car ?? "car";
    }

    private static string LapLine(RaceOutLap lap)
    {
        var line = "Lap " + (lap.LapId + 1).ToString(CultureInfo.InvariantCulture) + " " + Format(lap.Time);
        if (lap.Cuts == 1) {
            line += ", 1 cut";
        } else if (lap.Cuts > 1) {
            line += ", " + lap.Cuts.ToString(CultureInfo.InvariantCulture) + " cuts";
        }

        if (lap.Sectors.Count > 0) {
            line += " (" + string.Join(", ", lap.Sectors.Select(Format)) + ")";
        }

        return line;
    }

    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero) {
            time = TimeSpan.Zero;
        }

        var totalMs = (long)Math.Round(time.TotalMilliseconds);
        var hours = totalMs / 3_600_000;
        var minutes = totalMs / 60_000 % 60;
        var seconds = totalMs / 1000 % 60;
        var millis = totalMs % 1000;
        if (hours > 0) {
            return hours.ToString(CultureInfo.InvariantCulture)
                + ":" + minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":" + seconds.ToString("00", CultureInfo.InvariantCulture)
                + "." + millis.ToString("000", CultureInfo.InvariantCulture);
        }

        return minutes.ToString(CultureInfo.InvariantCulture)
            + ":" + seconds.ToString("00", CultureInfo.InvariantCulture)
            + "." + millis.ToString("000", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<(int Place, int Car)> Places(RaceOutSession session)
    {
        if (session.RaceResult.Count > 0) {
            return session.RaceResult.Select((car, place) => (place, car)).ToArray();
        }

        return session.BestLaps
            .GroupBy(lap => lap.CarNumber)
            .Select(group => group.OrderBy(lap => lap.Time).First())
            .OrderBy(lap => lap.Time)
            .Select((lap, place) => (place, lap.CarNumber))
            .ToArray();
    }

    private static TimeSpan? Best(RaceOutSession session, int car)
    {
        var times = session.BestLaps.Where(lap => lap.CarNumber == car && lap.Time > TimeSpan.Zero).Select(lap => lap.Time).ToArray();
        return times.Length == 0 ? null : times.Min();
    }

    private static int? FocusCar(RaceOutSession session)
    {
        var cars = session.Laps.Select(lap => lap.CarNumber).Distinct().ToArray();
        if (cars.Contains(0)) {
            return 0;
        }

        return cars.Length == 1 ? cars[0] : null;
    }

    private static string TypeName(int type) => type switch {
        0 => "Booking",
        1 => "Practice",
        2 => "Qualification",
        3 => "Race",
        4 => "Hotlap",
        5 => "Time Attack",
        6 => "Drift",
        7 => "Drag",
        _ => "Session",
    };

    private static List<RaceOutPlayer> ReadPlayers(JsonElement root)
    {
        var players = new List<RaceOutPlayer>();
        if (!root.TryGetProperty("players", out var array) || array.ValueKind != JsonValueKind.Array) {
            return players;
        }

        foreach (var player in array.EnumerateArray()) {
            players.Add(new RaceOutPlayer(Text(player, "name"), CarId(Text(player, "car")), Text(player, "skin")));
        }

        return players;
    }

    private static string? CarId(string? carId)
    {
        if (carId == null || !carId.StartsWith("__cm_tmp_", StringComparison.Ordinal)) {
            return carId;
        }

        var match = Regex.Match(carId, @"^__cm_tmp_([\w+._-]+)_[a-f0-9]{2,8}$");
        return match.Success ? match.Groups[1].Value : carId;
    }

    private static List<RaceOutSession> ReadSessions(JsonElement root)
    {
        var sessions = new List<RaceOutSession>();
        if (!root.TryGetProperty("sessions", out var array) || array.ValueKind != JsonValueKind.Array) {
            return sessions;
        }

        foreach (var session in array.EnumerateArray()) {
            sessions.Add(new RaceOutSession(
                Text(session, "name"),
                Int(session, "type"),
                Int(session, "lapsCount"),
                Laps(session),
                Ints(session, "lapstotal"),
                BestLaps(session),
                Ints(session, "raceResult")));
        }

        return sessions;
    }

    private static List<RaceOutLap> Laps(JsonElement session)
    {
        var laps = new List<RaceOutLap>();
        if (!session.TryGetProperty("laps", out var array) || array.ValueKind != JsonValueKind.Array) {
            return laps;
        }

        foreach (var lap in array.EnumerateArray()) {
            laps.Add(new RaceOutLap(
                Int(lap, "lap"),
                Int(lap, "car"),
                Int(lap, "cuts"),
                Text(lap, "tyre"),
                Milliseconds(lap, "time"),
                Times(lap, "sectors")));
        }

        return laps;
    }

    private static List<RaceOutBestLap> BestLaps(JsonElement session)
    {
        var laps = new List<RaceOutBestLap>();
        if (!session.TryGetProperty("bestLaps", out var array) || array.ValueKind != JsonValueKind.Array) {
            return laps;
        }

        foreach (var lap in array.EnumerateArray()) {
            laps.Add(new RaceOutBestLap(Int(lap, "lap"), Int(lap, "car"), Milliseconds(lap, "time")));
        }

        return laps;
    }

    private static List<RaceOutExtra> ReadExtras(JsonElement root)
    {
        var extras = new List<RaceOutExtra>();
        if (!root.TryGetProperty("extras", out var array) || array.ValueKind != JsonValueKind.Array) {
            return extras;
        }

        foreach (var extra in array.EnumerateArray()) {
            var name = Text(extra, "name") ?? "";
            extras.Add(name switch {
                "bestlap" => BestLapExtra(extra),
                "drift" => DriftExtra(extra),
                "drag" => DragExtra(extra),
                "timeattack" => TimeAttackExtra(extra),
                "__custom_mode" => CustomExtra(extra),
                "special_event" => SpecialExtra(extra),
                _ => new RaceOutExtra(name, false, null),
            });
        }

        return extras;
    }

    private static RaceOutExtra BestLapExtra(JsonElement extra)
    {
        var time = Milliseconds(extra, "time");
        return new RaceOutExtra("bestlap", time > TimeSpan.Zero, time > TimeSpan.Zero ? "Best lap " + Format(time) : null);
    }

    private static RaceOutExtra DriftExtra(JsonElement extra)
    {
        var points = Int(extra, "points");
        var combos = Int(extra, "combos");
        var levels = Int(extra, "levels");
        var finished = points > 0 || combos != 0 || levels != 0;
        if (!finished) {
            return new RaceOutExtra("drift", false, null);
        }

        return new RaceOutExtra(
            "drift",
            true,
            "Drift " + points.ToString(CultureInfo.InvariantCulture)
                + " points, combo " + combos.ToString(CultureInfo.InvariantCulture)
                + ", level " + levels.ToString(CultureInfo.InvariantCulture));
    }

    private static RaceOutExtra DragExtra(JsonElement extra)
    {
        var runs = Int(extra, "runs");
        var wins = Int(extra, "wins");
        var reaction = Milliseconds(extra, "reaction_time");
        if (runs <= 0) {
            return new RaceOutExtra("drag", false, null);
        }

        var line = "Drag " + wins.ToString(CultureInfo.InvariantCulture)
            + " wins from " + runs.ToString(CultureInfo.InvariantCulture) + " runs";
        if (reaction > TimeSpan.Zero) {
            line += ", reaction " + Format(reaction);
        }

        return new RaceOutExtra("drag", true, line);
    }

    private static RaceOutExtra TimeAttackExtra(JsonElement extra)
    {
        var points = Int(extra, "points");
        return new RaceOutExtra(
            "timeattack",
            points != 0,
            points == 0 ? null : "Time attack " + points.ToString(CultureInfo.InvariantCulture) + " points");
    }

    private static RaceOutExtra CustomExtra(JsonElement extra)
    {
        var cancelled = Bool(extra, "cancelled");
        var message = Text(extra, "message");
        return new RaceOutExtra("__custom_mode", !cancelled, string.IsNullOrWhiteSpace(message) ? null : message.Trim());
    }

    private static RaceOutExtra SpecialExtra(JsonElement extra)
    {
        var guid = Text(extra, "guid");
        var max = Int(extra, "max");
        var tier = Int(extra, "tier");
        var finished = !string.IsNullOrEmpty(guid) || max != 0 || tier != -1;
        return new RaceOutExtra("special_event", finished, null);
    }

    private static List<int> Ints(JsonElement parent, string name)
    {
        var values = new List<int>();
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) {
            return values;
        }

        foreach (var item in array.EnumerateArray()) {
            values.Add(item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value) ? value : 0);
        }

        return values;
    }

    private static List<TimeSpan> Times(JsonElement parent, string name)
    {
        var values = new List<TimeSpan>();
        if (!parent.TryGetProperty(name, out var element)) {
            return values;
        }

        if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                values.Add(Milliseconds(item));
            }

            return values;
        }

        values.Add(Milliseconds(element));
        return values;
    }

    private static TimeSpan Milliseconds(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var element) ? Milliseconds(element) : TimeSpan.Zero;

    private static TimeSpan Milliseconds(JsonElement element)
    {
        double value;
        if (element.ValueKind == JsonValueKind.Number) {
            if (!element.TryGetDouble(out value)) {
                return TimeSpan.Zero;
            }
        } else if (element.ValueKind == JsonValueKind.String) {
            if (!double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                return TimeSpan.Zero;
            }
        } else {
            return TimeSpan.Zero;
        }

        return value < 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(value);
    }

    private static string? Text(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) {
            return null;
        }

        var text = element.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int Int(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value)
            ? value
            : 0;

    private static bool Bool(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var element)
        && ((element.ValueKind == JsonValueKind.True)
            || (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) && value != 0));
}

public static class RaceOutFile
{
    public static string PathFor(string documentsDirectory) =>
        Path.Combine(documentsDirectory, "out", "race_out.json");

    public static RaceOut? Read(string documentsDirectory)
    {
        var path = PathFor(documentsDirectory);
        if (!File.Exists(path)) {
            return null;
        }

        return RaceOut.Parse(File.ReadAllText(path));
    }
}
