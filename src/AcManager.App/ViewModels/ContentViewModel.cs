using AcManager.Core.Content;
using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class ContentViewModel : ObservableObject
{
    private AcInstallLayout _layout = new(null, null, null, "");
    private int _busy;
    private string? _pendingKind;
    private string? _pendingId;

    [ObservableProperty]
    private string _archivePath = "";

    [ObservableProperty]
    private string _status = "Choose a .zip or .7z that contains a car or a track.";

    [ObservableProperty]
    private string _removalNote = "Set the game folder on the Install page before removing content.";

    [ObservableProperty]
    private string _removeCarText = "Remove car";

    [ObservableProperty]
    private string _removeTrackText = "Remove track";

    [ObservableProperty]
    private IReadOnlyList<RemovablePiece> _cars = [];

    [ObservableProperty]
    private IReadOnlyList<RemovablePiece> _tracks = [];

    [ObservableProperty]
    private RemovablePiece? _selectedCar;

    [ObservableProperty]
    private RemovablePiece? _selectedTrack;

    [ObservableProperty]
    private IReadOnlyList<ContentRepair> _repairs = [];

    [ObservableProperty]
    private ContentRepair? _selectedRepair;

    public bool ShowNoCars => Cars.Count == 0;

    public bool ShowNoTracks => Tracks.Count == 0;

    public bool ShowRepairList => Repairs.Count > 0;

    public string RepairNote { get; } = "Check looks at loose ini files. Packed data.acd archives stay unchanged.";

    public Action? Installed { get; set; }

    public void UseLayout(AcInstallLayout layout)
    {
        _layout = layout;
        Reload();
    }

    public void Reload()
    {
        ClearPending();
        Repairs = [];
        SelectedRepair = null;
        if (!_layout.HasGameRoot || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            Cars = [];
            Tracks = [];
            RemovalNote = "Set the game folder on the Install page before removing content.";
            return;
        }

        var library = ContentRemoval.List(_layout.GameRoot);
        var carId = SelectedCar?.Id;
        var trackId = SelectedTrack?.Id;
        Cars = library.Cars;
        Tracks = library.Tracks;
        SelectedCar = carId == null
            ? Cars.FirstOrDefault()
            : Cars.FirstOrDefault(item => item.Id == carId) ?? Cars.FirstOrDefault();
        SelectedTrack = trackId == null
            ? Tracks.FirstOrDefault()
            : Tracks.FirstOrDefault(item => item.Id == trackId) ?? Tracks.FirstOrDefault();
        RemovalNote = library.OfficialCarsKnown
            ? "These folders were added on top of the original game. Removing one deletes that folder."
            : "content/sfx/GUIDs.txt is missing or empty, so cars stay hidden. Tracks that are not marked original can still be removed.";
    }

    private bool CanInstall() => _busy == 0;

    private bool CanRemoveCar() => _busy == 0 && SelectedCar != null;

    private bool CanRemoveTrack() => _busy == 0 && SelectedTrack != null;

    private bool CanCheckRepairs() => _busy == 0;

    private bool CanApplyRepair() => _busy == 0 && SelectedRepair != null;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task Install()
    {
        if (_busy != 0) {
            return;
        }

        var path = ArchivePath.Trim();
        if (path.Length == 0) {
            Status = "Choose a .zip or .7z file.";
            return;
        }

        var zip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var sevenZip = path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase);
        if (!zip && !sevenZip) {
            Status = "Only .zip and .7z archives can be installed.";
            return;
        }

        if (!_layout.HasGameRoot || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            Status = "Set the game folder on the Install page before installing content.";
            return;
        }

        var gameRoot = _layout.GameRoot;
        BeginBusy();
        Status = "Installing…";
        try {
            var result = zip
                ? await Task.Run(() => ContentArchiveInstaller.Install(path, gameRoot))
                : await ContentArchiveInstaller.InstallSevenZipAsync(path, gameRoot);
            Status = Describe(result);
            if (result.Installed.Count > 0) {
                Reload();
                Installed?.Invoke();
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) {
            Status = ex.Message;
        } finally {
            EndBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveCar))]
    private async Task RemoveCar()
    {
        var car = SelectedCar;
        if (car == null) {
            Status = "Choose a car to remove.";
            return;
        }

        if (_pendingKind != "car" || _pendingId != car.Id) {
            SetPending("car", car.Id);
            Status = "Remove car " + car.Name + " (" + car.Id + ")? Press the button again to delete its folder.";
            return;
        }

        await Remove("car", car.Id);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveTrack))]
    private async Task RemoveTrack()
    {
        var track = SelectedTrack;
        if (track == null) {
            Status = "Choose a track to remove.";
            return;
        }

        if (_pendingKind != "track" || _pendingId != track.Id) {
            SetPending("track", track.Id);
            Status = "Remove track " + track.Name + " (" + track.Id + ")? Press the button again to delete its folder.";
            return;
        }

        await Remove("track", track.Id);
    }

    [RelayCommand(CanExecute = nameof(CanCheckRepairs))]
    private async Task CheckRepairs()
    {
        if (_busy != 0) {
            return;
        }

        if (!_layout.HasGameRoot || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            Status = "Set the game folder on the Install page before checking for repairs.";
            return;
        }

        var gameRoot = _layout.GameRoot;
        BeginBusy();
        Status = "Checking loose ini files…";
        try {
            var found = await Task.Run(() => ContentRepairs.Scan(gameRoot));
            ShowRepairs(found);
            Status = found.Count == 0
                ? "No file repairs found. Packed data.acd archives were left unchanged."
                : found.Count + " file repairs found.";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Status = ex.Message;
        } finally {
            EndBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyRepair))]
    private async Task ApplyRepair()
    {
        var repair = SelectedRepair;
        if (repair == null || _busy != 0 || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            return;
        }

        var gameRoot = _layout.GameRoot;
        BeginBusy();
        Status = "Applying " + repair.Title + "…";
        try {
            await Task.Run(() => ContentRepairs.Apply(gameRoot, repair));
            var found = await Task.Run(() => ContentRepairs.Scan(gameRoot));
            ShowRepairs(found);
            Status = "Updated " + repair.RelativePath + ".";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) {
            Status = ex.Message;
        } finally {
            EndBusy();
        }
    }

    private void ShowRepairs(IReadOnlyList<ContentRepair> found)
    {
        var kind = SelectedRepair?.Kind;
        var path = SelectedRepair?.RelativePath;
        Repairs = found;
        SelectedRepair = path == null
            ? found.FirstOrDefault()
            : found.FirstOrDefault(item => item.Kind == kind && item.RelativePath == path) ?? found.FirstOrDefault();
    }

    private async Task Remove(string kind, string id)
    {
        if (_busy != 0 || string.IsNullOrWhiteSpace(_layout.GameRoot)) {
            return;
        }

        var gameRoot = _layout.GameRoot;
        BeginBusy();
        Status = "Removing " + kind + " " + id + "…";
        try {
            await Task.Run(() => ContentRemoval.Delete(gameRoot, kind, id));
            Reload();
            Installed?.Invoke();
            Status = "Removed " + kind + " " + id + ".";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) {
            Status = ex.Message;
        } finally {
            EndBusy();
        }
    }

    private void BeginBusy()
    {
        _busy = 1;
        InstallCommand.NotifyCanExecuteChanged();
        RemoveCarCommand.NotifyCanExecuteChanged();
        RemoveTrackCommand.NotifyCanExecuteChanged();
        CheckRepairsCommand.NotifyCanExecuteChanged();
        ApplyRepairCommand.NotifyCanExecuteChanged();
    }

    private void EndBusy()
    {
        _busy = 0;
        InstallCommand.NotifyCanExecuteChanged();
        RemoveCarCommand.NotifyCanExecuteChanged();
        RemoveTrackCommand.NotifyCanExecuteChanged();
        CheckRepairsCommand.NotifyCanExecuteChanged();
        ApplyRepairCommand.NotifyCanExecuteChanged();
    }

    private void SetPending(string kind, string id)
    {
        _pendingKind = kind;
        _pendingId = id;
        RemoveCarText = kind == "car" ? "Remove " + id : "Remove car";
        RemoveTrackText = kind == "track" ? "Remove " + id : "Remove track";
    }

    private void ClearPending()
    {
        _pendingKind = null;
        _pendingId = null;
        RemoveCarText = "Remove car";
        RemoveTrackText = "Remove track";
    }

    partial void OnSelectedCarChanged(RemovablePiece? value)
    {
        if (_pendingKind == "car" && !string.Equals(value?.Id, _pendingId, StringComparison.Ordinal)) {
            ClearPending();
        }

        RemoveCarCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTrackChanged(RemovablePiece? value)
    {
        if (_pendingKind == "track" && !string.Equals(value?.Id, _pendingId, StringComparison.Ordinal)) {
            ClearPending();
        }

        RemoveTrackCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRepairChanged(ContentRepair? value) => ApplyRepairCommand.NotifyCanExecuteChanged();

    partial void OnRepairsChanged(IReadOnlyList<ContentRepair> value) => OnPropertyChanged(nameof(ShowRepairList));

    partial void OnCarsChanged(IReadOnlyList<RemovablePiece> value) => OnPropertyChanged(nameof(ShowNoCars));

    partial void OnTracksChanged(IReadOnlyList<RemovablePiece> value) => OnPropertyChanged(nameof(ShowNoTracks));

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
