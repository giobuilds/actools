using AcManager.App.ViewModels;
using AcManager.Core.Settings;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class VideoPageTests
{
    [Fact]
    public void Publish_loads_video_ini_and_save_keeps_unrelated_keys()
    {
        var root = Directory.CreateTempSubdirectory("actools-video-ui-").FullName;
        try {
            var documents = Path.Combine(root, "Documents", "Assetto Corsa");
            var video = VideoSettingsFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(video)!);
            File.WriteAllText(video, "[VIDEO]\r\nFULLSCREEN=1\r\nANISOTROPIC=8\r\nWIDTH=1920\r\nHEIGHT=1080\r\n");
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(game, "content", "cars"));
            Directory.CreateDirectory(Path.Combine(game, "content", "tracks"));
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");

            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(game, documents, null, "Steam")),
                new RecordingLauncher());

            Assert.Equal(1920m, window.Video.Width);
            Assert.Equal(1080m, window.Video.Height);
            Assert.True(window.Video.Fullscreen);

            window.Video.Width = 1600;
            window.Video.Fullscreen = false;
            window.Video.SaveCommand.Execute(null);

            Assert.Contains("ANISOTROPIC=8", File.ReadAllText(video), StringComparison.Ordinal);
            Assert.Contains("WIDTH=1600", File.ReadAllText(video), StringComparison.Ordinal);
            Assert.Contains("FULLSCREEN=0", File.ReadAllText(video), StringComparison.Ordinal);
            Assert.Contains("Saved ", window.Video.Status, StringComparison.Ordinal);
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

    private sealed class RecordingLauncher : IGameLauncher
    {
        public GameLaunchStatus LaunchSession(AcInstallLayout layout) => GameLaunchStatus.ProtonNotFound;

        public bool IsGameRunning() => false;
    }
}
