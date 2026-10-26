using System.Globalization;
using AcTools.Core.Data;

namespace AcManager.Core.Settings;

/// <summary>
/// Custom Shaders Patch presence and version from the game folder.
/// The version lives in extension/config/data_manifest.ini next to dwrite.dll.
/// </summary>
public sealed record PatchInfo(bool Installed, string Version, int? Build)
{
    public static PatchInfo Missing { get; } = new(false, "", null);

    public string Label =>
        !Installed ? "Not installed"
        : Build is int build ? Version + " (build " + build.ToString(CultureInfo.InvariantCulture) + ")"
        : Version;
}

public static class PatchInfoFile
{
    public static PatchInfo Read(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            return PatchInfo.Missing;
        }

        if (!File.Exists(Path.Combine(gameRoot, "dwrite.dll"))) {
            return PatchInfo.Missing;
        }

        var manifest = Path.Combine(gameRoot, "extension", "config", "data_manifest.ini");
        if (!File.Exists(manifest)) {
            return PatchInfo.Missing;
        }

        var version = IniFile.Load(manifest)["VERSION"];
        var name = version.Get("SHADERS_PATCH")?.Trim() ?? "";
        if (name.Length == 0) {
            return PatchInfo.Missing;
        }

        int? build = int.TryParse(version.Get("SHADERS_PATCH_BUILD"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
        return new PatchInfo(true, name, build);
    }
}
