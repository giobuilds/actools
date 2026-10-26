using AcTools.Platform;

namespace AcTools.Platform.Linux;

/// <summary>
/// How to start acs.exe with the Proton build this prefix already uses.
/// steam -applaunch starts AssettoCorsa.exe, which is the official main menu and does not load race.ini.
/// </summary>
public sealed record ProtonSessionCommand(
    string ProtonScript,
    string GameExecutable,
    string WorkingDirectory,
    string CompatDataPath,
    string SteamClientPath,
    string? LibraryPath);

public static class ProtonSession
{
    public const string LaunchVerb = "waitforexitandrun";

    public static ProtonSessionCommand? Resolve(AcInstallLayout layout, IEnumerable<string> steamRoots)
    {
        if (string.IsNullOrWhiteSpace(layout.GameRoot)) {
            return null;
        }

        var executable = FindGameExecutable(layout.GameRoot);
        var compatData = CompatDataDirectory(layout);
        if (executable == null || compatData == null) {
            return null;
        }

        var roots = Roots(steamRoots);
        var script = FindProtonScript(compatData, roots);
        if (script == null) {
            return null;
        }

        var client = FindSteamClient(compatData, roots, script);
        if (client == null) {
            return null;
        }

        return new ProtonSessionCommand(
            script,
            executable,
            layout.GameRoot,
            compatData,
            client,
            LibraryRoot(layout.GameRoot));
    }

    /// <summary>
    /// Why <see cref="Resolve"/> returned null. The sentence names the missing piece.
    /// </summary>
    public static string Explain(AcInstallLayout layout, IEnumerable<string> steamRoots)
    {
        if (string.IsNullOrWhiteSpace(layout.GameRoot) || FindGameExecutable(layout.GameRoot) == null) {
            return "acs.exe is missing from the game folder, so the session was not started.";
        }

        var compatData = CompatDataDirectory(layout);
        if (compatData == null) {
            return "The game has no Proton prefix yet. Launch it once from Steam so Proton can create it.";
        }

        if (FindProtonScript(compatData, Roots(steamRoots)) == null) {
            return "Proton build not found. Looked in the prefix version file, config_info, and Steam config.vdf.";
        }

        return "Could not find the Steam client that owns this prefix.";
    }

    private static List<string> Roots(IEnumerable<string> steamRoots) =>
        steamRoots.Where(static root => !string.IsNullOrWhiteSpace(root)).ToList();

    public static string? FindGameExecutable(string gameRoot)
    {
        var acs = Path.Combine(gameRoot, "acs.exe");
        if (File.Exists(acs)) {
            return acs;
        }

        var pro = Path.Combine(gameRoot, "acs_pro.exe");
        return File.Exists(pro) ? pro : null;
    }

    /// <summary>
    /// Proton's STEAM_COMPAT_DATA_PATH is the compatdata/244210 directory. The pfx folder is one level down.
    /// </summary>
    public static string? CompatDataDirectory(AcInstallLayout layout)
    {
        if (!string.IsNullOrWhiteSpace(layout.ProtonPrefix)) {
            var prefix = layout.ProtonPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(prefix), "pfx", StringComparison.OrdinalIgnoreCase)) {
                return Path.GetDirectoryName(prefix);
            }

            if (File.Exists(Path.Combine(prefix, "version"))
                || File.Exists(Path.Combine(prefix, "config_info"))
                || Directory.Exists(Path.Combine(prefix, "pfx"))) {
                return prefix;
            }
        }

        if (string.IsNullOrWhiteSpace(layout.GameRoot)) {
            return null;
        }

        var inferred = AcInstallPaths.InferPrefix(layout.GameRoot);
        return inferred == null ? null : Path.GetDirectoryName(inferred);
    }

    private static string? FindProtonScript(string compatData, IReadOnlyList<string> steamRoots)
    {
        foreach (var name in ToolNames(compatData, steamRoots)) {
            var script = FindTool(name, steamRoots);
            if (script != null) {
                return script;
            }
        }

        return ProtonScriptFromConfigInfo(compatData);
    }

    private static IEnumerable<string> ToolNames(string compatData, IReadOnlyList<string> steamRoots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ReadVersionNames(compatData)
            .Concat(ReadConfigInfoNames(compatData))
            .Concat(ReadMappedNames(steamRoots))) {
            if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\')) {
                continue;
            }

            if (seen.Add(name)) {
                yield return name;
            }
        }
    }

    private static IEnumerable<string> ReadVersionNames(string compatData)
    {
        foreach (var line in ReadLines(Path.Combine(compatData, "version"))) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.All(char.IsDigit)) {
                continue;
            }

            yield return trimmed;
            var tokens = trimmed.Split(' ', '\t', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 1 && !tokens[^1].All(char.IsDigit)) {
                yield return tokens[^1];
            }

            yield break;
        }
    }

    private static IEnumerable<string> ReadConfigInfoNames(string compatData)
    {
        foreach (var line in ReadLines(Path.Combine(compatData, "config_info"))) {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.All(char.IsDigit) || trimmed.Contains('/') || trimmed.Contains('\\')) {
                continue;
            }

            yield return trimmed;
            yield break;
        }
    }

    private static IEnumerable<string> ReadMappedNames(IReadOnlyList<string> steamRoots)
    {
        var appId = AcInstallLayout.AssettoCorsaAppId.ToString();
        foreach (var root in steamRoots) {
            var vdf = Path.Combine(root, "config", "config.vdf");
            if (!File.Exists(vdf)) {
                continue;
            }

            string text;
            try {
                text = File.ReadAllText(vdf);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                continue;
            }

            var name = SteamConfigVdf.ReadCompatToolName(text, appId);
            if (!string.IsNullOrWhiteSpace(name)) {
                yield return name;
            }
        }
    }

    private static string? FindTool(string tool, IReadOnlyList<string> steamRoots)
    {
        foreach (var root in LibraryRoots(steamRoots)) {
            var custom = Path.Combine(root, "compatibilitytools.d", tool, "proton");
            if (File.Exists(custom)) {
                return custom;
            }

            var common = Path.Combine(root, "steamapps", "common", tool, "proton");
            if (File.Exists(common)) {
                return common;
            }

            var valve = MatchValveProton(Path.Combine(root, "steamapps", "common"), tool);
            if (valve != null) {
                return valve;
            }
        }

        return null;
    }

    private static string? MatchValveProton(string commonDirectory, string tool)
    {
        if (!IsValveToolId(tool) || !Directory.Exists(commonDirectory)) {
            return null;
        }

        string[] directories;
        try {
            directories = Directory.GetDirectories(commonDirectory);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }

        foreach (var directory in directories) {
            if (!DirectoryMatchesTool(Path.GetFileName(directory), tool)) {
                continue;
            }

            var script = Path.Combine(directory, "proton");
            if (File.Exists(script)) {
                return script;
            }
        }

        return null;
    }

    private static bool IsValveToolId(string tool) =>
        tool.StartsWith("proton_", StringComparison.OrdinalIgnoreCase)
        || tool.StartsWith("proton-", StringComparison.OrdinalIgnoreCase);

    private static bool DirectoryMatchesTool(string directoryName, string tool)
    {
        if (tool.Contains("experimental", StringComparison.OrdinalIgnoreCase)) {
            return directoryName.Contains("Proton", StringComparison.OrdinalIgnoreCase)
                && directoryName.Contains("Experimental", StringComparison.OrdinalIgnoreCase);
        }

        if (tool.Contains("hotfix", StringComparison.OrdinalIgnoreCase)) {
            return directoryName.Contains("Proton", StringComparison.OrdinalIgnoreCase)
                && directoryName.Contains("Hotfix", StringComparison.OrdinalIgnoreCase);
        }

        var version = VersionToken(tool);
        if (version == null || !directoryName.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return ContainsVersion(directoryName, version);
    }

    private static string? VersionToken(string tool)
    {
        var start = 0;
        while (start < tool.Length && !char.IsDigit(tool[start])) {
            start++;
        }

        if (start >= tool.Length) {
            return null;
        }

        var end = start;
        while (end < tool.Length && (char.IsDigit(tool[end]) || tool[end] == '.')) {
            end++;
        }

        return tool[start..end];
    }

    private static bool ContainsVersion(string directoryName, string version)
    {
        var index = 0;
        while ((index = directoryName.IndexOf(version, index, StringComparison.OrdinalIgnoreCase)) >= 0) {
            var before = index == 0 || !char.IsDigit(directoryName[index - 1]);
            var after = index + version.Length >= directoryName.Length || !char.IsDigit(directoryName[index + version.Length]);
            if (before && after) {
                return true;
            }

            index += version.Length;
        }

        return false;
    }

    private static string? ProtonScriptFromConfigInfo(string compatData)
    {
        foreach (var line in ReadLines(Path.Combine(compatData, "config_info"))) {
            var trimmed = line.Trim();
            if (!Path.IsPathRooted(trimmed) || (!Directory.Exists(trimmed) && !File.Exists(trimmed))) {
                continue;
            }

            var script = ProtonScriptNear(trimmed);
            if (script != null) {
                return script;
            }
        }

        return null;
    }

    private static string? ProtonScriptNear(string path)
    {
        var current = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(current); i++) {
            var candidate = Path.Combine(current, "proton");
            if (File.Exists(candidate)) {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static string? FindSteamClient(string compatData, IReadOnlyList<string> steamRoots, string protonScript)
    {
        foreach (var line in ReadLines(Path.Combine(compatData, "config_info"))) {
            var trimmed = line.Trim();
            if (Path.IsPathRooted(trimmed) && IsSteamClientRoot(trimmed)) {
                return trimmed;
            }
        }

        var above = ClientAbove(protonScript);
        if (above != null) {
            return above;
        }

        foreach (var root in steamRoots) {
            if (IsSteamClientRoot(root)) {
                return root;
            }
        }

        return null;
    }

    private static string? ClientAbove(string protonScript)
    {
        var current = Path.GetDirectoryName(protonScript);
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(current); i++) {
            if (IsSteamClientRoot(current)) {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static bool IsSteamClientRoot(string path) =>
        Directory.Exists(Path.Combine(path, "ubuntu12_32"))
        || File.Exists(Path.Combine(path, "steam.sh"));

    private static string? LibraryRoot(string gameRoot)
    {
        var common = Path.GetDirectoryName(gameRoot);
        var steamapps = common == null ? null : Path.GetDirectoryName(common);
        if (steamapps == null || !string.Equals(Path.GetFileName(steamapps), "steamapps", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return Path.GetDirectoryName(steamapps);
    }

    private static IEnumerable<string> LibraryRoots(IReadOnlyList<string> steamRoots)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in steamRoots) {
            if (!Directory.Exists(root)) {
                continue;
            }

            foreach (var library in SteamAcDiscovery.LibraryPaths(root)) {
                if (seen.Add(library)) {
                    yield return library;
                }
            }
        }
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        foreach (var line in ReadAllLinesOrEmpty(path)) {
            yield return line;
        }
    }

    private static string[] ReadAllLinesOrEmpty(string path)
    {
        if (!File.Exists(path)) {
            return [];
        }

        try {
            return File.ReadAllLines(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return [];
        }
    }
}
