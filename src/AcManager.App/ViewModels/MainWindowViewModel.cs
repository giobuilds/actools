using AcTools.Platform;
using AcManager.App;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IInstallSettingsStore store, IInstallLocator locator, IGameLauncher launcher, bool openDrive = false)
    {
        Drive = new DriveViewModel(launcher);
        Video = new VideoViewModel();
        Content = new ContentViewModel();
        Settings = new SettingsViewModel();
        Content.Installed = Drive.Rescan;
        Install = new InstallViewModel(store, locator, layout => {
            Drive.UseLayout(layout);
            Video.UseLayout(layout);
            Content.UseLayout(layout);
            Settings.UseLayout(layout);
        });
        Install.Publish();
        CurrentPage = openDrive ? Drive : Install;
    }

    public InstallViewModel Install { get; }

    public DriveViewModel Drive { get; }

    public VideoViewModel Video { get; }

    public ContentViewModel Content { get; }

    public SettingsViewModel Settings { get; }

    public string Version => AppVersion.Current;

    [ObservableProperty]
    private ObservableObject _currentPage = null!;

    public bool IsInstallPage => ReferenceEquals(CurrentPage, Install);

    public bool IsDrivePage => ReferenceEquals(CurrentPage, Drive);

    public bool IsContentPage => ReferenceEquals(CurrentPage, Content);

    public bool IsVideoPage => ReferenceEquals(CurrentPage, Video);

    public bool IsSettingsPage => ReferenceEquals(CurrentPage, Settings);

    [RelayCommand]
    private void ShowInstall() => CurrentPage = Install;

    [RelayCommand]
    private void ShowDrive() => CurrentPage = Drive;

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

    partial void OnCurrentPageChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(IsInstallPage));
        OnPropertyChanged(nameof(IsDrivePage));
        OnPropertyChanged(nameof(IsContentPage));
        OnPropertyChanged(nameof(IsVideoPage));
        OnPropertyChanged(nameof(IsSettingsPage));
    }
}
