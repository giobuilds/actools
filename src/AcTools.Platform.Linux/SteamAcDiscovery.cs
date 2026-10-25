using AcTools.Platform;

namespace AcTools.Platform.Linux;

public sealed class SteamAcDiscovery : IInstallLocator
{
    public IReadOnlyList<string> DefaultSteamRoots { get; } = GetDefaultSteamRoots();

    public AcInstallLayout Locate() => FindInRoots(DefaultSteamRoots);

    public static AcInstallLayout FindInRoots(IEnumerable<string> steamRoots)
    {
        AcInstallLayout? withoutPrefix = null;
        foreach (var root in steamRoots) {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) {
                continue;
            }

            foreach (var library in LibraryPaths(root)) {
                var game = Path.Combine(library, "steamapps", "common", "assettocorsa");
                if (!AcInstallPaths.IsGameRoot(game)) {
                    continue;
                }

                var prefix = Path.Combine(library, "steamapps", "compatdata", AcInstallLayout.AssettoCorsaAppId.ToString(), "pfx");
                if (Directory.Exists(prefix)) {
                    return new AcInstallLayout(game, AcInstallPaths.FindDocuments(prefix), prefix, "Steam");
                }

                withoutPrefix ??= new AcInstallLayout(game, null, null, "Steam");
            }
        }

        return withoutPrefix ?? new AcInstallLayout(null, null, null, "Steam");
    }

    public static IEnumerable<string> LibraryPaths(string steamRoot)
    {
        yield return steamRoot;

        foreach (var vdf in new[] {
            Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
            Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        }) {
            if (!File.Exists(vdf)) {
                continue;
            }

            foreach (var path in SteamLibraryFolders.ReadPaths(File.ReadAllText(vdf))) {
                yield return path;
            }
        }
    }

    public static IReadOnlyList<string> GetDefaultSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<string>();
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdg)) {
            roots.Add(Path.Combine(xdg, "Steam"));
        }

        roots.Add(Path.Combine(home, ".local", "share", "Steam"));
        roots.Add(Path.Combine(home, ".steam", "steam"));
        roots.Add(Path.Combine(home, ".steam", "root"));
        roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"));
        roots.Add(Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam"));
        return roots;
    }
}
