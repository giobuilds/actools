using AcTools.Utils;

namespace AcManager.Core.Content;

/// <summary>
/// Car ids from <c>content/sfx/GUIDs.txt</c>, the list <c>CarObject.TestIfKunos</c> uses.
/// Each id is stored lowercased. The folder name is compared as written.
/// </summary>
public static class OfficialCarIds
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    public static IReadOnlySet<string> FromGameRoot(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            return None;
        }

        return FromFile(AcPaths.GetSfxGuidsFilename(gameRoot));
    }

    public static IReadOnlySet<string> FromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            return None;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        try {
            foreach (var line in File.ReadLines(path)) {
                var parts = line.Split('/');
                if (parts.Length > 3
                    && parts[1] == "cars"
                    && parts[0].EndsWith("event:", StringComparison.Ordinal)) {
                    ids.Add(parts[2].ToLowerInvariant());
                }
            }
        } catch (IOException) {
            return None;
        } catch (UnauthorizedAccessException) {
            return None;
        }

        return ids;
    }
}
