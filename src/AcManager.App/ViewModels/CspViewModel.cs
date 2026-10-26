using AcManager.Core.Settings;
using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public sealed record FfbMode(string Id, string Name);

public sealed record GrassQuality(int Value, string Name);

public partial class CspViewModel : ObservableObject
{
    public const string GeneralFile = "general.ini";
    public const string WeatherFile = "weather_fx.ini";

    public static IReadOnlyList<FfbMode> Modes { get; } = [
        new(FfbPostProcess.GammaType, "Gamma"),
        new(FfbPostProcess.LutType, "LUT"),
    ];

    public IReadOnlyList<FfbMode> FfbModes => Modes;

    public static IReadOnlyList<GrassQuality> GrassLevels { get; } = [
        new(0, "Very low"),
        new(1, "Low"),
        new(2, "Medium"),
        new(3, "High"),
        new(4, "Very high"),
    ];

    public IReadOnlyList<GrassQuality> GrassQualities => GrassLevels;

    private string? _documents;
    private string? _gameRoot;

    [ObservableProperty]
    private bool _installed;

    [ObservableProperty]
    private string _versionLabel = PatchInfo.Missing.Label;

    [ObservableProperty]
    private bool _patchEnabled = true;

    [ObservableProperty]
    private bool _weatherFxEnabled = true;

    [ObservableProperty]
    private bool _adjustments = true;

    [ObservableProperty]
    private decimal _pixelDensity = 100;

    [ObservableProperty]
    private bool _grass = true;

    [ObservableProperty]
    private GrassQuality _grassQuality = GrassLevels[2];

    [ObservableProperty]
    private bool _extraFx;

    [ObservableProperty]
    private bool _ffbEnabled;

    [ObservableProperty]
    private FfbMode _ffbMode = Modes[0];

    [ObservableProperty]
    private decimal _gamma = 0.5m;

    [ObservableProperty]
    private IReadOnlyList<string> _curves = [];

    [ObservableProperty]
    private string? _curve;

    [ObservableProperty]
    private bool _hasCurves;

    [ObservableProperty]
    private string _status = "Set the Documents directory on the Install page. These files live in that folder.";

    [ObservableProperty]
    private bool _canSave;

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
            Installed = false;
            VersionLabel = PatchInfo.Missing.Label;
            Status = "Set the Documents directory on the Install page. These files live in that folder.";
            return;
        }

        try {
            var patch = PatchInfoFile.Read(_gameRoot);
            Installed = patch.Installed;
            VersionLabel = patch.Label;
            PatchEnabled = ExtensionFlagFile.ReadEnabled(_gameRoot, _documents, GeneralFile);
            WeatherFxEnabled = ExtensionFlagFile.ReadEnabled(_gameRoot, _documents, WeatherFile);
            var graphics = GraphicsSettingsFile.Read(_gameRoot, _documents);
            Adjustments = graphics.Adjustments;
            PixelDensity = graphics.PixelDensity;
            Grass = graphics.Grass;
            GrassQuality = Level(graphics.GrassQuality);
            ExtraFx = graphics.ExtraFx;

            var ffb = FfbPostProcessFile.Read(_documents);
            FfbEnabled = ffb.Enabled;
            FfbMode = Mode(ffb.Type);
            Gamma = (decimal)ffb.Gamma;
            var curves = FfbPostProcessFile.Curves(_documents).ToList();
            if (ffb.Curve != null && !curves.Contains(ffb.Curve, StringComparer.OrdinalIgnoreCase)) {
                curves.Insert(0, ffb.Curve);
            }

            Curves = curves;
            HasCurves = Curves.Count > 0;
            Curve = ffb.Curve;
            CanSave = true;
            Status = Installed
                ? "Custom Shaders Patch " + patch.Label + ". Using " + Path.Combine(_documents, "cfg")
                : "Custom Shaders Patch is not installed in the game folder. Force-feedback post-processing still saves in " + Path.Combine(_documents, "cfg") + ".";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            CanSave = false;
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_documents)) {
            Status = "Set the Documents directory on the Install page. These files live in that folder.";
            return;
        }

        try {
            if (Installed) {
                ExtensionFlagFile.WriteEnabled(_documents, GeneralFile, PatchEnabled);
                ExtensionFlagFile.WriteEnabled(_documents, WeatherFile, WeatherFxEnabled);
                GraphicsSettingsFile.Write(_documents, new GraphicsSettings(
                    Adjustments,
                    (int)PixelDensity,
                    Grass,
                    GrassQuality.Value,
                    ExtraFx));
            }

            FfbPostProcessFile.Write(_documents, new FfbPostProcess(
                FfbEnabled,
                FfbMode.Id,
                (double)Gamma,
                Curve));
            Status = Installed
                ? "Saved Custom Shaders Patch options, graphics, and force-feedback post-processing. The next session uses them."
                : "Saved force-feedback post-processing. The next session uses it.";
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Status = ex.Message;
        }
    }

    private static FfbMode Mode(string id) =>
        Modes.First(mode => mode.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private static GrassQuality Level(int value) =>
        GrassLevels.First(level => level.Value == Math.Clamp(value, 0, 4));
}
