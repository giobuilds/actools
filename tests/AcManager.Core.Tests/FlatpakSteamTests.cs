using System.Runtime.Versioning;
using AcTools.Platform;
using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class FlatpakSteamTests
{
    [Fact]
    public void Sandbox_path_keeps_the_data_directory_and_rewrites_the_persisted_home()
    {
        var root = Directory.CreateTempSubdirectory("actools-flatpak-path-").FullName;
        try {
            var app = Path.Combine(root, ".var", "app", FlatpakSteam.AppId);
            var dataProton = Path.Combine(app, "data", "Steam", "steamapps", "common", "Proton 9.0", "proton");
            var persisted = Path.Combine(app, ".local", "share", "Steam", "compatibilitytools.d", "GE-Proton10-32", "proton");
            var outside = Path.Combine(root, "library", "steamapps", "common", "assettocorsa");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            Assert.False(FlatpakSteam.Owns(outside));
            Assert.Equal(Path.GetFullPath(dataProton), FlatpakSteam.SandboxPath(dataProton));
            Assert.Equal(
                Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d", "GE-Proton10-32", "proton"),
                FlatpakSteam.SandboxPath(persisted));
            Assert.Null(FlatpakSteam.AppDirectory(Path.Combine(root, ".var", "app", FlatpakSteam.AppId + "-extra", "data")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Arguments_expose_a_library_that_sits_outside_the_sandbox()
    {
        var root = Directory.CreateTempSubdirectory("actools-flatpak-args-").FullName;
        try {
            var app = Path.Combine(root, ".var", "app", FlatpakSteam.AppId);
            var steam = Path.Combine(app, ".local", "share", "Steam");
            var library = Path.Combine(root, "library");
            var game = Path.Combine(library, "steamapps", "common", "assettocorsa");
            var compat = Path.Combine(library, "steamapps", "compatdata", "244210");
            var proton = Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32", "proton");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sandboxSteam = Path.Combine(home, ".local", "share", "Steam");
            var command = new ProtonSessionCommand(
                proton,
                Path.Combine(game, "acs.exe"),
                game,
                compat,
                steam,
                library);

            var arguments = FlatpakSteam.Arguments(command);

            Assert.Contains("--filesystem=" + Path.GetFullPath(library) + ":rw", arguments);
            Assert.Contains("--filesystem=" + Path.GetFullPath(game) + ":rw", arguments);
            Assert.Contains("--filesystem=" + Path.GetFullPath(compat) + ":rw", arguments);
            Assert.DoesNotContain(arguments, argument => argument.Contains(FlatpakSteam.AppId + "/.local", StringComparison.Ordinal));
            Assert.Contains("--cwd=" + game, arguments);
            Assert.Contains("--env=STEAM_COMPAT_CLIENT_INSTALL_PATH=" + sandboxSteam, arguments);
            Assert.Contains("--env=STEAM_COMPAT_DATA_PATH=" + compat, arguments);
            Assert.Contains("--command=" + Path.Combine(sandboxSteam, "compatibilitytools.d", "GE-Proton10-32", "proton"), arguments);
            Assert.Equal(FlatpakSteam.AppId, arguments[^3]);
            Assert.Equal("waitforexitandrun", arguments[^2]);
            Assert.Equal(Path.Combine(game, "acs.exe"), arguments[^1]);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Launch_runs_flatpak_steam_inside_the_sandbox()
    {
        var root = Directory.CreateTempSubdirectory("actools-flatpak-run-").FullName;
        try {
            var app = Path.Combine(root, ".var", "app", FlatpakSteam.AppId);
            var steam = Path.Combine(app, ".local", "share", "Steam");
            Directory.CreateDirectory(Path.Combine(steam, "ubuntu12_32"));
            var game = Path.Combine(steam, "steamapps", "common", "assettocorsa");
            Directory.CreateDirectory(game);
            var acs = Path.Combine(game, "acs.exe");
            File.WriteAllText(acs, "");
            var compat = Path.Combine(steam, "steamapps", "compatdata", "244210");
            Directory.CreateDirectory(Path.Combine(compat, "pfx"));
            File.WriteAllText(Path.Combine(compat, "version"), "GE-Proton10-32\n");
            var record = Path.Combine(root, "record");
            Directory.CreateDirectory(record);
            var proton = Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32", "proton");
            Directory.CreateDirectory(Path.GetDirectoryName(proton)!);
            File.WriteAllText(proton, "#!/bin/sh\nexit 0\n");
            var flatpak = Path.Combine(root, "flatpak");
            File.WriteAllText(flatpak, FlatpakScript(record));
            File.SetUnixFileMode(flatpak, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([steam], state, () => true, flatpak);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sandboxSteam = Path.Combine(home, ".local", "share", "Steam");

            var status = launcher.LaunchSession(new AcInstallLayout(game, null, Path.Combine(compat, "pfx"), "Steam"));

            Assert.Equal(GameLaunchStatus.SessionStarted, status);
            var log = Path.Combine(state, "launch.log");
            Assert.Contains("Flatpak Proton output is in " + log, launcher.LaunchDetail, StringComparison.Ordinal);
            var argsPath = Path.Combine(record, "args");
            WaitFor(argsPath);
            var args = File.ReadAllLines(argsPath);
            Assert.Equal("run", args[0]);
            Assert.DoesNotContain(args, argument => argument.StartsWith("--filesystem=", StringComparison.Ordinal));
            Assert.Contains("--cwd=" + Path.Combine(sandboxSteam, "steamapps", "common", "assettocorsa"), args);
            Assert.Contains("--env=STEAM_COMPAT_DATA_PATH=" + Path.Combine(sandboxSteam, "steamapps", "compatdata", "244210"), args);
            Assert.Contains("--env=STEAM_COMPAT_CLIENT_INSTALL_PATH=" + sandboxSteam, args);
            Assert.Contains("--env=SteamAppId=244210", args);
            Assert.Contains("--command=" + Path.Combine(sandboxSteam, "compatibilitytools.d", "GE-Proton10-32", "proton"), args);
            Assert.Equal([FlatpakSteam.AppId, "waitforexitandrun", Path.Combine(sandboxSteam, "steamapps", "common", "assettocorsa", "acs.exe")], args[^3..]);
            var sidPath = Path.Combine(record, "sid");
            WaitFor(sidPath);
            Assert.NotEqual(OwnSessionId(), File.ReadAllText(sidPath).Trim());
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Launch_reports_a_missing_flatpak_command()
    {
        var root = Directory.CreateTempSubdirectory("actools-flatpak-missing-").FullName;
        try {
            var app = Path.Combine(root, ".var", "app", FlatpakSteam.AppId);
            var steam = Path.Combine(app, "data", "Steam");
            Directory.CreateDirectory(Path.Combine(steam, "ubuntu12_32"));
            var game = Path.Combine(steam, "steamapps", "common", "assettocorsa");
            Directory.CreateDirectory(game);
            File.WriteAllText(Path.Combine(game, "acs.exe"), "");
            var compat = Path.Combine(steam, "steamapps", "compatdata", "244210");
            Directory.CreateDirectory(Path.Combine(compat, "pfx"));
            File.WriteAllText(Path.Combine(compat, "version"), "GE-Proton10-32\n");
            var protonDir = Path.Combine(steam, "compatibilitytools.d", "GE-Proton10-32");
            Directory.CreateDirectory(protonDir);
            File.WriteAllText(Path.Combine(protonDir, "proton"), "#!/bin/sh\nexit 0\n");
            var state = Path.Combine(root, "state");
            var launcher = new SteamGameLauncher([steam], state, () => true, Path.Combine(root, "missing-flatpak"));

            var status = launcher.LaunchSession(new AcInstallLayout(game, null, Path.Combine(compat, "pfx"), "Steam"));

            Assert.Equal(GameLaunchStatus.LaunchFailed, status);
            Assert.Equal("Flatpak is not installed, so this Steam prefix cannot be started.", launcher.LaunchDetail);
            Assert.False(File.Exists(Path.Combine(state, "launch.log")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    private static string FlatpakScript(string record) =>
        "#!/bin/sh\n"
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
