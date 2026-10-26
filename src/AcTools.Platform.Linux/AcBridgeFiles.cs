namespace AcTools.Platform.Linux;

/// <summary>
/// Copies AcBridge.exe into the prefix so Proton can start it next to acs.exe.
/// </summary>
public static class AcBridgeFiles
{
    public const int Port = 27150;

    public const string FileName = "AcBridge.exe";

    public static string PrefixExecutable(string compatDataPath) =>
        Path.Combine(compatDataPath, "pfx", "drive_c", "actools", FileName);

    public static string? Stage(string? source, string compatDataPath)
    {
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) {
            return null;
        }

        var destination = PrefixExecutable(compatDataPath);
        var directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory)) {
            return null;
        }

        Directory.CreateDirectory(directory);
        var fullSource = Path.GetFullPath(source);
        var fullDestination = Path.GetFullPath(destination);
        if (!string.Equals(fullSource, fullDestination, StringComparison.Ordinal)) {
            File.Copy(fullSource, fullDestination, overwrite: true);
        }

        return fullDestination;
    }
}
