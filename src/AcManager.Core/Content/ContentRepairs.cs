using System.Globalization;
using AcTools.Core.Data;

namespace AcManager.Core.Content;

/// <summary>
/// A file-only repair. Packed data.acd archives are left unchanged.
/// </summary>
public sealed record ContentRepair(string Kind, string Title, string Detail, string RelativePath);

/// <summary>
/// Suggestions for loose ini files. The checks follow Content Manager's car.ini version
/// and default tyre rules, plus the numeric WAV_PITCH fix the zip installer already uses.
/// </summary>
public static class ContentRepairs
{
    public const string WavPitch = "wav-pitch";

    public const string CarVersion = "car-version";

    public const string TyreIndex = "tyre-index";

    public static IReadOnlyList<ContentRepair> Scan(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot)) {
            return [];
        }

        var found = new List<ContentRepair>();
        ScanCars(gameRoot, found);
        ScanTracks(gameRoot, found);
        return found;
    }

    public static void Apply(string gameRoot, ContentRepair repair)
    {
        var full = Resolve(gameRoot, repair.RelativePath)
            ?? throw new InvalidOperationException("That repair is outside the game folder.");
        if (repair.Kind == WavPitch) {
            ApplySurfaces(gameRoot, full);
            return;
        }

        if (repair.Kind == CarVersion) {
            EnsureLooseCarFile(gameRoot, full, "car.ini");
            ApplyVersion(full);
            return;
        }

        if (repair.Kind == TyreIndex) {
            EnsureLooseCarFile(gameRoot, full, "tyres.ini");
            ApplyTyres(full);
            return;
        }

        throw new InvalidOperationException("Unknown repair.");
    }

    private static void ScanCars(string gameRoot, List<ContentRepair> found)
    {
        var cars = Path.Combine(gameRoot, "content", "cars");
        if (!Directory.Exists(cars)) {
            return;
        }

        IEnumerable<string> directories;
        try {
            directories = Directory.EnumerateDirectories(cars);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return;
        }

        foreach (var car in directories) {
            if (File.Exists(Path.Combine(car, "data.acd"))) {
                continue;
            }

            var data = Path.Combine(car, "data");
            if (!Directory.Exists(data)) {
                continue;
            }

            var carId = Path.GetFileName(car);
            ConsiderVersion(gameRoot, carId, Path.Combine(data, "car.ini"), found);
            ConsiderTyres(gameRoot, carId, Path.Combine(data, "tyres.ini"), found);
        }
    }

    private static void ConsiderVersion(string gameRoot, string carId, string file, List<ContentRepair> found)
    {
        if (!File.Exists(file) || VersionOk(Read(file, "HEADER", "VERSION"))) {
            return;
        }

        found.Add(new ContentRepair(
            CarVersion,
            "Car " + carId + " data version is not a format id",
            Relative(gameRoot, file) + " [HEADER] VERSION must be 1–9, or start with extended-. Apply sets it to 2 and keeps the first .bak.",
            Relative(gameRoot, file)));
    }

    private static void ConsiderTyres(string gameRoot, string carId, string file, List<ContentRepair> found)
    {
        if (!File.Exists(file)) {
            return;
        }

        IniFile ini;
        try {
            ini = IniFile.Load(file);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return;
        }

        var sets = FrontTyreSets(ini);
        if (sets == 0 || IndexOk(Value(ini, "COMPOUND_DEFAULT", "INDEX"), sets)) {
            return;
        }

        found.Add(new ContentRepair(
            TyreIndex,
            "Car " + carId + " default tyre set is out of range",
            Relative(gameRoot, file) + " has " + sets.ToString(CultureInfo.InvariantCulture)
                + " front tyre sets. [COMPOUND_DEFAULT] INDEX is outside that range. Apply sets it to 0 and keeps the first .bak.",
            Relative(gameRoot, file)));
    }

    private static void ScanTracks(string gameRoot, List<ContentRepair> found)
    {
        var tracks = Path.Combine(gameRoot, "content", "tracks");
        if (!Directory.Exists(tracks)) {
            return;
        }

        IEnumerable<string> files;
        try {
            files = Directory.EnumerateFiles(tracks, "surfaces.ini", SearchOption.AllDirectories);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return;
        }

        foreach (var file in files) {
            var parent = Path.GetFileName(Path.GetDirectoryName(file));
            if (!string.Equals(parent, "data", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            string text;
            try {
                text = File.ReadAllText(file);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                continue;
            }

            TrackSurfaces.Sanitize(text, out var changed);
            if (changed == 0) {
                continue;
            }

            var relative = Relative(gameRoot, file);
            found.Add(new ContentRepair(
                WavPitch,
                "Track surface pitch is not a number",
                relative + " has a WAV_PITCH value the original game cannot read. Custom Shaders Patch accepts values such as extended-0. Apply writes 0 and keeps the first .bak.",
                relative));
        }
    }

    private static void ApplySurfaces(string gameRoot, string file)
    {
        var tracks = Path.Combine(gameRoot, "content", "tracks");
        if (!IsUnder(file, tracks)
            || !string.Equals(Path.GetFileName(file), "surfaces.ini", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "data", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("That repair is not a track surfaces.ini.");
        }

        var text = File.ReadAllText(file);
        var updated = TrackSurfaces.Sanitize(text, out var changed);
        if (changed == 0) {
            throw new InvalidOperationException("This surfaces.ini no longer needs a repair.");
        }

        Backup(file);
        File.WriteAllText(file, updated);
    }

    private static void ApplyVersion(string file)
    {
        var text = File.ReadAllText(file);
        if (VersionOk(Value(IniFile.Parse(text), "HEADER", "VERSION"))) {
            throw new InvalidOperationException("This car.ini no longer needs a repair.");
        }

        Backup(file);
        File.WriteAllText(file, IniText.SetValues(text, "HEADER", [new KeyValuePair<string, string>("VERSION", "2")]));
    }

    private static void ApplyTyres(string file)
    {
        var text = File.ReadAllText(file);
        var ini = IniFile.Parse(text);
        var sets = FrontTyreSets(ini);
        if (sets > 0 && IndexOk(Value(ini, "COMPOUND_DEFAULT", "INDEX"), sets)) {
            throw new InvalidOperationException("This tyres.ini no longer needs a repair.");
        }

        if (sets == 0) {
            throw new InvalidOperationException("This tyres.ini has no front tyre sets to select.");
        }

        Backup(file);
        File.WriteAllText(file, IniText.SetValues(text, "COMPOUND_DEFAULT", [new KeyValuePair<string, string>("INDEX", "0")]));
    }

    private static void EnsureLooseCarFile(string gameRoot, string file, string fileName)
    {
        var cars = Path.Combine(gameRoot, "content", "cars");
        var data = Path.GetDirectoryName(file);
        var car = data == null ? null : Path.GetDirectoryName(data);
        if (car == null
            || !IsUnder(file, cars)
            || !string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetFileName(data), "data", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("That repair is not a loose car data file.");
        }

        if (File.Exists(Path.Combine(car, "data.acd"))) {
            throw new InvalidOperationException("This car keeps its data in data.acd, so the loose file was left unchanged.");
        }
    }

    private static bool VersionOk(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) {
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith("extended-", StringComparison.Ordinal)) {
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            && version > 0
            && version < 10;
    }

    private static bool IndexOk(string? raw, int sets)
    {
        if (string.IsNullOrWhiteSpace(raw)) {
            return 0 < sets;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0
            && index < sets;
    }

    private static int FrontTyreSets(IniFile ini)
    {
        var count = 0;
        while (count < 64) {
            var name = count == 0 ? "FRONT" : "FRONT_" + count.ToString(CultureInfo.InvariantCulture);
            if (!HasSection(ini, name)) {
                return count;
            }

            count++;
        }

        return count;
    }

    private static bool HasSection(IniFile ini, string name)
    {
        foreach (var section in ini.SectionNames) {
            if (section.Equals(name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string? Read(string file, string section, string key)
    {
        try {
            return Value(IniFile.Load(file), section, key);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return null;
        }
    }

    private static string? Value(IniFile ini, string section, string key)
    {
        foreach (var name in ini.SectionNames) {
            if (!name.Equals(section, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            foreach (var pair in ini[name].Pairs) {
                if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) {
                    return pair.Value;
                }
            }
        }

        return null;
    }

    private static void Backup(string file)
    {
        var backup = file + ".bak";
        if (!File.Exists(backup)) {
            File.Copy(file, backup);
        }
    }

    private static string Relative(string gameRoot, string file) =>
        Path.GetRelativePath(gameRoot, file);

    private static string? Resolve(string gameRoot, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) {
            return null;
        }

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts) {
            if (part.Length == 0 || part == "." || part == "..") {
                return null;
            }
        }

        var root = Path.GetFullPath(gameRoot);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return IsUnder(full, root) ? full : null;
    }

    private static bool IsUnder(string full, string parent)
    {
        var root = Path.GetFullPath(parent);
        var path = Path.GetFullPath(full);
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
