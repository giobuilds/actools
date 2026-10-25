using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IInstallSettingsStore store, IInstallLocator locator, IGameLauncher launcher, bool openDrive = false)
    {
        Drive = new DriveViewModel(launcher);
        Video = new VideoViewModel();
        Install = new InstallViewModel(store, locator, layout => {
            Drive.UseLayout(layout);
            Video.UseLayout(layout);
        });
        Install.Publish();
        CurrentPage = openDrive ? Drive : Install;
    }

    public InstallViewModel Install { get; }

    public DriveViewModel Drive { get; }

    public VideoViewModel Video { get; }

    [ObservableProperty]
    private ObservableObject _currentPage = null!;

    public bool IsInstallPage => ReferenceEquals(CurrentPage, Install);

    public bool IsDrivePage => ReferenceEquals(CurrentPage, Drive);

    public bool IsVideoPage => ReferenceEquals(CurrentPage, Video);

    [RelayCommand]
    private void ShowInstall() => CurrentPage = Install;

    [RelayCommand]
    private void ShowDrive() => CurrentPage = Drive;

    [RelayCommand]
    private void ShowVideo()
    {
        Video.Reload();
        CurrentPage = Video;
    }

    partial void OnCurrentPageChanged(ObservableObject value)
    {
        OnPropertyChanged(nameof(IsInstallPage));
        OnPropertyChanged(nameof(IsDrivePage));
        OnPropertyChanged(nameof(IsVideoPage));
    }
}
