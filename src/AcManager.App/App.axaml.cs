using AcManager.App.ViewModels;
using AcManager.App.Views;
using AcManager.Core.Bridge;
using AcManager.Core.Online;
using AcTools.Platform.Linux;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AcManager.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var paths = new XdgAppPaths();
            var store = new InstallSettingsStore(paths);
            var locator = new SteamAcDiscovery();
            var launcher = new SteamGameLauncher();
            var openDrive = desktop.Args?.Contains("--drive") == true;
            var invite = InviteLink.TryParseArgs(desktop.Args);
            var window = new MainWindowViewModel(store, locator, launcher, openDrive, live: new SessionBridgeClient());
            desktop.MainWindow = new MainWindow {
                DataContext = window,
            };
            AcmanagerScheme.RegisterCurrentProcess();
            if (invite != null) {
                _ = window.OpenInviteAsync(invite);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
