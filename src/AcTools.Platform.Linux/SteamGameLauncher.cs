using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using AcTools.Platform;

namespace AcTools.Platform.Linux;

public sealed class SteamGameLauncher : IGameLauncher
{
    private const string SetsidPath = "/usr/bin/setsid";

    private const string FlatpakPath = "/usr/bin/flatpak";

    private const string LogScript = "exec \"$0\" \"$@\" </dev/null >>\"$ACTOOLS_LAUNCH_LOG\" 2>&1";

    /// <summary>
    /// Keeps each launch rooted so the runtime can reap it after the race exits.
    /// </summary>
    private static readonly ConcurrentBag<Process> DetachedSessions = new();

    private readonly IReadOnlyList<string> _steamRoots;

    private readonly string _stateDirectory;

    private readonly Func<bool>? _steamIsRunning;

    private readonly string _flatpakExecutable;

    private readonly string? _bridgeExecutable;

    public SteamGameLauncher()
        : this(SteamAcDiscovery.GetDefaultSteamRoots(), XdgAppPaths.DefaultStateDirectory())
    {
    }

    public SteamGameLauncher(IReadOnlyList<string> steamRoots)
        : this(steamRoots, XdgAppPaths.DefaultStateDirectory())
    {
    }

    public SteamGameLauncher(
        IReadOnlyList<string> steamRoots,
        string stateDirectory,
        Func<bool>? steamIsRunning = null,
        string? flatpakExecutable = null,
        string? bridgeExecutable = null)
    {
        _steamRoots = steamRoots;
        _stateDirectory = Path.GetFullPath(stateDirectory);
        _steamIsRunning = steamIsRunning;
        _flatpakExecutable = string.IsNullOrWhiteSpace(flatpakExecutable) ? FlatpakPath : flatpakExecutable;
        _bridgeExecutable = bridgeExecutable;
        LaunchLogPath = Path.Combine(_stateDirectory, "launch.log");
    }

    public string LaunchDetail { get; private set; } = "";

    public string LaunchLogPath { get; }

    public GameLaunchStatus LaunchSession(AcInstallLayout layout)
    {
        LaunchDetail = "";
        if (string.IsNullOrWhiteSpace(layout.GameRoot) || ProtonSession.FindGameExecutable(layout.GameRoot) == null) {
            LaunchDetail = "acs.exe is missing from the game folder, so the session was not started.";
            return GameLaunchStatus.GameNotFound;
        }

        var command = ProtonSession.Resolve(layout, _steamRoots);
        if (command == null) {
            LaunchDetail = ProtonSession.Explain(layout, _steamRoots);
            return GameLaunchStatus.ProtonNotFound;
        }

        if (HasSteamClientFiles() && !SteamProcessIsRunning()) {
            LaunchDetail = "Steam is not running. Start Steam, then press Drive again.";
            return GameLaunchStatus.SteamNotRunning;
        }

        var insideFlatpak = FlatpakSteam.Owns(command.ProtonScript) || FlatpakSteam.Owns(command.SteamClientPath);
        if (insideFlatpak && !File.Exists(_flatpakExecutable)) {
            LaunchDetail = "Flatpak is not installed, so this Steam prefix cannot be started.";
            return GameLaunchStatus.LaunchFailed;
        }

        string? logPath = null;
        try {
            logPath = PrepareLog();
            try {
                var staged = AcBridgeFiles.Stage(BridgeSource(), command.CompatDataPath);
                if (staged != null) {
                    StartAwayFromTheTerminal(StartInfo(command, insideFlatpak, logPath, staged, [
                        "--port",
                        AcBridgeFiles.Port.ToString(CultureInfo.InvariantCulture),
                    ]));
                }
            } catch (Win32Exception) {
                // The race still starts. Live timing stays off when the helper does not.
            } catch (IOException) {
            } catch (InvalidOperationException) {
            } catch (UnauthorizedAccessException) {
            }

            var start = StartInfo(command, insideFlatpak, logPath, command.GameExecutable, null);
            StartAwayFromTheTerminal(start);
            LaunchDetail = (insideFlatpak ? "Flatpak Proton output is in " : "Proton output is in ") + logPath + ".";
            return GameLaunchStatus.SessionStarted;
        } catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException) {
            LaunchDetail = logPath == null
                ? ex.Message
                : "Proton did not start. The log is " + logPath + ". " + ex.Message;
            return GameLaunchStatus.LaunchFailed;
        }
    }

    private string BridgeSource()
    {
        if (_bridgeExecutable != null) {
            return _bridgeExecutable;
        }

        return AcBridgeFiles.BesideTheApp(AppContext.BaseDirectory);
    }

    private ProcessStartInfo StartInfo(
        ProtonSessionCommand command,
        bool insideFlatpak,
        string logPath,
        string executable,
        IReadOnlyList<string>? extraArguments)
    {
        var start = new ProcessStartInfo {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
        };
        if (insideFlatpak) {
            start.FileName = _flatpakExecutable;
            var launched = command with { GameExecutable = executable };
            foreach (var argument in FlatpakSteam.Arguments(launched)) {
                start.ArgumentList.Add(argument);
            }
        } else {
            start.FileName = command.ProtonScript;
            start.ArgumentList.Add(ProtonSession.LaunchVerb);
            start.ArgumentList.Add(executable);
            start.Environment["STEAM_COMPAT_DATA_PATH"] = command.CompatDataPath;
            start.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = command.SteamClientPath;
            start.Environment["STEAM_COMPAT_INSTALL_PATH"] = command.WorkingDirectory;
            if (!string.IsNullOrEmpty(command.LibraryPath)) {
                start.Environment["STEAM_COMPAT_LIBRARY_PATHS"] = command.LibraryPath;
            }

            var appId = AcInstallLayout.AssettoCorsaAppId.ToString();
            start.Environment["STEAM_COMPAT_APP_ID"] = appId;
            start.Environment["SteamAppId"] = appId;
            start.Environment["SteamGameId"] = appId;
            start.Environment["SteamOverlayGameId"] = appId;
        }

        if (extraArguments != null) {
            foreach (var extra in extraArguments) {
                start.ArgumentList.Add(extra);
            }
        }

        start.Environment["ACTOOLS_LAUNCH_LOG"] = logPath;
        return start;
    }

    /// <summary>
    /// Starts Proton in a new session and appends its output to the launch log.
    /// Closing this app returns the terminal and leaves the race running.
    /// </summary>
    private static void StartAwayFromTheTerminal(ProcessStartInfo start)
    {
        var command = start.FileName;
        var arguments = start.ArgumentList.ToList();
        start.ArgumentList.Clear();
        if (File.Exists(SetsidPath)) {
            start.FileName = SetsidPath;
            start.ArgumentList.Add("/bin/sh");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(LogScript);
        } else {
            start.FileName = "/bin/sh";
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(LogScript);
        }

        start.ArgumentList.Add(command);
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        var process = new Process {
            StartInfo = start,
            EnableRaisingEvents = true,
        };
        process.Start();
        DetachedSessions.Add(process);
    }

    private string PrepareLog()
    {
        Directory.CreateDirectory(_stateDirectory);
        var header = "---- "
            + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            + "Z ----"
            + Environment.NewLine;
        File.AppendAllText(LaunchLogPath, header);
        return LaunchLogPath;
    }

    /// <summary>
    /// A directory named ubuntu12_32 is not the client. The check looks for the client binary.
    /// </summary>
    private bool HasSteamClientFiles()
    {
        foreach (var root in _steamRoots) {
            if (string.IsNullOrWhiteSpace(root)) {
                continue;
            }

            if (File.Exists(Path.Combine(root, "ubuntu12_32", "steam"))
                || File.Exists(Path.Combine(root, "steam.sh"))) {
                return true;
            }
        }

        return false;
    }

    private bool SteamProcessIsRunning()
    {
        if (_steamIsRunning != null) {
            return _steamIsRunning();
        }

        Process[] processes;
        try {
            processes = Process.GetProcesses();
        } catch (SystemException) {
            // A failed process list is not proof that Steam is stopped.
            return true;
        }

        try {
            foreach (var process in processes) {
                try {
                    if (process.ProcessName.Equals("steam", StringComparison.Ordinal)) {
                        return true;
                    }
                } catch (InvalidOperationException) {
                    // The process exited while its name was being read.
                }
            }
        } finally {
            foreach (var process in processes) {
                process.Dispose();
            }
        }

        return false;
    }

    public bool IsGameRunning()
    {
        try {
            return Process.GetProcesses().Any(process => {
                try {
                    return process.ProcessName.Equals("acs.exe", StringComparison.OrdinalIgnoreCase)
                        || process.ProcessName.Equals("acs_pro.exe", StringComparison.OrdinalIgnoreCase);
                } catch (InvalidOperationException) {
                    return false;
                }
            });
        } catch (SystemException) {
            return false;
        }
    }
}
