using System.Diagnostics;
using AcTools.Platform;

namespace AcTools.Platform.Linux;

public sealed class SteamGameLauncher : IGameLauncher
{
    public GameLaunchStatus LaunchAssettoCorsa()
    {
        var steam = FindSteamExecutable();
        if (steam == null) {
            return GameLaunchStatus.SteamNotFound;
        }

        Process.Start(new ProcessStartInfo(steam, $"-applaunch {AcInstallLayout.AssettoCorsaAppId}") {
            UseShellExecute = false,
        });
        return GameLaunchStatus.SteamInvoked;
    }

    public bool IsGameRunning()
    {
        try {
            return Process.GetProcesses().Any(process => {
                try {
                    return process.ProcessName.Equals("acs.exe", StringComparison.OrdinalIgnoreCase);
                } catch (InvalidOperationException) {
                    return false;
                }
            });
        } catch (SystemException) {
            return false;
        }
    }

    public static string? FindSteamExecutable()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) {
            return null;
        }

        foreach (var directory in path.Split(':', StringSplitOptions.RemoveEmptyEntries)) {
            var candidate = Path.Combine(directory, "steam");
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        return null;
    }
}
