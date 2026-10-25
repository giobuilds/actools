using AcManager.Core.Settings;
using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class VideoViewModel : ObservableObject
{
    private string? _documents;

    [ObservableProperty]
    private decimal _width = VideoSettings.Default.Width;

    [ObservableProperty]
    private decimal _height = VideoSettings.Default.Height;

    [ObservableProperty]
    private bool _fullscreen = VideoSettings.Default.Fullscreen;

    [ObservableProperty]
    private string _status = "Set the Documents directory on the Install page. video.ini lives in that folder.";

    [ObservableProperty]
    private bool _canSave;

    public void UseLayout(AcInstallLayout layout)
    {
        _documents = layout.DocumentsDirectory;
        Reload();
    }

    [RelayCommand]
    public void Reload()
    {
        if (string.IsNullOrWhiteSpace(_documents)) {
            CanSave = false;
            Status = "Set the Documents directory on the Install page. video.ini lives in that folder.";
            return;
        }

        try {
            var settings = VideoSettingsFile.Read(_documents);
            Width = settings.Width;
            Height = settings.Height;
            Fullscreen = settings.Fullscreen;
            CanSave = true;
            Status = "Using " + VideoSettingsFile.PathFor(_documents);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            CanSave = false;
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_documents)) {
            Status = "Set the Documents directory on the Install page. video.ini lives in that folder.";
            return;
        }

        var width = (int)Width;
        var height = (int)Height;
        if (width < 320 || height < 200) {
            Status = "Width and height need to be at least 320 by 200.";
            return;
        }

        try {
            var path = VideoSettingsFile.Write(_documents, new VideoSettings(width, height, Fullscreen));
            Status = "Saved " + path + ". The next session uses this resolution.";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Status = ex.Message;
        }
    }
}
