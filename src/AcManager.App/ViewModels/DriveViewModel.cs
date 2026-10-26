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
    private decimal _raceLaps = 5;

    [ObservableProperty]
    private decimal _opponents = 5;

    [ObservableProperty]
    private decimal _aiLevel = 90;

    [ObservableProperty]
    private bool _showMinutes = true;

    [ObservableProperty]
    private bool _showSpawn = true;

    [ObservableProperty]
    private bool _showLaps;

    [ObservableProperty]
    private bool _showOpponents;

    [ObservableProperty]
    private bool _showAi;

    [ObservableProperty]
    private bool _showMatches;

    [ObservableProperty]
    private bool _showWeekend;

    [ObservableProperty]
    private decimal _dragMatches = 10;

    [ObservableProperty]
    private decimal _practiceMinutes = 15;

    [ObservableProperty]
    private decimal _qualificationMinutes = 30;

    [ObservableProperty]
    private string? _previewPath;

    [ObservableProperty]
    private string _status = "Choose a car and a track.";

    [ObservableProperty]
    private bool _libraryReady;

    public IReadOnlyList<string> SpawnSets { get; } = ["PIT", "START"];

    public IReadOnlyList<DriveModeOption> Modes { get; } = [
        new(QuickDriveMode.Practice, "Practice"),
        new(QuickDriveMode.Hotlap, "Hotlap"),
        new(QuickDriveMode.Race, "Race"),
        new(QuickDriveMode.TrackDay, "Track Day"),
        new(QuickDriveMode.TimeAttack, "Time Attack"),
        new(QuickDriveMode.Drift, "Drift"),
        new(QuickDriveMode.Drag, "Drag"),
        new(QuickDriveMode.Weekend, "Weekend"),
    ];

    [ObservableProperty]
    private DriveModeOption _selectedMode;

    public DriveViewModel(IGameLauncher launcher)
    {
        _launcher = launcher;
        _selectedMode = Modes[0];
    }

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

    public void Rescan()
    {
        var trackId = SelectedTrack?.Id;
        var layoutId = SelectedLayout?.Id;
        var weatherId = SelectedWeather?.Id;
        _cars = ContentLibrary.ScanCars(_layout.CarsDirectory);
        Tracks = ContentLibrary.ScanTracks(_layout.TracksDirectory);
        Weather = ContentLibrary.ScanWeather(_layout.WeatherDirectory);

        TrackSummary? track = trackId == null
            ? Tracks.FirstOrDefault()
            : Tracks.FirstOrDefault(item => item.Id == trackId) ?? Tracks.FirstOrDefault();
        var keptTrack = trackId != null && track?.Id == trackId;
        SelectedTrack = track;
        if (keptTrack && SelectedTrack != null) {
            SelectedLayout = SelectedTrack.Layouts.FirstOrDefault(item => item.Id == layoutId)
                ?? SelectedTrack.Layouts.FirstOrDefault();
        }

        SelectedWeather = weatherId == null
            ? Weather.FirstOrDefault(item => item.Id == "3_clear") ?? Weather.FirstOrDefault()
            : Weather.FirstOrDefault(item => item.Id == weatherId)
                ?? Weather.FirstOrDefault(item => item.Id == "3_clear")
                ?? Weather.FirstOrDefault();
        ApplyFilter();
        DriveCommand.NotifyCanExecuteChanged();
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

    partial void OnSelectedModeChanged(DriveModeOption value)
    {
        var mode = value.Mode;
        ShowMinutes = mode is QuickDriveMode.Practice or QuickDriveMode.TimeAttack or QuickDriveMode.Drift;
        ShowSpawn = mode is QuickDriveMode.Practice or QuickDriveMode.Drift;
        ShowLaps = mode is QuickDriveMode.Race or QuickDriveMode.Weekend;
        ShowOpponents = mode is QuickDriveMode.Race or QuickDriveMode.TrackDay or QuickDriveMode.Weekend;
        ShowAi = ShowOpponents || mode == QuickDriveMode.Drag;
        ShowMatches = mode == QuickDriveMode.Drag;
        ShowWeekend = mode == QuickDriveMode.Weekend;
    }

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

    [RelayCommand]
    private void OpenLaunchLog()
    {
        var message = LaunchFeedback.TryOpen(_launcher);
        if (message != null) {
            Status = message;
        }
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
                Mode = SelectedMode.Mode,
                CarId = SelectedCar.Id,
                SkinId = SelectedSkin?.Id ?? "",
                TrackId = SelectedTrack.Id,
                TrackLayoutId = SelectedLayout?.Id ?? "",
                WeatherId = SelectedWeather?.Id ?? "3_clear",
                DurationMinutes = (double)DurationMinutes,
                SpawnSet = SpawnSet,
                RaceLaps = (int)RaceLaps,
                Opponents = (int)Opponents,
                AiLevel = (int)AiLevel,
                DragMatches = (int)DragMatches,
                PracticeMinutes = (int)PracticeMinutes,
                QualificationMinutes = (int)QualificationMinutes,
            });
            var launch = _launcher.LaunchSession(_layout);
            Status = LaunchFeedback.AfterWrite(
                _launcher,
                launch,
                path,
                "Starting the " + SessionLabel(SelectedMode.Mode) + " session.");
            if (launch == GameLaunchStatus.SessionStarted) {
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
                var sawGame = false;
                for (var i = 0; i < 60 && !token.IsCancellationRequested && !sawGame; i++) {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    if (_launcher.IsGameRunning()) {
                        sawGame = true;
                        Post(token, "Assetto Corsa is running.");
                    }
                }

                while (sawGame && !token.IsCancellationRequested) {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    if (!_launcher.IsGameRunning()) {
                        Post(token, "Assetto Corsa exited.");
                        return;
                    }
                }

                if (!sawGame) {
                    Post(token, LaunchFeedback.GaveUp(_launcher));
                }
            } catch (OperationCanceledException) {
                // A newer Drive replaced this watch.
            }
        }, token);
    }

    private void Post(CancellationToken token, string status)
    {
        if (token.IsCancellationRequested) {
            return;
        }

        Dispatcher.UIThread.Post(() => {
            if (!token.IsCancellationRequested) {
                Status = status;
            }
        });
    }

    private static string SessionLabel(QuickDriveMode mode) => mode switch {
        QuickDriveMode.Hotlap => "hotlap",
        QuickDriveMode.Race => "race",
        QuickDriveMode.TrackDay => "track day",
        QuickDriveMode.TimeAttack => "time attack",
        QuickDriveMode.Drift => "drift",
        QuickDriveMode.Drag => "drag",
        QuickDriveMode.Weekend => "weekend",
        _ => "practice",
    };
}

public sealed record DriveModeOption(QuickDriveMode Mode, string Name);
