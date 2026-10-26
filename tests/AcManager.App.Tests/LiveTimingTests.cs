using AcManager.App.ViewModels;
using AcManager.Core.Bridge;
using AcTools.Platform;
using Xunit;

namespace AcManager.App.Tests;

public class LiveTimingTests
{
    [Fact]
    public void Drive_page_shows_the_bridge_note_and_stays_quiet_without_one()
    {
        var quiet = new DriveViewModel(new IdleLauncher());
        quiet.RefreshLive();
        Assert.Equal(SessionBridgeProtocol.OffNote, quiet.LiveNote);

        var drive = new DriveViewModel(new IdleLauncher(), new FixedBridge("Live timing: ks_ferrari_458 on ks_silverstone, 3 laps completed, position 1, gear 2, 142.5 km/h."));
        drive.RefreshLive();
        Assert.Contains("ks_ferrari_458 on ks_silverstone", drive.LiveNote, StringComparison.Ordinal);
        Assert.Contains("gear 2", drive.LiveNote, StringComparison.Ordinal);
    }

    private sealed class FixedBridge : ISessionBridge
    {
        private readonly string _note;

        public FixedBridge(string note) => _note = note;

        public string ReadNote() => _note;
    }

    private sealed class IdleLauncher : IGameLauncher
    {
        public GameLaunchStatus LaunchSession(AcInstallLayout layout) => GameLaunchStatus.ProtonNotFound;

        public bool IsGameRunning() => false;
    }
}
