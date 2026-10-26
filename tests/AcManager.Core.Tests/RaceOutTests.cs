using AcManager.Core.Results;
using Xunit;

namespace AcManager.Core.Tests;

public class RaceOutTests
{
    private const string Finished = """
        {
          "track": "ks_silverstone",
          "number_of_sessions": 1,
          "players": [
            {"name": "Driver", "car": "__cm_tmp_ks_ferrari_458_ab12", "skin": "00_red"},
            {"name": "Bot", "car": "ks_ferrari_458"}
          ],
          "sessions": [
            {
              "name": "Quick Race",
              "type": 3,
              "lapsCount": 2,
              "laps": [
                {"lap": 0, "car": 0, "cuts": 0, "time": 102500, "sectors": [25000, 40000, 37500]},
                {"lap": 1, "car": 0, "cuts": 1, "time": 101100, "sectors": [24800, 39000, 37300]},
                {"lap": 0, "car": 1, "cuts": 0, "time": 110000}
              ],
              "lapstotal": [2, 2],
              "bestLaps": [
                {"lap": 1, "car": 0, "time": 101100},
                {"lap": 0, "car": 1, "time": 110000}
              ],
              "raceResult": [0, 1]
            }
          ],
          "extras": [
            {"name": "bestlap", "time": 101100},
            {"name": "drift", "points": 0, "combos": 0, "levels": 0},
            {"name": "drag", "wins": 2, "runs": 3, "reaction_time": 412}
          ]
        }
        """;

    [Fact]
    public void Finished_race_lists_places_player_laps_and_extras()
    {
        var race = RaceOut.Parse(Finished);

        Assert.True(race.IsFinished);
        Assert.Equal("ks_ferrari_458", race.Players[0].CarId);
        Assert.Equal(
            [
                "ks_silverstone",
                "Quick Race, 2 laps",
                "P1 Driver, ks_ferrari_458, best 1:41.100",
                "P2 Bot, ks_ferrari_458, best 1:50.000",
                "Lap 1 1:42.500 (0:25.000, 0:40.000, 0:37.500)",
                "Lap 2 1:41.100, 1 cut (0:24.800, 0:39.000, 0:37.300)",
                "Best lap 1:41.100",
                "Drag 2 wins from 3 runs, reaction 0:00.412",
            ],
            race.Lines());
    }

    [Fact]
    public void A_short_qualification_is_left_unfinished()
    {
        const string json = """
            {
              "track": "ks_monza",
              "number_of_sessions": 1,
              "players": [{"name": "Driver", "car": "ks_ferrari_458"}],
              "sessions": [{
                "name": "Qualify",
                "type": 2,
                "lapsCount": 3,
                "laps": [{"lap": 0, "car": 0, "time": -5}],
                "lapstotal": [1]
              }]
            }
            """;

        var lines = RaceOut.Parse(json).Lines();

        Assert.Contains("This session was left unfinished.", lines);
        Assert.Contains("Qualify, 3 laps", lines);
        Assert.Contains("Lap 1 0:00.000", lines);
    }

    [Fact]
    public void Missing_result_file_is_absent_and_replays_are_newest_first()
    {
        var root = Directory.CreateTempSubdirectory("actools-results-").FullName;
        try {
            Assert.Null(RaceOutFile.Read(root));
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => RaceOut.Parse("{"));

            var replay = Path.Combine(root, "replay");
            var older = Path.Combine(replay, "notes.txt");
            var hotlap = Path.Combine(replay, "temp", "hotlap.acreplay");
            var saved = Path.Combine(replay, "saved.ACREPLAY");
            Directory.CreateDirectory(Path.GetDirectoryName(hotlap)!);
            File.WriteAllText(older, "nope");
            File.WriteAllBytes(hotlap, new byte[1536]);
            File.WriteAllBytes(saved, new byte[2 * 1024 * 1024]);
            File.SetLastWriteTimeUtc(hotlap, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(saved, new DateTime(2026, 10, 2, 15, 30, 0, DateTimeKind.Utc));

            var listed = ReplayLibrary.List(root);

            Assert.Equal(["saved", "temp/hotlap"], listed.Select(file => file.RelativePath));
            Assert.Equal("saved  2.0 MB  2026-10-02 15:30Z", listed[0].Label);
            Assert.Equal("temp/hotlap  2 KB  2026-10-01 12:00Z", listed[1].Label);
        } finally {
            Directory.Delete(root, true);
        }
    }
}
