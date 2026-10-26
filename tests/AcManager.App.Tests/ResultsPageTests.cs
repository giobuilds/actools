using AcManager.App.ViewModels;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class ResultsPageTests
{
    [Fact]
    public void Results_page_shows_the_last_session_and_replays()
    {
        var root = Directory.CreateTempSubdirectory("actools-results-ui-").FullName;
        try {
            var documents = Path.Combine(root, "Documents", "Assetto Corsa");
            Directory.CreateDirectory(Path.Combine(documents, "out"));
            File.WriteAllText(Path.Combine(documents, "out", "race_out.json"), """
                {
                  "track": "ks_silverstone",
                  "number_of_sessions": 1,
                  "players": [{"name": "Driver", "car": "ks_ferrari_458"}],
                  "sessions": [{
                    "name": "Practice",
                    "type": 1,
                    "lapsCount": 0,
                    "laps": [{"lap": 0, "car": 0, "time": 90000, "cuts": 2}],
                    "lapstotal": [1]
                  }]
                }
                """);
            var replay = Path.Combine(documents, "replay", "lap.acreplay");
            Directory.CreateDirectory(Path.GetDirectoryName(replay)!);
            File.WriteAllBytes(replay, new byte[100]);
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");

            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
                new IdleLauncher());

            Assert.Contains("ks_silverstone", window.Results.Lines);
            Assert.Contains("Lap 1 1:30.000, 2 cuts", window.Results.Lines);
            Assert.Contains(window.Results.Replays, line => line.StartsWith("lap  ", StringComparison.Ordinal));
            Assert.Contains("out/race_out.json", window.Results.Status, StringComparison.Ordinal);

            window.ShowResultsCommand.Execute(null);
            Assert.True(window.IsResultsPage);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Broken_result_file_keeps_a_short_status()
    {
        var root = Directory.CreateTempSubdirectory("actools-results-bad-").FullName;
        try {
            var documents = Path.Combine(root, "docs");
            Directory.CreateDirectory(Path.Combine(documents, "out"));
            File.WriteAllText(Path.Combine(documents, "out", "race_out.json"), "{");
            var results = new ResultsViewModel();
            results.UseLayout(new AcInstallLayout(null, documents, null, "test"));

            Assert.Empty(results.Lines);
            Assert.Equal("race_out.json could not be read.", results.Status);
        } finally {
            Directory.Delete(root, true);
        }
    }

    private sealed class FixedLocator : IInstallLocator
    {
        private readonly AcInstallLayout _layout;

        public FixedLocator(AcInstallLayout layout) => _layout = layout;

        public AcInstallLayout Locate() => _layout;

        public IReadOnlyList<string> DefaultSteamRoots => ["test-root"];
    }

    private sealed class IdleLauncher : IGameLauncher
    {
        public GameLaunchStatus LaunchSession(AcInstallLayout layout) => GameLaunchStatus.ProtonNotFound;

        public bool IsGameRunning() => false;
    }
}
