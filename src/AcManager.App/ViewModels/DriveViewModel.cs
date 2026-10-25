using System.Collections.ObjectModel;
using AcManager.Core.Content;
using AcManager.Core.Sessions;
using AcTools.Core.Sessions;
using AcTools.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class DriveViewModel : ObservableObject
{
    private readonly IGameLauncher _launcher;
    private AcInstallLayout _layout = new(null, null, null, "");
    private IReadOnlyList<CarSummary> _cars = [];
    private CancellationTokenSource? _watch;

    public DriveViewModel(IGameLauncher launcher)
    {
        _launcher = launcher;
    }

    public ObservableCollection<CarSummary> FilteredCars { get; } = [];

    [ObservableProperty]
    private string _filter = "";

    [ObservableProperty]
    private CarSummary? _selectedCar;

    [ObservableProperty]
    private CarSkin? _selectedSkin;

    [ObservableProperty]
    private IReadOnlyList<CarSkin> _skins = [];

    [ObservableProperty]
    private IReadOnlyList<TrackSummary> _tracks = [];

    [ObservableProperty]
    private TrackSummary? _selectedTrack;

    [ObservableProperty]
    private IReadOnlyList<TrackLayout> _layouts = [];

    [ObservableProperty]
    private TrackLayout? _selectedLayout;

    [ObservableProperty]
    private IReadOnlyList<WeatherSummary> _weather = [];

    [ObservableProperty]
    private WeatherSummary? _selectedWeather;

    [ObservableProperty]
    private decimal _durationMinutes = 30;

    [ObservableProperty]
    private string _spawnSet = "PIT";

    [ObservableProperty]
    private string? _previewPath;

    [ObservableProperty]
    private string _status = "Choose a car and a track.";

    [ObservableProperty]
    private bool _libraryReady;

    public IReadOnlyList<string> SpawnSets { get; } = ["PIT", "START"];

    public void UseLayout(AcInstallLayout layout)
    {
        _layout = layout;
        LibraryReady = layout.HasGameRoot;
        _cars = ContentLibrary.ScanCars(layout.CarsDirectory);
        Tracks = ContentLibrary.ScanTracks(layout.TracksDirectory);
        Weather = ContentLibrary.ScanWeather(layout.WeatherDirectory);
        SelectedTrack = Tracks.FirstOrDefault();
        SelectedWeather = Weather.FirstOrDefault(item => item.Id == "3_clear") ?? Weather.FirstOrDefault();
        ApplyFilter();
        DriveCommand.NotifyCanExecuteChanged();
        if (!LibraryReady) {
            Status = "Set a valid game folder on the Install page before driving.";
        } else if (string.IsNullOrWhiteSpace(layout.DocumentsDirectory)) {
            Status = "Game folder found. Set the Documents directory on the Install page so race.ini has a place to go.";
        } else if (_cars.Count == 0) {
            Status = "The cars folder is empty.";
        } else {
            Status = _cars.Count + " cars, " + Tracks.Count + " tracks.";
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedCarChanged(CarSummary? value)
    {
        Skins = value?.Skins ?? [];
        SelectedSkin = value?.DefaultSkin;
        DriveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSkinChanged(CarSkin? value) => PreviewPath = value?.PreviewPath;

    partial void OnSelectedTrackChanged(TrackSummary? value)
    {
        Layouts = value?.Layouts ?? [];
        SelectedLayout = Layouts.FirstOrDefault();
        DriveCommand.NotifyCanExecuteChanged();
    }

    partial void OnDurationMinutesChanged(decimal value) => DriveCommand.NotifyCanExecuteChanged();

    private void ApplyFilter()
    {
        var selectedId = SelectedCar?.Id;
        FilteredCars.Clear();
        foreach (var car in _cars) {
            if (Matches(car, Filter)) {
                FilteredCars.Add(car);
            }
        }

        SelectedCar = FilteredCars.FirstOrDefault(car => car.Id == selectedId) ?? FilteredCars.FirstOrDefault();
    }

    private static bool Matches(CarSummary car, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) {
            return true;
        }

        return car.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || car.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (car.Brand?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || (car.ClassName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private bool CanDrive() =>
        LibraryReady
        && SelectedCar != null
        && SelectedTrack != null
        && !string.IsNullOrWhiteSpace(_layout.DocumentsDirectory);

    [RelayCommand(CanExecute = nameof(CanDrive))]
    private void Drive()
    {
        if (SelectedCar == null || SelectedTrack == null) {
            return;
        }

        try {
            var path = PracticeLaunch.WriteRaceIni(_layout, new PracticeSession {
                CarId = SelectedCar.Id,
                SkinId = SelectedSkin?.Id ?? "",
                TrackId = SelectedTrack.Id,
                TrackLayoutId = SelectedLayout?.Id ?? "",
                WeatherId = SelectedWeather?.Id ?? "3_clear",
                DurationMinutes = (double)DurationMinutes,
                SpawnSet = SpawnSet,
            });
            var launch = _launcher.LaunchAssettoCorsa();
            Status = launch == GameLaunchStatus.SteamInvoked
                ? "Wrote " + path + ". Steam is starting Assetto Corsa."
                : "Wrote " + path + ". The steam command is not on PATH, so the game was not started.";
            if (launch == GameLaunchStatus.SteamInvoked) {
                WatchForGame();
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) {
            Status = ex.Message;
        }
    }

    private void WatchForGame()
    {
        _watch?.Cancel();
        _watch = new CancellationTokenSource();
        var token = _watch.Token;
        _ = Task.Run(async () => {
            try {
                for (var i = 0; i < 30 && !token.IsCancellationRequested; i++) {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    if (_launcher.IsGameRunning()) {
                        Dispatcher.UIThread.Post(() => Status = "Assetto Corsa is running.");
                        return;
                    }
                }
            } catch (OperationCanceledException) {
                // A newer Drive replaced this watch.
            }
        }, token);
    }
}
