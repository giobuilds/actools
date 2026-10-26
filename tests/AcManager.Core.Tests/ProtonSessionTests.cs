using System.Runtime.Versioning;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class ProtonSessionTests
{
    [Fact]
    public void Resolve_uses_the_proton_build_named_in_the_prefix()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-").FullName;
        try {
            var steam = ClientRoot(root);
            var game = WriteGame(steam);
            var compat = WriteCompat(steam, "GE-Proton10-32\n");
            var script = WriteProton(Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32"));

            var command = ProtonSession.Resolve(Layout(game, compat), [steam]);

            Assert.NotNull(command);
            Assert.Equal(script, command.ProtonScript);
            Assert.Equal(Path.Combine(game, "acs.exe"), command.GameExecutable);
            Assert.Equal(game, command.WorkingDirectory);
            Assert.Equal(compat, command.CompatDataPath);
            Assert.Equal(steam, command.SteamClientPath);
            Assert.Equal(steam, command.LibraryPath);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Resolve_reads_the_compat_tool_mapping_and_skips_other_app_sections()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-vdf-").FullName;
        try {
            var steam = ClientRoot(root);
            var game = WriteGame(steam);
            var compat = WriteCompat(steam, version: null);
            Directory.CreateDirectory(Path.Combine(steam, "config"));
            File.WriteAllText(Path.Combine(steam, "config", "config.vdf"), """
                "InstallConfigStore"
                {
                    "244210"
                    {
                        "name"		"shader-decoy"
                        "ShaderCacheSize"		"291311013"
                    }
                    "CompatToolMapping"
                    {
                        "8500"
                        {
                            "name"		"GE-Proton-decoy"
                            "config"		""
                            "priority"		"250"
                        }
                        "244210"
                        {
                            "name"		"proton_experimental"
                            "config"		""
                            "priority"		"250"
                        }
                    }
                }
                """);
            WriteProton(Path.Combine(steam, "compatibilitytools.d", "GE-Proton-decoy"));
            WriteProton(Path.Combine(steam, "compatibilitytools.d", "shader-decoy"));
            var expected = WriteProton(Path.Combine(steam, "steamapps", "common", "Proton - Experimental"));

            var command = ProtonSession.Resolve(Layout(game, compat), [steam]);

            Assert.NotNull(command);
            Assert.Equal(expected, command.ProtonScript);
            Assert.Equal("proton_experimental", SteamConfigVdf.ReadCompatToolName(File.ReadAllText(Path.Combine(steam, "config", "config.vdf")), "244210"));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Resolve_follows_the_proton_path_recorded_in_config_info()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-info-").FullName;
        try {
            var steam = ClientRoot(root);
            var game = WriteGame(steam);
            var compat = WriteCompat(steam, "missing-tool\n");
            var outside = Path.Combine(root, "CustomProton");
            var fonts = Path.Combine(outside, "files", "share", "fonts");
            Directory.CreateDirectory(fonts);
            var script = WriteProton(outside);
            File.WriteAllText(Path.Combine(compat, "config_info"), "missing-tool\n" + fonts + "\n" + steam + "\n");

            var command = ProtonSession.Resolve(Layout(game, compat), [steam]);

            Assert.NotNull(command);
            Assert.Equal(script, command.ProtonScript);
            Assert.Equal(steam, command.SteamClientPath);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Launch_starts_acs_under_proton_instead_of_the_official_launcher()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-run-").FullName;
        try {
            var steam = ClientRoot(root);
            var game = WriteGame(steam);
            var compat = WriteCompat(steam, "GE-Proton10-32\n");
            var record = Path.Combine(root, "record");
            Directory.CreateDirectory(record);
            var proton = Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32", "proton");
            Directory.CreateDirectory(Path.GetDirectoryName(proton)!);
            File.WriteAllText(proton, Script(record));
            File.SetUnixFileMode(proton, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var acs = Path.Combine(game, "acs.exe");
            File.WriteAllText(Path.Combine(steam, "steam.sh"), "#!/bin/sh\nexit 0\n");
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([steam], state, () => true);

            var status = launcher.LaunchSession(Layout(game, compat));

            Assert.Equal(GameLaunchStatus.SessionStarted, status);
            var log = Path.Combine(state, "launch.log");
            Assert.Equal(log, launcher.LaunchLogPath);
            Assert.Contains("Proton output is in " + log, launcher.LaunchDetail, StringComparison.Ordinal);
            Assert.Matches(@"---- \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}Z ----", File.ReadAllText(log));
            var argsPath = Path.Combine(record, "args");
            WaitFor(argsPath);
            Assert.Equal(["waitforexitandrun", acs], File.ReadAllLines(argsPath));
            Assert.DoesNotContain("applaunch", File.ReadAllText(argsPath), StringComparison.Ordinal);
            Assert.DoesNotContain("AssettoCorsa.exe", File.ReadAllText(argsPath), StringComparison.Ordinal);
            Assert.Equal(log, File.ReadAllText(Path.Combine(record, "logenv")).Trim());
            Assert.Equal(compat, File.ReadAllText(Path.Combine(record, "compat")).Trim());
            Assert.Equal(steam, File.ReadAllText(Path.Combine(record, "client")).Trim());
            Assert.Equal(game, File.ReadAllText(Path.Combine(record, "install")).Trim());
            Assert.Equal("244210", File.ReadAllText(Path.Combine(record, "appid")).Trim());
            Assert.EndsWith(
                Path.Combine("steamapps", "common", "assettocorsa"),
                File.ReadAllText(Path.Combine(record, "cwd")).Trim());
            Assert.NotEqual(OwnSessionId(), File.ReadAllText(Path.Combine(record, "sid")).Trim());
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Launch_does_not_start_a_process_when_proton_or_the_game_is_missing()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-miss-").FullName;
        try {
            var steam = ClientRoot(root);
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([steam], state);
            var missingGame = launcher.LaunchSession(new AcInstallLayout(root, null, null, "test"));
            Assert.Equal(GameLaunchStatus.GameNotFound, missingGame);
            Assert.Contains("acs.exe is missing", launcher.LaunchDetail, StringComparison.Ordinal);

            var game = WriteGame(steam);
            var compat = WriteCompat(steam, "not-a-real-proton\n");
            var missingProton = launcher.LaunchSession(Layout(game, compat));
            Assert.Equal(GameLaunchStatus.ProtonNotFound, missingProton);
            Assert.Contains("Proton build not found", launcher.LaunchDetail, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(state, "launch.log")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static string ClientRoot(string root)
    {
        var steam = Path.Combine(root, "Steam");
        Directory.CreateDirectory(Path.Combine(steam, "ubuntu12_32"));
        return steam;
    }

    private static string WriteGame(string steam)
    {
        var game = Path.Combine(steam, "steamapps", "common", "assettocorsa");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "acs.exe"), "");
        return game;
    }

    private static string WriteCompat(string steam, string? version)
    {
        var compat = Path.Combine(steam, "steamapps", "compatdata", "244210");
        Directory.CreateDirectory(Path.Combine(compat, "pfx"));
        if (version != null) {
            File.WriteAllText(Path.Combine(compat, "version"), version);
        }

        return compat;
    }

    private static string WriteProton(string directory)
    {
        Directory.CreateDirectory(directory);
        var script = Path.Combine(directory, "proton");
        File.WriteAllText(script, "#!/bin/sh\nexit 0\n");
        return script;
    }

    private static AcInstallLayout Layout(string game, string compat) =>
        new(game, null, Path.Combine(compat, "pfx"), "Steam");

    [Fact]
    public void Launch_explains_a_missing_prefix_and_a_missing_steam_client()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-gap-").FullName;
        try {
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([], state);
            var game = Path.Combine(root, "assettocorsa");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");
            var noPrefix = launcher.LaunchSession(new AcInstallLayout(game, null, null, "test"));
            Assert.Equal(GameLaunchStatus.ProtonNotFound, noPrefix);
            Assert.Contains("no Proton prefix", launcher.LaunchDetail, StringComparison.Ordinal);

            var steam = Path.Combine(root, "Steam");
            Directory.CreateDirectory(steam);
            var installed = WriteGame(steam);
            var compat = WriteCompat(steam, "GE-Proton10-32\n");
            var outside = Path.Combine(root, "CustomProton");
            WriteProton(outside);
            File.WriteAllText(Path.Combine(compat, "config_info"), "GE-Proton10-32\n" + outside + "\n");
            var clientLauncher = new SteamGameLauncher([steam], state);
            var noClient = clientLauncher.LaunchSession(Layout(installed, compat));
            Assert.Equal(GameLaunchStatus.ProtonNotFound, noClient);
            Assert.Contains("Steam client that owns this prefix", clientLauncher.LaunchDetail, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Launch_refuses_when_the_steam_client_is_not_running()
    {
        var root = Directory.CreateTempSubdirectory("actools-proton-steam-").FullName;
        try {
            var steam = ClientRoot(root);
            var game = WriteGame(steam);
            var compat = WriteCompat(steam, "GE-Proton10-32\n");
            var record = Path.Combine(root, "record");
            Directory.CreateDirectory(record);
            var proton = Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32", "proton");
            Directory.CreateDirectory(Path.GetDirectoryName(proton)!);
            File.WriteAllText(proton, Script(record));
            File.SetUnixFileMode(proton, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(Path.Combine(steam, "ubuntu12_32", "steam"), "");
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([steam], state, () => false);

            var status = launcher.LaunchSession(Layout(game, compat));

            Assert.Equal(GameLaunchStatus.SteamNotRunning, status);
            Assert.Equal("Steam is not running. Start Steam, then press Drive again.", launcher.LaunchDetail);
            Thread.Sleep(250);
            Assert.False(File.Exists(Path.Combine(record, "args")));
            Assert.False(File.Exists(Path.Combine(state, "launch.log")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static string Script(string record) =>
        "#!/bin/sh\n"
        + "printf '%s\\n' \"$ACTOOLS_LAUNCH_LOG\" > '" + record + "/logenv'\n"
        + "printf '%s\\n' \"$PWD\" > '" + record + "/cwd'\n"
        + "printf '%s\\n' \"$STEAM_COMPAT_DATA_PATH\" > '" + record + "/compat'\n"
        + "printf '%s\\n' \"$STEAM_COMPAT_CLIENT_INSTALL_PATH\" > '" + record + "/client'\n"
        + "printf '%s\\n' \"$STEAM_COMPAT_INSTALL_PATH\" > '" + record + "/install'\n"
        + "printf '%s\\n' \"$SteamAppId\" > '" + record + "/appid'\n"
        + "printf '%s\\n' \"$@\" > '" + record + "/args'\n"
        + "session=$(awk 'BEGIN{RS=\")\"} NR==2{print $4}' /proc/self/stat)\n"
        + "printf '%s\\n' \"$session\" > '" + record + "/sid'\n";

    private static string OwnSessionId()
    {
        var stat = File.ReadAllText("/proc/self/stat");
        var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
        return fields[3];
    }

    private static void WaitFor(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!File.Exists(path) && DateTime.UtcNow < deadline) {
            Thread.Sleep(20);
        }
    }
}
