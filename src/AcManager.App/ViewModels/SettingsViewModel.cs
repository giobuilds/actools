using AcManager.Core.Settings;
using AcTools.Platform;
using AcTools.Platform.Linux;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public sealed record AssistLevel(int Value, string Name);

public partial class PythonAppItem : ObservableObject
{
    public PythonAppItem(string section, string name, bool active)
    {
        Section = section;
        Name = name;
        Active = active;
    }

    public string Section { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _active;
}

public partial class SettingsViewModel : ObservableObject
{
    public static IReadOnlyList<AssistLevel> Levels { get; } = [
        new(0, "Off"),
        new(1, "Factory"),
        new(2, "On"),
    ];

    public IReadOnlyList<AssistLevel> AssistLevels => Levels;

    private readonly IInputDevices _devices;
    private string? _documents;
    private string? _gameRoot;

    public SettingsViewModel(IInputDevices? devices = null)
    {
        _devices = devices ?? new SdlInputDevices();
    }

    [ObservableProperty]
    private string _inputMethod = "Not set";

    [ObservableProperty]
    private IReadOnlyList<string> _controllers = [];

    [ObservableProperty]
    private bool _hasControllers;

    [ObservableProperty]
    private IReadOnlyList<string> _connected = [];

    [ObservableProperty]
    private decimal _forceFeedback = 100;

    [ObservableProperty]
    private decimal _steerLock = 900;

    [ObservableProperty]
    private decimal _master = 100;

    [ObservableProperty]
    private decimal _engine = 100;

    [ObservableProperty]
    private decimal _tyres = 80;

    [ObservableProperty]
    private decimal _wind = 90;

    [ObservableProperty]
    private decimal _opponents = 90;

    [ObservableProperty]
    private decimal _music = 50;

    [ObservableProperty]
    private decimal _uiSounds = 70;

    [ObservableProperty]
    private decimal _surfaces = 100;

    [ObservableProperty]
    private decimal _dirt = 100;

    [ObservableProperty]
    private bool _idealLine;

    [ObservableProperty]
    private bool _autoBlip;

    [ObservableProperty]
    private decimal _stability;

    [ObservableProperty]
    private bool _autoBrake;

    [ObservableProperty]
    private bool _autoShifter;

    [ObservableProperty]
    private AssistLevel _abs = Levels[1];

    [ObservableProperty]
    private AssistLevel _traction = Levels[1];

    [ObservableProperty]
    private bool _autoClutch;

    [ObservableProperty]
    private bool _visualDamage = true;

    [ObservableProperty]
    private decimal _damage = 100;

    [ObservableProperty]
    private decimal _fuelRate = 1;

    [ObservableProperty]
    private decimal _tyreWear = 1;

    [ObservableProperty]
    private bool _tyreBlankets;

    [ObservableProperty]
    private decimal _slipstream = 1;

    [ObservableProperty]
    private IReadOnlyList<PythonAppItem> _apps = [];

    [ObservableProperty]
    private string _status = "Set the Documents directory on the Install page. These files live in that folder.";

    [ObservableProperty]
    private bool _canSave;

    public bool HasApps => Apps.Count > 0;

    partial void OnAppsChanged(IReadOnlyList<PythonAppItem> value) => OnPropertyChanged(nameof(HasApps));

    public void UseLayout(AcInstallLayout layout)
    {
        _documents = layout.DocumentsDirectory;
        _gameRoot = layout.GameRoot;
        Reload();
    }

    [RelayCommand]
    public void Reload()
    {
        if (string.IsNullOrWhiteSpace(_documents)) {
            CanSave = false;
            RefreshConnected([]);
            Status = "Set the Documents directory on the Install page. These files live in that folder.";
            return;
        }

        try {
            var controls = ControlSettingsFile.Read(_documents);
            InputMethod = controls.InputMethod.Length == 0 ? "Not set" : controls.InputMethod;
            Controllers = controls.Controllers.Select(item => item.Label).ToArray();
            HasControllers = Controllers.Count > 0;
            RefreshConnected(controls.Controllers.Select(item => item.Name).ToArray());
            ForceFeedback = controls.ForceFeedback;
            SteerLock = controls.SteerLock;

            var audio = AudioSettingsFile.Read(_documents);
            Master = audio.Master;
            Engine = audio.Engine;
            Tyres = audio.Tyres;
            Wind = audio.Wind;
            Opponents = audio.Opponents;
            Music = audio.Music;
            UiSounds = audio.Ui;
            Surfaces = audio.Surfaces;
            Dirt = audio.Dirt;

            var assists = AssistSettingsFile.Read(_documents);
            IdealLine = assists.IdealLine;
            AutoBlip = assists.AutoBlip;
            Stability = assists.StabilityControl;
            AutoBrake = assists.AutoBrake;
            AutoShifter = assists.AutoShifter;
            Abs = Level(assists.Abs);
            Traction = Level(assists.TractionControl);
            AutoClutch = assists.AutoClutch;
            VisualDamage = assists.VisualDamage;
            Damage = (decimal)assists.Damage;
            FuelRate = (decimal)assists.FuelRate;
            TyreWear = (decimal)assists.TyreWear;
            TyreBlankets = assists.TyreBlankets;
            Slipstream = (decimal)assists.Slipstream;

            Apps = PythonAppsFile.Read(_documents, _gameRoot)
                .Select(app => new PythonAppItem(app.Section, app.Name, app.Active))
                .ToArray();
            CanSave = true;
            Status = "Using " + Path.Combine(_documents, "cfg");
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            CanSave = false;
            RefreshConnected([]);
            Status = ex.Message;
        }
    }

    private void RefreshConnected(IReadOnlyList<string> savedNames)
    {
        Connected = ConnectedControllers.Lines(savedNames, _devices.List());
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_documents)) {
            Status = "Set the Documents directory on the Install page. These files live in that folder.";
            return;
        }

        try {
            AssistSettingsFile.Write(_documents, new AssistSettings(
                IdealLine,
                AutoBlip,
                (int)Stability,
                AutoBrake,
                AutoShifter,
                Abs.Value,
                Traction.Value,
                AutoClutch,
                VisualDamage,
                (double)Damage,
                (double)FuelRate,
                (double)TyreWear,
                TyreBlankets,
                (double)Slipstream));
            AudioSettingsFile.Write(_documents, new AudioSettings(
                (int)Master,
                (int)Engine,
                (int)Tyres,
                (int)Wind,
                (int)Opponents,
                (int)Music,
                (int)UiSounds,
                (int)Surfaces,
                (int)Dirt));
            var current = ControlSettingsFile.Read(_documents);
            ControlSettingsFile.Write(_documents, current with {
                ForceFeedback = (int)ForceFeedback,
                SteerLock = (int)SteerLock,
            });
            PythonAppsFile.Write(_documents, Apps.Select(app => new PythonAppSetting(app.Section, app.Name, app.Active)).ToArray());
            Status = "Saved assists, audio, controls, and Python apps. The next session uses them.";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Status = ex.Message;
        }
    }

    private static AssistLevel Level(int value) =>
        Levels.First(level => level.Value == Math.Clamp(value, 0, 2));
}
