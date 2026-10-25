using System.ComponentModel;
using System.Diagnostics;
using AcTools.Platform;

namespace AcTools.Platform.Linux;

public sealed class SteamGameLauncher : IGameLauncher
{
    private readonly IReadOnlyList<string> _steamRoots;

    public SteamGameLauncher()
        : this(SteamAcDiscovery.GetDefaultSteamRoots())
    {
    }

    public SteamGameLauncher(IReadOnlyList<string> steamRoots)
    {
        _steamRoots = steamRoots;
    }

    public GameLaunchStatus LaunchSession(AcInstallLayout layout)
    {
        if (string.IsNullOrWhiteSpace(layout.GameRoot) || ProtonSession.FindGameExecutable(layout.GameRoot) == null) {
            return GameLaunchStatus.GameNotFound;
        }

        var command = ProtonSession.Resolve(layout, _steamRoots);
        if (command == null) {
            return GameLaunchStatus.ProtonNotFound;
        }

        try {
            var start = new ProcessStartInfo {
                FileName = command.ProtonScript,
                WorkingDirectory = command.WorkingDirectory,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(ProtonSession.LaunchVerb);
            start.ArgumentList.Add(command.GameExecutable);
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
            Process.Start(start);
            return GameLaunchStatus.SessionStarted;
        } catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException) {
            return GameLaunchStatus.LaunchFailed;
        }
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
