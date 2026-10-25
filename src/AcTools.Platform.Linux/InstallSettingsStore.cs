using System.Text.Json;
using AcTools.Platform;

namespace AcTools.Platform.Linux;

public sealed class XdgAppPaths
{
    public XdgAppPaths(string? configDirectory = null)
    {
        ConfigDirectory = configDirectory ?? DefaultConfigDirectory();
    }

    public string ConfigDirectory { get; }

    public string InstallSettingsFile => Path.Combine(ConfigDirectory, "install.json");

    public static string DefaultConfigDirectory()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(xdg)) {
            xdg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(xdg, "actools");
    }
}

public sealed class InstallSettingsStore : IInstallSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
    };

    private readonly XdgAppPaths _paths;

    public InstallSettingsStore(XdgAppPaths paths)
    {
        _paths = paths;
    }

    public InstallSettings Load()
    {
        if (!File.Exists(_paths.InstallSettingsFile)) {
            return new InstallSettings();
        }

        try {
            return JsonSerializer.Deserialize<InstallSettings>(File.ReadAllText(_paths.InstallSettingsFile), JsonOptions)
                ?? new InstallSettings();
        } catch (JsonException) {
            return new InstallSettings();
        }
    }

    public void Save(InstallSettings settings)
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(_paths.InstallSettingsFile, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public AcInstallLayout Resolve(IInstallLocator locator)
    {
        var saved = Load();
        var gameRoot = BlankToNull(saved.GameRoot);
        var documents = BlankToNull(saved.DocumentsDirectory);
        var prefix = BlankToNull(saved.ProtonPrefix);

        if (gameRoot == null && documents == null && prefix == null) {
            return locator.Locate();
        }

        if (prefix == null && gameRoot != null) {
            var inferred = AcInstallPaths.InferPrefix(gameRoot);
            if (inferred != null && Directory.Exists(inferred)) {
                prefix = inferred;
            }
        }

        if (documents == null && prefix != null && Directory.Exists(prefix)) {
            documents = AcInstallPaths.FindDocuments(prefix);
        }

        return new AcInstallLayout(gameRoot, documents, prefix, "Saved settings");
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
