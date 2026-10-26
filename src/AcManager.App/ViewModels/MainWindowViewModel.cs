using AcManager.Core.Bridge;
using AcManager.Core.Content;
using AcManager.Core.Online;
using AcTools.Platform;
using AcManager.App;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(
        IInstallSettingsStore store,
        IInstallLocator locator,
        IGameLauncher launcher,
        bool openDrive = false,
        IOnlineDirectory? servers = null,
        ISteamIdLookup? steamIds = null,
        IServerContentInstaller? contentInstaller = null,
        ISessionBridge? live = null)
    {
        var ids = steamIds ?? new SteamLoginUsersLookup();
        Drive = new DriveViewModel(launcher, live);
        Video = new VideoViewModel();
        Content = new ContentViewModel();
        Settings = new SettingsViewModel();
        Csp = new CspViewModel();
        Online = new OnlineViewModel(launcher, servers ?? new HttpLobbyDirectory(ids.MostRecentId), ids, contentInstaller);
        Content.Installed = Drive.Rescan;
        Online.Installed = () => {
            Drive.Rescan();
            Content.Reload();
        };
        Install = new InstallViewModel(store, locator, layout => {
            Drive.UseLayout(layout);
            Video.UseLayout(layout);
            Content.UseLayout(layout);
            Settings.UseLayout(layout);
            Csp.UseLayout(layout);
            Online.UseLayout(layout);
        });
        Install.Publish();
        if (openDrive) {
            Drive.RefreshLive();
        }

        CurrentPage = openDrive ? Drive : Install;
    }

    public InstallViewModel Install { get; }

    public DriveViewModel Drive { get; }

    public VideoViewModel Video { get; }

    public ContentViewModel Content { get; }

    public SettingsViewModel Settings { get; }

    public CspViewModel Csp { get; }

    public OnlineViewModel Online { get; }

    public string Version => AppVersion.Current;

    [ObservableProperty]
    private ObservableObject _currentPage = null!;

    public bool IsInstallPage => ReferenceEquals(CurrentPage, Install);

    public bool IsDrivePage => ReferenceEquals(CurrentPage, Drive);

    public bool IsContentPage => ReferenceEquals(CurrentPage, Content);

    public bool IsVideoPage => ReferenceEquals(CurrentPage, Video);

    public bool IsSettingsPage => ReferenceEquals(CurrentPage, Settings);

    public bool IsCspPage => ReferenceEquals(CurrentPage, Csp);

    public bool IsOnlinePage => ReferenceEquals(CurrentPage, Online);

    [RelayCommand]
    private void ShowInstall() => CurrentPage = Install;

    [RelayCommand]
    private void ShowDrive()
    {
        Drive.RefreshLive();
        CurrentPage = Drive;
    }

    [RelayCommand]
    private void ShowContent()
    {
        Content.Reload();
        CurrentPage = Content;
    }

    [RelayCommand]
    private void ShowVideo()
    {
        Video.Reload();
        CurrentPage = Video;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        Settings.Reload();
        CurrentPage = Settings;
    }

    [RelayCommand]
    private void ShowCsp()
    {
        Csp.Reload();
        CurrentPage = Csp;
    }

    [RelayCommand]
    private void ShowOnline()
    {
        Online.Reload();
        CurrentPage = Online;
    }

    public Task OpenInviteAsync(OnlineInvite invite)
    {
        CurrentPage = Online;
        return Online.OpenInviteAsync(invite);
    }

    partial void OnCurrentPageChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(IsInstallPage));
        OnPropertyChanged(nameof(IsDrivePage));
        OnPropertyChanged(nameof(IsContentPage));
        OnPropertyChanged(nameof(IsVideoPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(IsCspPage));
        OnPropertyChanged(nameof(IsOnlinePage));
    }
}
