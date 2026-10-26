using AcTools.Platform;

namespace AcTools.Platform.Linux;

/// <summary>
/// Flatpak Steam runs Proton inside its sandbox. Host files under the app directory
/// are rewritten to the path that sandbox sees. data, cache, and config stay on the
/// host path because Flatpak bind-mounts those three directories in place.
/// </summary>
public static class FlatpakSteam
{
    public const string AppId = "com.valvesoftware.Steam";

    public static bool Owns(string? path) => AppDirectory(path) != null;

    public static string? AppDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        var full = Path.GetFullPath(path);
        var marker = Path.DirectorySeparatorChar + ".var"
            + Path.DirectorySeparatorChar + "app"
            + Path.DirectorySeparatorChar + AppId;
        var index = full.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) {
            return null;
        }

        var end = index + marker.Length;
        if (end < full.Length && full[end] != Path.DirectorySeparatorChar) {
            return null;
        }

        return full[..end];
    }

    public static string SandboxPath(string path)
    {
        var full = Path.GetFullPath(path);
        var app = AppDirectory(full);
        if (app == null) {
            return full;
        }

        var relative = Path.GetRelativePath(app, full);
        if (IsHostMounted(relative)) {
            return full;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return relative == "." ? home : Path.Combine(home, relative);
    }

    public static IReadOnlyList<string> Arguments(ProtonSessionCommand command)
    {
        var game = SandboxPath(command.WorkingDirectory);
        var proton = SandboxPath(command.ProtonScript);
        var compat = SandboxPath(command.CompatDataPath);
        var client = SandboxPath(command.SteamClientPath);
        var arguments = new List<string> { "run" };
        foreach (var directory in Filesystems(
            command.WorkingDirectory,
            command.CompatDataPath,
            Path.GetDirectoryName(command.ProtonScript),
            command.LibraryPath)) {
            arguments.Add("--filesystem=" + directory + ":rw");
        }

        arguments.Add("--cwd=" + game);
        arguments.Add("--env=STEAM_COMPAT_DATA_PATH=" + compat);
        arguments.Add("--env=STEAM_COMPAT_CLIENT_INSTALL_PATH=" + client);
        arguments.Add("--env=STEAM_COMPAT_INSTALL_PATH=" + game);
        if (!string.IsNullOrEmpty(command.LibraryPath)) {
            arguments.Add("--env=STEAM_COMPAT_LIBRARY_PATHS=" + SandboxPath(command.LibraryPath));
        }

        var appId = AcInstallLayout.AssettoCorsaAppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        arguments.Add("--env=STEAM_COMPAT_APP_ID=" + appId);
        arguments.Add("--env=SteamAppId=" + appId);
        arguments.Add("--env=SteamGameId=" + appId);
        arguments.Add("--env=SteamOverlayGameId=" + appId);
        arguments.Add("--command=" + proton);
        arguments.Add(AppId);
        arguments.Add(ProtonSession.LaunchVerb);
        arguments.Add(SandboxPath(command.GameExecutable));
        return arguments;
    }

    private static bool IsHostMounted(string relative) =>
        StartsWithDirectory(relative, "data")
        || StartsWithDirectory(relative, "cache")
        || StartsWithDirectory(relative, "config");

    private static bool StartsWithDirectory(string relative, string name) =>
        relative.Equals(name, StringComparison.Ordinal)
        || relative.StartsWith(name + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static IReadOnlyList<string> Filesystems(params string?[] directories)
    {
        var grants = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories) {
            if (string.IsNullOrWhiteSpace(directory) || Owns(directory)) {
                continue;
            }

            grants.Add(Path.GetFullPath(directory));
        }

        return grants.ToArray();
    }
}
