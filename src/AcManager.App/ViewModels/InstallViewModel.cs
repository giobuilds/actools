using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class InstallViewModel : ObservableObject
{
    private readonly IInstallSettingsStore _store;
    private readonly IInstallLocator _locator;
    private readonly Action<AcInstallLayout> _onChanged;

    public InstallViewModel(IInstallSettingsStore store, IInstallLocator locator, Action<AcInstallLayout> onChanged)
    {
        _store = store;
        _locator = locator;
        _onChanged = onChanged;
        SearchedRoots = string.Join(Environment.NewLine, locator.DefaultSteamRoots);
    }

    public string SearchedRoots { get; }

    [ObservableProperty]
    private string _gameRoot = "";

    [ObservableProperty]
    private string _documentsDirectory = "";

    [ObservableProperty]
    private string _protonPrefix = "";

    [ObservableProperty]
    private string _source = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private bool _gameRootValid;

    [ObservableProperty]
    private bool _acsPresent;

    [ObservableProperty]
    private bool _carsPresent;

    [ObservableProperty]
    private bool _tracksPresent;

    public void Publish()
    {
        var layout = _store.Resolve(_locator);
        Apply(layout);
        _onChanged(layout);
        Status = layout.HasGameRoot
            ? "Using the " + layout.Source.ToLowerInvariant() + " install."
            : "No Assetto Corsa install found. Set the game folder, or install the game in Steam and choose Detect.";
    }

    [RelayCommand]
    private void Detect()
    {
        var layout = _locator.Locate();
        GameRoot = layout.GameRoot ?? "";
        DocumentsDirectory = layout.DocumentsDirectory ?? "";
        ProtonPrefix = layout.ProtonPrefix ?? "";
        Source = layout.Source;
        RefreshChecks();
        Status = layout.HasGameRoot
            ? "Detected a Steam install. Save to keep these paths."
            : "Steam libraries were searched and no assettocorsa folder with acs.exe was found.";
    }

    [RelayCommand]
    private void Save()
    {
        _store.Save(new InstallSettings {
            GameRoot = BlankToNull(GameRoot),
            DocumentsDirectory = BlankToNull(DocumentsDirectory),
            ProtonPrefix = BlankToNull(ProtonPrefix),
        });
        var layout = _store.Resolve(_locator);
        Apply(layout);
        _onChanged(layout);
        Status = "Saved to " + XdgConfigHint() + ".";
    }

    private void Apply(AcInstallLayout layout)
    {
        GameRoot = layout.GameRoot ?? "";
        DocumentsDirectory = layout.DocumentsDirectory ?? "";
        ProtonPrefix = layout.ProtonPrefix ?? "";
        Source = layout.Source;
        RefreshChecks();
    }

    partial void OnGameRootChanged(string value) => RefreshChecks();

    partial void OnDocumentsDirectoryChanged(string value) => RefreshChecks();

    partial void OnProtonPrefixChanged(string value) => RefreshChecks();

    private void RefreshChecks()
    {
        var root = GameRoot.Trim();
        GameRootValid = root.Length > 0 && AcInstallPaths.IsGameRoot(root);
        AcsPresent = root.Length > 0 && (File.Exists(Path.Combine(root, "acs.exe")) || File.Exists(Path.Combine(root, "acs_pro.exe")));
        CarsPresent = root.Length > 0 && Directory.Exists(Path.Combine(root, "content", "cars"));
        TracksPresent = root.Length > 0 && Directory.Exists(Path.Combine(root, "content", "tracks"));
    }

    private static string? BlankToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string XdgConfigHint() =>
        Path.Combine(XdgAppPathsFallback(), "install.json");

    private static string XdgAppPathsFallback()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(xdg)) {
            xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(xdg, "actools");
    }
}
