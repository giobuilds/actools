using System.ComponentModel;
using System.Diagnostics;
using AcTools.Platform;

namespace AcManager.App.ViewModels;

internal static class LaunchFeedback
{
    public static string AfterWrite(IGameLauncher launcher, GameLaunchStatus launch, string path, string startedSentence)
    {
        if (launch == GameLaunchStatus.SessionStarted) {
            var status = "Wrote " + path + ". " + startedSentence;
            if (!string.IsNullOrEmpty(launcher.LaunchDetail)) {
                status += " " + launcher.LaunchDetail;
            }

            return status;
        }

        return "Wrote " + path + ". " + Failure(launcher, launch);
    }

    public static string GaveUp(IGameLauncher launcher)
    {
        if (string.IsNullOrEmpty(launcher.LaunchDetail)) {
            return "The game did not stay running.";
        }

        return "The game did not stay running. " + launcher.LaunchDetail;
    }

    public static string? TryOpen(IGameLauncher launcher)
    {
        var path = launcher.LaunchLogPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            return "There is no launch log yet.";
        }

        try {
            Process.Start(new ProcessStartInfo {
                FileName = "xdg-open",
                ArgumentList = { path },
                UseShellExecute = false,
            });
            return null;
        } catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException) {
            return ex.Message;
        }
    }

    private static string Failure(IGameLauncher launcher, GameLaunchStatus launch)
    {
        if (!string.IsNullOrEmpty(launcher.LaunchDetail)) {
            return launcher.LaunchDetail;
        }

        if (launch == GameLaunchStatus.GameNotFound) {
            return "acs.exe is missing from the game folder, so the session was not started.";
        }

        if (launch == GameLaunchStatus.LaunchFailed) {
            return "Proton did not start.";
        }

        if (launch == GameLaunchStatus.SteamNotRunning) {
            return "Steam is not running. Start Steam, then press Drive again.";
        }

        return "Could not find Proton for this prefix, so the session was not started.";
    }
}
