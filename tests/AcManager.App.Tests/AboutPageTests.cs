using AcManager.App;
using AcManager.App.ViewModels;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.App.Tests;

public class AboutPageTests
{
    [Fact]
    public async Task About_page_shows_the_licence_and_a_newer_release()
    {
        var about = new AboutViewModel(new FakeReleases("""
            [{"tag_name":"v9.0.0","draft":false,"prerelease":false}]
            """));

        Assert.Contains("Microsoft Public License", about.LicenseText, StringComparison.Ordinal);
        Assert.Contains("Avalonia", about.NoticeText, StringComparison.Ordinal);
        Assert.Contains("CommunityToolkit.Mvvm", about.NoticeText, StringComparison.Ordinal);

        await about.CheckAsync();

        Assert.Equal("Version 9.0.0 is available.", about.UpdateNote);
        Assert.Equal("1.8.0", about.Version);
    }

    [Fact]
    public async Task A_failed_update_check_keeps_a_short_note()
    {
        var about = new AboutViewModel(new ThrowingReleases());

        await about.CheckAsync();

        Assert.Equal("Update check did not reach GitHub.", about.UpdateNote);
    }

    [Fact]
    public async Task About_command_opens_the_page()
    {
        var root = Directory.CreateTempSubdirectory("actools-about-").FullName;
        try {
            var window = new MainWindowViewModel(
                new InstallSettingsStore(new XdgAppPaths(Path.Combine(root, "config"))),
                new FixedLocator(new AcInstallLayout(null, null, null, "test")),
                new IdleLauncher(),
                releases: new FakeReleases("[]"));

            await window.ShowAboutCommand.ExecuteAsync(null);

            Assert.True(window.IsAboutPage);
            Assert.Equal("This is the latest release.", window.About.UpdateNote);
        } finally {
            Directory.Delete(root, true);
        }
    }

    private sealed class FakeReleases : IReleaseLookup
    {
        private readonly string _json;

        public FakeReleases(string json) => _json = json;

        public Task<string> ReadReleasesAsync(CancellationToken cancellationToken) => Task.FromResult(_json);
    }

    private sealed class ThrowingReleases : IReleaseLookup
    {
        public Task<string> ReadReleasesAsync(CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
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
