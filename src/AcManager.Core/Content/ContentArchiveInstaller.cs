using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AcManager.Core.Content;

public enum InstalledContentKind
{
    Car,
    Track,
}

public sealed record ArchiveEntryPlan(InstalledContentKind Kind, string Id, string ArchivePrefix);

public sealed record InstalledPiece(string Kind, string Id, string Destination, string? Note = null);

public sealed record ContentInstallResult(
    IReadOnlyList<InstalledPiece> Installed,
    IReadOnlyList<string> Skipped);

/// <summary>
/// Copies a car or a track out of a zip or a 7z into content/cars or content/tracks.
/// The archive must contain ui/ui_car.json or ui/ui_track.json. Existing folders are left alone.
/// Both kinds rewrite surfaces.ini pitch values and Custom Shaders Patch shader names.
/// </summary>
public static class ContentArchiveInstaller
{
    private const int MaxEntries = 20000;
    private const long MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static IReadOnlyList<ArchiveEntryPlan> Plan(IEnumerable<string> entryNames, string? archiveName)
    {
        var plans = new List<ArchiveEntryPlan>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in entryNames) {
            var name = Normalize(raw);
            if (name.Length == 0 || name.EndsWith('/')) {
                continue;
            }

            if (TryMarkerPrefix(name, "ui/ui_car.json", out var carPrefix)) {
                Add(plans, seen, InstalledContentKind.Car, carPrefix, archiveName);
                continue;
            }

            if (TryMarkerPrefix(name, "ui/ui_track.json", out var trackPrefix)) {
                Add(plans, seen, InstalledContentKind.Track, trackPrefix, archiveName);
                continue;
            }

            if (TryLayoutPrefix(name, out var layoutPrefix)) {
                Add(plans, seen, InstalledContentKind.Track, layoutPrefix, archiveName);
            }
        }

        return plans;
    }

    public static ContentInstallResult Install(string archivePath, string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            throw new ArgumentException("A game folder is required.", nameof(gameRoot));
        }

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath)) {
            throw new FileNotFoundException("Could not find that zip.", archivePath);
        }

        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("Only .zip archives can be installed.");
        }

        ZipArchive archive;
        try {
            archive = ZipFile.OpenRead(archivePath);
        } catch (InvalidDataException) {
            throw new InvalidDataException("That file is not a zip archive.");
        }

        using (archive) {
            if (archive.Entries.Count > MaxEntries) {
                return new ContentInstallResult([], ["That zip has too many files."]);
            }

            var plans = Plan(archive.Entries.Select(entry => entry.FullName), Path.GetFileName(archivePath));
            if (plans.Count == 0) {
                return new ContentInstallResult([], [
                    "No car or track was found. The zip needs a ui/ui_car.json or ui/ui_track.json.",
                ]);
            }

            var installed = new List<InstalledPiece>();
            var skipped = new List<string>();
            foreach (var plan in plans) {
                var dest = Path.GetFullPath(Path.Combine(gameRoot, "content", Folder(plan.Kind), plan.Id));
                if (Directory.Exists(dest)) {
                    skipped.Add(Label(plan) + " already exists.");
                    continue;
                }

                var entries = EntriesUnder(archive, plan.ArchivePrefix);
                var rejection = Rejection(entries, plan.ArchivePrefix, dest);
                if (rejection != null) {
                    skipped.Add(Label(plan) + " was skipped because " + rejection + ".");
                    continue;
                }

                try {
                    Directory.CreateDirectory(dest);
                    Extract(entries, plan.ArchivePrefix, dest);
                    installed.Add(new InstalledPiece(
                        KindName(plan.Kind),
                        plan.Id,
                        dest,
                        VanillaNotes(plan.Kind, dest)));
                } catch (InvalidDataException ex) when (ex.Message.Contains("escapes", StringComparison.Ordinal)) {
                    TryDelete(dest);
                    skipped.Add(Label(plan) + " was skipped because an entry escapes the destination.");
                } catch (Exception ex) when (ex is InvalidDataException or CryptographicException) {
                    TryDelete(dest);
                    skipped.Add(Label(plan) + " was skipped because the archive entry could not be read. Password-protected zips are not supported.");
                } catch (Exception) {
                    TryDelete(dest);
                    throw;
                }
            }

            return new ContentInstallResult(installed, skipped);
        }
    }

    public static async Task<ContentInstallResult> InstallSevenZipAsync(
        string archivePath,
        string gameRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            throw new ArgumentException("A game folder is required.", nameof(gameRoot));
        }

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath)) {
            throw new FileNotFoundException("Could not find that archive.", archivePath);
        }

        if (!archivePath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("Only .zip and .7z archives can be installed.");
        }

        if (!ServerContentInstaller.HasSevenZipSignature(archivePath)) {
            throw new InvalidDataException("That file is not a 7z archive.");
        }

        if (ServerContentInstaller.FindSevenZip() == null) {
            throw new InvalidDataException("7-Zip is not installed, so these archives cannot be extracted.");
        }

        var temp = Directory.CreateTempSubdirectory("actools-7z-install-");
        try {
            var reason = await ServerContentInstaller.ExtractToDirectoryAsync(archivePath, temp.FullName, cancellationToken)
                .ConfigureAwait(false);
            if (reason != null) {
                return new ContentInstallResult([], [SevenZipProblem(reason)]);
            }

            var plans = Plan(RelativeNames(temp.FullName), Path.GetFileName(archivePath));
            if (plans.Count == 0) {
                return new ContentInstallResult([], [
                    "No car or track was found. The archive needs a ui/ui_car.json or ui/ui_track.json.",
                ]);
            }

            var installed = new List<InstalledPiece>();
            var skipped = new List<string>();
            foreach (var plan in plans) {
                var dest = Path.GetFullPath(Path.Combine(gameRoot, "content", Folder(plan.Kind), plan.Id));
                if (Directory.Exists(dest)) {
                    skipped.Add(Label(plan) + " already exists.");
                    continue;
                }

                try {
                    Directory.CreateDirectory(dest);
                    CopyExtracted(temp.FullName, plan.ArchivePrefix, dest);
                    installed.Add(new InstalledPiece(
                        KindName(plan.Kind),
                        plan.Id,
                        dest,
                        VanillaNotes(plan.Kind, dest)));
                } catch (InvalidDataException ex) when (ex.Message.Contains("symbolic", StringComparison.Ordinal)) {
                    TryDelete(dest);
                    skipped.Add(Label(plan) + " was skipped because an entry is a symbolic link.");
                } catch (InvalidDataException ex) when (ex.Message.Contains("escapes", StringComparison.Ordinal)) {
                    TryDelete(dest);
                    skipped.Add(Label(plan) + " was skipped because an entry escapes the destination.");
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    TryDelete(dest);
                    skipped.Add(Label(plan) + " was skipped because the archive entry could not be copied.");
                } catch (Exception) {
                    TryDelete(dest);
                    throw;
                }
            }

            return new ContentInstallResult(installed, skipped);
        } finally {
            TryDelete(temp.FullName);
        }
    }

    private static void Add(
        List<ArchiveEntryPlan> plans,
        HashSet<string> seen,
        InstalledContentKind kind,
        string prefix,
        string? archiveName)
    {
        if (!PrefixIsSafe(prefix) || !seen.Add(KindName(kind) + "\0" + prefix)) {
            return;
        }

        var id = IdFor(prefix, archiveName);
        if (id == null) {
            return;
        }

        plans.Add(new ArchiveEntryPlan(kind, id, prefix));
    }

    private static bool TryMarkerPrefix(string name, string marker, out string prefix)
    {
        prefix = "";
        if (name.Equals(marker, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (!name.EndsWith("/" + marker, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        prefix = name[..^marker.Length];
        return prefix.EndsWith('/');
    }

    private static bool TryLayoutPrefix(string name, out string prefix)
    {
        prefix = "";
        const string file = "ui_track.json";
        if (name.Length <= file.Length + 1 || !name.EndsWith(file, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (name[name.Length - file.Length - 1] != '/') {
            return false;
        }

        var directory = name[..^(file.Length + 1)];
        var parentSlash = directory.LastIndexOf('/');
        if (parentSlash < 0) {
            return false;
        }

        var parent = directory[(parentSlash + 1)..];
        if (parent.Length == 0 || parent.Equals("ui", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var above = directory[..parentSlash];
        var grandSlash = above.LastIndexOf('/');
        var grand = grandSlash < 0 ? above : above[(grandSlash + 1)..];
        if (!grand.Equals("ui", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        prefix = grandSlash < 0 ? "" : above[..(grandSlash + 1)];
        return true;
    }

    private static string? IdFor(string prefix, string? archiveName)
    {
        var trimmed = prefix.Trim('/');
        if (trimmed.Length == 0) {
            if (string.IsNullOrWhiteSpace(archiveName)) {
                return null;
            }

            return Sanitize(Path.GetFileNameWithoutExtension(archiveName));
        }

        var slash = trimmed.LastIndexOf('/');
        var id = slash < 0 ? trimmed : trimmed[(slash + 1)..];
        return IsSafeSegment(id) ? id : null;
    }

    private static string? Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name) {
            if (c == ':' || c < ' ' || Array.IndexOf(InvalidFileNameChars, c) >= 0) {
                continue;
            }

            builder.Append(c);
        }

        var id = builder.ToString().Trim();
        return IsSafeSegment(id) ? id : null;
    }

    private static bool PrefixIsSafe(string prefix)
    {
        if (prefix.Length == 0) {
            return true;
        }

        if (!prefix.EndsWith('/')) {
            return false;
        }

        foreach (var part in prefix.TrimEnd('/').Split('/')) {
            if (!IsSafeSegment(part)) {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeSegment(string segment)
    {
        if (segment.Length == 0 || segment is "." or "..") {
            return false;
        }

        foreach (var c in segment) {
            if (c == ':' || c < ' ' || Array.IndexOf(InvalidFileNameChars, c) >= 0) {
                return false;
            }
        }

        return true;
    }

    private static List<ZipArchiveEntry> EntriesUnder(ZipArchive archive, string prefix)
    {
        var list = new List<ZipArchiveEntry>();
        foreach (var entry in archive.Entries) {
            var name = Normalize(entry.FullName);
            if (name.Length == 0) {
                continue;
            }

            // The trailing slash keeps ks_foo from swallowing ks_foo_extra.
            if (prefix.Length == 0 || name.StartsWith(prefix, StringComparison.Ordinal)) {
                list.Add(entry);
            }
        }

        return list;
    }

    private static string? Rejection(IReadOnlyList<ZipArchiveEntry> entries, string prefix, string dest)
    {
        if (entries.Count > MaxEntries) {
            return "it has too many files";
        }

        long bytes = 0;
        foreach (var entry in entries) {
            if (entry.Length < 0) {
                return "an entry escapes the destination";
            }

            try {
                bytes = checked(bytes + entry.Length);
            } catch (OverflowException) {
                return "it is larger than 2 GB";
            }

            if (bytes > MaxUncompressedBytes) {
                return "it is larger than 2 GB";
            }

            var name = Normalize(entry.FullName);
            if (prefix.Length > 0 && !name.StartsWith(prefix, StringComparison.Ordinal)) {
                return "an entry escapes the destination";
            }

            var relative = prefix.Length == 0 ? name : name[prefix.Length..];
            if (relative.Length == 0) {
                continue;
            }

            if (!TrySafeRelative(relative, out var safe)) {
                return "an entry escapes the destination";
            }

            var candidate = Path.GetFullPath(Path.Combine(dest, safe.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(dest, candidate)) {
                return "an entry escapes the destination";
            }
        }

        return null;
    }

    private static void Extract(IReadOnlyList<ZipArchiveEntry> entries, string prefix, string dest)
    {
        foreach (var entry in entries) {
            var name = Normalize(entry.FullName);
            var relative = prefix.Length == 0 ? name : name[prefix.Length..];
            if (relative.Length == 0) {
                continue;
            }

            if (!TrySafeRelative(relative, out var safe)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            var target = Path.GetFullPath(Path.Combine(dest, safe.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(dest, target)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            if (name.EndsWith('/')) {
                Directory.CreateDirectory(target);
                continue;
            }

            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) {
                Directory.CreateDirectory(parent);
            }

            entry.ExtractToFile(target, overwrite: false);
        }
    }

    private static bool TrySafeRelative(string relative, out string safe)
    {
        safe = "";
        var trimmed = relative.TrimEnd('/');
        if (trimmed.Length == 0) {
            return false;
        }

        var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) {
            return false;
        }

        foreach (var part in parts) {
            if (!IsSafeSegment(part)) {
                return false;
            }
        }

        safe = string.Join('/', parts);
        return true;
    }

    private static bool IsInside(string root, string candidate)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string Normalize(string name) => name.Replace('\\', '/').TrimStart('/');

    private static string Folder(InstalledContentKind kind) =>
        kind == InstalledContentKind.Car ? "cars" : "tracks";

    private static string KindName(InstalledContentKind kind) =>
        kind == InstalledContentKind.Car ? "car" : "track";

    private static string Label(ArchiveEntryPlan plan) => KindName(plan.Kind) + " " + plan.Id;

    private static string? VanillaNotes(InstalledContentKind kind, string dest)
    {
        var notes = new List<string>();
        if (kind == InstalledContentKind.Track && TrackSurfaces.SanitizeTree(dest) > 0) {
            notes.Add("Adjusted surfaces.ini so pitch values load without Custom Shaders Patch.");
        }

        if (Kn5ShaderFallback.RewriteTree(dest) > 0) {
            notes.Add("Replaced Custom Shaders Patch materials with the original game's shaders.");
        }

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static List<string> RelativeNames(string extractedRoot)
    {
        var root = Path.GetFullPath(extractedRoot);
        var names = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            if (IsReparse(file)) {
                throw new InvalidDataException("An entry is a symbolic link.");
            }

            var full = Path.GetFullPath(file);
            if (!IsInside(root, full)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            names.Add(Normalize(Path.GetRelativePath(root, full)));
        }

        return names;
    }

    private static void CopyExtracted(string extractedRoot, string prefix, string dest)
    {
        var root = Path.GetFullPath(extractedRoot);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            if (IsReparse(file)) {
                throw new InvalidDataException("An entry is a symbolic link.");
            }

            var full = Path.GetFullPath(file);
            if (!IsInside(root, full)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            var name = Normalize(Path.GetRelativePath(root, full));
            if (prefix.Length > 0 && !name.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var relative = prefix.Length == 0 ? name : name[prefix.Length..];
            if (relative.Length == 0) {
                continue;
            }

            if (!TrySafeRelative(relative, out var safe)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            var target = Path.GetFullPath(Path.Combine(dest, safe.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(dest, target)) {
                throw new InvalidDataException("An entry escapes the destination.");
            }

            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) {
                Directory.CreateDirectory(parent);
            }

            File.Copy(full, target, overwrite: false);
        }
    }

    private static bool IsReparse(string path)
    {
        try {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) {
            return false;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return true;
        }
    }

    private static string SevenZipProblem(string reason)
    {
        if (reason.EndsWith('.')) {
            return reason;
        }

        return reason switch {
            "password-protected archives are not supported" => "Password-protected archives are not supported.",
            "an entry escapes the destination" => "An entry escapes the destination.",
            "an entry is a symbolic link" => "An entry is a symbolic link.",
            "it has too many files" => "That archive has too many files.",
            "it is larger than 2 GB" => "That archive is larger than 2 GB.",
            "no car or track was found in the archive" =>
                "No car or track was found. The archive needs a ui/ui_car.json or ui/ui_track.json.",
            "7-Zip could not read the archive" => "7-Zip could not read the archive.",
            "7-Zip could not extract the archive" => "7-Zip could not extract the archive.",
            "7-Zip is not installed, so these archives cannot be extracted" =>
                "7-Zip is not installed, so these archives cannot be extracted.",
            _ => "The archive could not be installed.",
        };
    }

    private static void TryDelete(string directory)
    {
        try {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, true);
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
    }
}
