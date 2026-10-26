using System.Globalization;

namespace AcManager.Core.Results;

public sealed record ReplayFile(string RelativePath, long Bytes, DateTime WrittenUtc)
{
    public string Label
    {
        get {
            var size = Bytes >= 1024 * 1024
                ? (Bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                : Math.Max(1, (Bytes + 1023) / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
            return RelativePath
                + "  "
                + size
                + "  "
                + WrittenUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                + "Z";
        }
    }
}

public static class ReplayLibrary
{
    public static string DirectoryFor(string documentsDirectory) =>
        Path.Combine(documentsDirectory, "replay");

    public static IReadOnlyList<ReplayFile> List(string documentsDirectory)
    {
        var directory = DirectoryFor(documentsDirectory);
        if (!Directory.Exists(directory)) {
            return [];
        }

        var files = new List<ReplayFile>();
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) {
            if (!path.EndsWith(".acreplay", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var info = new FileInfo(path);
            var relative = Path.GetRelativePath(directory, path);
            var withoutExtension = relative[..^".acreplay".Length];
            files.Add(new ReplayFile(withoutExtension.Replace('\\', '/'), info.Length, info.LastWriteTimeUtc));
        }

        return files
            .OrderByDescending(file => file.WrittenUtc)
            .ThenBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }
}
