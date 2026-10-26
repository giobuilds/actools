using AcTools.Platform;

namespace AcManager.Core.Settings;

/// <summary>
/// Compares SDL device names with the names already stored in controls.ini.
/// A different name means the game has not recorded that device yet.
/// </summary>
public static class ConnectedControllers
{
    public const string BindOnce =
        "Bind once inside Assetto Corsa so the game records a connected device. Saving here leaves device and axis bindings unchanged.";

    public static IReadOnlyList<string> Lines(IReadOnlyList<string> savedNames, InputDeviceList connected)
    {
        if (!string.IsNullOrEmpty(connected.Unavailable)) {
            return [connected.Unavailable];
        }

        if (connected.Devices.Count == 0) {
            return ["No joysticks are connected."];
        }

        var lines = new List<string>(connected.Devices.Count + 1);
        var needsBind = false;
        foreach (var device in connected.Devices) {
            lines.Add(device.Label);
            if (!IsSaved(savedNames, device.Name)) {
                needsBind = true;
            }
        }

        if (needsBind) {
            lines.Add(BindOnce);
        }

        return lines;
    }

    private static bool IsSaved(IReadOnlyList<string> savedNames, string deviceName)
    {
        foreach (var saved in savedNames) {
            if (saved.Trim().Equals(deviceName.Trim(), StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}
