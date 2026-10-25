namespace AcTools.Platform;

/// <summary>
/// Where a Proton (or manual) Assetto Corsa install keeps its files.
/// Game content lives in the Steam library. Configs the game reads live in the prefix Documents folder.
/// </summary>
public sealed record AcInstallLayout(
    string? GameRoot,
    string? DocumentsDirectory,
    string? ProtonPrefix,
    string Source)
{
    public const int AssettoCorsaAppId = 244210;

    public bool HasGameRoot =>
        !string.IsNullOrWhiteSpace(GameRoot) && AcInstallPaths.IsGameRoot(GameRoot);

    public string? CarsDirectory =>
        string.IsNullOrWhiteSpace(GameRoot) ? null : Path.Combine(GameRoot, "content", "cars");

    public string? TracksDirectory =>
        string.IsNullOrWhiteSpace(GameRoot) ? null : Path.Combine(GameRoot, "content", "tracks");

    public string? WeatherDirectory =>
        string.IsNullOrWhiteSpace(GameRoot) ? null : Path.Combine(GameRoot, "content", "weather");

    public string? RaceIniPath =>
        string.IsNullOrWhiteSpace(DocumentsDirectory)
            ? null
            : Path.Combine(DocumentsDirectory, "cfg", "race.ini");
}

public static class AcInstallPaths
{
    public static bool IsGameRoot(string directory)
    {
        return Directory.Exists(Path.Combine(directory, "content", "cars"))
            && Directory.Exists(Path.Combine(directory, "content", "tracks"))
            && (File.Exists(Path.Combine(directory, "acs.exe"))
                || File.Exists(Path.Combine(directory, "acs_pro.exe")));
    }

    public static string? InferPrefix(string gameRoot)
    {
        var common = Path.GetDirectoryName(gameRoot);
        var steamapps = common == null ? null : Path.GetDirectoryName(common);
        if (steamapps == null
            || !string.Equals(Path.GetFileName(steamapps), "steamapps", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return Path.Combine(steamapps, "compatdata", AcInstallLayout.AssettoCorsaAppId.ToString(), "pfx");
    }

    public static string ExpectedDocuments(string protonPrefix) =>
        Path.Combine(protonPrefix, "drive_c", "users", "steamuser", "Documents", "Assetto Corsa");

    /// <summary>
    /// Returns the Documents/Assetto Corsa directory inside the prefix.
    /// When the folder has not been created yet, returns the path Proton will use for steamuser.
    /// </summary>
    public static string FindDocuments(string protonPrefix)
    {
        var users = Path.Combine(protonPrefix, "drive_c", "users");
        if (!Directory.Exists(users)) {
            return ExpectedDocuments(protonPrefix);
        }

        var preferred = ExpectedDocuments(protonPrefix);
        if (Directory.Exists(preferred)) {
            return preferred;
        }

        foreach (var user in Directory.GetDirectories(users)) {
            var name = Path.GetFileName(user);
            if (name.Equals("Public", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var documents = Path.Combine(user, "Documents", "Assetto Corsa");
            if (Directory.Exists(documents)) {
                return documents;
            }
        }

        return preferred;
    }
}
