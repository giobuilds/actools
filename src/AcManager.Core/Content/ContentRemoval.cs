using System.Text;
using System.Text.Json;

namespace AcManager.Core.Content;

public sealed record RemovablePiece(string Kind, string Id, string Name)
{
    public string Subtitle => Id;
}

public sealed record RemovableLibrary(
    IReadOnlyList<RemovablePiece> Cars,
    IReadOnlyList<RemovablePiece> Tracks,
    bool OfficialCarsKnown);

/// <summary>
/// Lists cars and tracks added on top of the original game, and deletes one folder.
/// Official cars are the ids named in content/sfx/GUIDs.txt. Official tracks carry the "original" tag.
/// </summary>
public static class ContentRemoval
{
    public static RemovableLibrary List(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot)) {
            return new RemovableLibrary([], [], false);
        }

        return new RemovableLibrary(ListCars(gameRoot), ListTracks(gameRoot), OfficialCarIds(gameRoot) != null);
    }

    public static void Delete(string gameRoot, string kind, string id)
    {
        if (kind is not ("car" or "track")) {
            throw new ArgumentException("Choose a car or a track.", nameof(kind));
        }

        if (!IsSafeId(id)) {
            throw new ArgumentException("That folder name cannot be removed.", nameof(id));
        }

        var library = List(gameRoot);
        var pieces = kind == "car" ? library.Cars : library.Tracks;
        var piece = pieces.FirstOrDefault(item => item.Id == id);
        if (piece == null) {
            throw new InvalidOperationException("That folder is part of the original game, or it is no longer there.");
        }

        var parent = ContentParent(gameRoot, kind);
        var destination = Path.GetFullPath(Path.Combine(parent, piece.Id));
        if (!IsInside(parent, destination)) {
            throw new InvalidOperationException("That folder is outside the game content directory.");
        }

        if (!Directory.Exists(destination)) {
            throw new DirectoryNotFoundException("That folder is no longer there.");
        }

        if (IsLink(destination)) {
            throw new IOException("That folder is a link, so it was left in place.");
        }

        Directory.Delete(destination, recursive: true);
    }

    private static IReadOnlyList<RemovablePiece> ListCars(string gameRoot)
    {
        var official = OfficialCarIds(gameRoot);
        if (official == null) {
            return [];
        }

        var cars = new List<RemovablePiece>();
        foreach (var car in ContentLibrary.ScanCars(Path.Combine(gameRoot, "content", "cars"))) {
            if (official.Contains(car.Id) || !IsSafeId(car.Id)) {
                continue;
            }

            var directory = Path.Combine(gameRoot, "content", "cars", car.Id);
            if (IsLink(directory)) {
                continue;
            }

            cars.Add(new RemovablePiece("car", car.Id, car.Name));
        }

        return cars;
    }

    private static IReadOnlyList<RemovablePiece> ListTracks(string gameRoot)
    {
        var directory = Path.Combine(gameRoot, "content", "tracks");
        var names = ContentLibrary.ScanTracks(directory).ToDictionary(track => track.Id, track => track.Name, StringComparer.Ordinal);
        if (!Directory.Exists(directory)) {
            return [];
        }

        var tracks = new List<RemovablePiece>();
        foreach (var trackDirectory in Directory.GetDirectories(directory)) {
            var id = Path.GetFileName(trackDirectory);
            if (!IsSafeId(id) || IsLink(trackDirectory) || !names.TryGetValue(id, out var name)) {
                continue;
            }

            if (TrackKind(trackDirectory) != TrackInstallKind.Added) {
                continue;
            }

            tracks.Add(new RemovablePiece("track", id, name));
        }

        tracks.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return tracks;
    }

    private enum TrackInstallKind
    {
        Unknown,
        Official,
        Added,
    }

    private static TrackInstallKind TrackKind(string trackDirectory)
    {
        var sawAdded = false;
        foreach (var json in TrackUiFiles(trackDirectory)) {
            if (!TryReadUi(json, out var official)) {
                continue;
            }

            if (official) {
                return TrackInstallKind.Official;
            }

            sawAdded = true;
        }

        return sawAdded ? TrackInstallKind.Added : TrackInstallKind.Unknown;
    }

    private static IEnumerable<string> TrackUiFiles(string trackDirectory)
    {
        var ui = Path.Combine(trackDirectory, "ui");
        if (!Directory.Exists(ui)) {
            yield break;
        }

        var main = Path.Combine(ui, "ui_track.json");
        if (File.Exists(main)) {
            yield return main;
        }

        foreach (var layout in Directory.GetDirectories(ui)) {
            var json = Path.Combine(layout, "ui_track.json");
            if (File.Exists(json)) {
                yield return json;
            }
        }
    }

    private static bool TryReadUi(string path, out bool official)
    {
        official = false;
        string text;
        try {
            text = File.ReadAllText(path);
        } catch (IOException) {
            return false;
        }

        try {
            using var document = JsonDocument.Parse(BareNewlines(ContentLibrary.Relax(text)), new JsonDocumentOptions {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return false;
            }

            official = IsOfficialTrack(document.RootElement);
            return true;
        } catch (JsonException) {
            // A mod with broken JSON is still added content. Original tracks parse.
            official = false;
            return true;
        }
    }

    private static string BareNewlines(string text)
    {
        var builder = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (c == '"' && !IsEscaped(text, i)) {
                inString = !inString;
            }

            if (inString && c is '\n' or '\r') {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') {
                    i++;
                }

                builder.Append(' ');
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool IsEscaped(string text, int index)
    {
        var slashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--) {
            slashes++;
        }

        return slashes % 2 == 1;
    }

    private static bool IsOfficialTrack(JsonElement root)
    {
        if (root.TryGetProperty("author", out var authorElement)
            && authorElement.ValueKind == JsonValueKind.String) {
            var author = authorElement.GetString();
            if (!string.IsNullOrWhiteSpace(author) && !author.Equals("Kunos", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }

        if (!root.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array) {
            return false;
        }

        foreach (var tag in tags.EnumerateArray()) {
            if (tag.ValueKind == JsonValueKind.String
                && string.Equals(tag.GetString(), "original", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string>? OfficialCarIds(string gameRoot)
    {
        var path = Path.Combine(gameRoot, "content", "sfx", "GUIDs.txt");
        if (!File.Exists(path)) {
            return null;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path)) {
            var parts = line.Split('/');
            if (parts.Length > 3 && parts[1] == "cars" && parts[0].EndsWith("event:", StringComparison.Ordinal)) {
                ids.Add(parts[2]);
            }
        }

        return ids.Count == 0 ? null : ids;
    }

    private static string ContentParent(string gameRoot, string kind)
    {
        var folder = kind == "car" ? "cars" : "tracks";
        return Path.GetFullPath(Path.Combine(gameRoot, "content", folder));
    }

    private static bool IsSafeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id is "." or "..") {
            return false;
        }

        if (id.IndexOfAny(['/', '\\', ':']) >= 0) {
            return false;
        }

        return id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static bool IsInside(string root, string candidate)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static bool IsLink(string directory)
    {
        if (!Directory.Exists(directory)) {
            return false;
        }

        return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
    }
}
