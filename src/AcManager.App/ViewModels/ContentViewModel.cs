using AcManager.Core.Content;
using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class ContentViewModel : ObservableObject
{
    private AcInstallLayout _layout = new(null, null, null, "");
    private int _installing;

    [ObservableProperty]
    private string _archivePath = "";

    [ObservableProperty]
    private string _status = "Choose a .zip that contains a car or a track.";

    public Action? Installed { get; set; }

    public void UseLayout(AcInstallLayout layout) => _layout = layout;

    private bool CanInstall() => _installing == 0;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task Install()
    {
        if (_installing != 0) {
            return;
        }

        var path = ArchivePath.Trim();
        if (path.Length == 0) {
            Status = "Choose a .zip file.";
            return;
        }

        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            Status = "Only .zip archives can be installed. 7z is not supported yet.";
            return;
        }

        if (!_layout.HasGameRoot || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            Status = "Set the game folder on the Install page before installing content.";
            return;
        }

        var gameRoot = _layout.GameRoot;
        _installing = 1;
        InstallCommand.NotifyCanExecuteChanged();
        Status = "Installing…";
        try {
            var result = await Task.Run(() => ContentArchiveInstaller.Install(path, gameRoot));
            Status = Describe(result);
            if (result.Installed.Count > 0) {
                Installed?.Invoke();
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) {
            Status = ex.Message;
        } finally {
            _installing = 0;
            InstallCommand.NotifyCanExecuteChanged();
        }
    }

    private static string Describe(ContentInstallResult result)
    {
        var lines = new List<string>(result.Installed.Count + result.Skipped.Count);
        foreach (var piece in result.Installed) {
            var line = "Installed " + piece.Kind + " " + piece.Id + ".";
            if (!string.IsNullOrEmpty(piece.Note)) {
                line += " " + piece.Note;
            }

            lines.Add(line);
        }

        foreach (var skip in result.Skipped) {
            lines.Add(skip);
        }

        return lines.Count == 0 ? "Nothing was installed." : string.Join(Environment.NewLine, lines);
    }
}
