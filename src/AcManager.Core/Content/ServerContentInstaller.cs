using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AcManager.Core.Content;

public readonly record struct ServerContentProgress(int Index, int Count, string Id, string Phase);

public interface IServerContentInstaller
{
    Task<ContentInstallResult> InstallAsync(
        IReadOnlyList<ServerContentOffer> offers,
        string gameRoot,
        Action<ServerContentProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// Downloads the 7z archives a server publishes and copies the car or track into the game folder.
/// Custom Shaders Patch materials are kept. The zip installer's shader and surface rewrites do not run here.
/// </summary>
public sealed class ServerContentInstaller : IServerContentInstaller, IDisposable
{
    public const long MaxArchiveBytes = 2L * 1024 * 1024 * 1024;

    private const int MaxEntries = 20000;

    private static readonly byte[] SevenZipSignature = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];

    private readonly HttpClient _http;
    private readonly bool _checkDns;
    private readonly string? _sevenZip;
    private readonly long _maxBytes;

    public ServerContentInstaller()
        : this(CreateHandler(), checkDns: true, sevenZip: null, maxArchiveBytes: MaxArchiveBytes, ownsHandler: true)
    {
    }

    public ServerContentInstaller(HttpMessageHandler handler, string? sevenZip = null, long? maxArchiveBytes = null)
        : this(handler, checkDns: false, sevenZip, maxArchiveBytes ?? MaxArchiveBytes, ownsHandler: false)
    {
    }

    private ServerContentInstaller(HttpMessageHandler handler, bool checkDns, string? sevenZip, long maxArchiveBytes, bool ownsHandler)
    {
        _checkDns = checkDns;
        _sevenZip = sevenZip;
        _maxBytes = maxArchiveBytes;
        _http = new HttpClient(handler, ownsHandler) {
            Timeout = TimeSpan.FromHours(2),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ContentManager");
    }

    /// <summary>
    /// Lists and extracts a 7z into a directory.
    /// The caller copies the files. Shader and surface rewrites do not run here.
    /// A non-null return is why extraction stopped.
    /// </summary>
    public static async Task<string?> ExtractToDirectoryAsync(
        string archivePath,
        string destination,
        CancellationToken cancellationToken = default)
    {
        using var installer = new ServerContentInstaller();
        return await installer.ExtractAsync(archivePath, destination, cancellationToken).ConfigureAwait(false);
    }

    public static bool HasSevenZipSignature(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        var signature = new byte[SevenZipSignature.Length];
        var got = 0;
        while (got < signature.Length) {
            var read = stream.Read(signature, got, signature.Length - got);
            if (read == 0) {
                return false;
            }

            got += read;
        }

        return signature.AsSpan().SequenceEqual(SevenZipSignature);
    }

    public static string? FindSevenZip()
    {
        foreach (var candidate in new[] { "/usr/bin/7z", "/usr/bin/7zz", "/usr/bin/7za" }) {
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
            foreach (var name in new[] { "7z", "7zz", "7za" }) {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }
        }

        return null;
    }

    public async Task<ContentInstallResult> InstallAsync(
        IReadOnlyList<ServerContentOffer> offers,
        string gameRoot,
        Action<ServerContentProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot)) {
            return new ContentInstallResult([], ["The game folder is missing, so content cannot be installed."]);
        }

        if (offers.Count == 0) {
            return new ContentInstallResult([], ["The server did not publish downloads."]);
        }

        if (ResolveTool() == null) {
            return new ContentInstallResult([], ["7-Zip is not installed, so these archives cannot be extracted."]);
        }

        var installed = new List<InstalledPiece>();
        var skipped = new List<string>();
        for (var i = 0; i < offers.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            var offer = offers[i];
            var label = Label(offer.Kind, offer.Id);
            if (!ContentDownloadUrl.TryCreate(offer.Url, out var uri)) {
                skipped.Add(Skipped(label, "it is not a public web address"));
                continue;
            }

            if (Directory.Exists(Destination(gameRoot, offer.Kind, offer.Id))) {
                skipped.Add(label + " already exists.");
                continue;
            }

            progress?.Invoke(new ServerContentProgress(i + 1, offers.Count, offer.Id, "Downloading"));
            var temp = Directory.CreateTempSubdirectory("actools-content-");
            try {
                var archive = Path.Combine(temp.FullName, offer.Id + ".7z");
                var reason = await DownloadAsync(uri, archive, cancellationToken).ConfigureAwait(false);
                if (reason != null) {
                    skipped.Add(Skipped(label, reason));
                    continue;
                }

                progress?.Invoke(new ServerContentProgress(i + 1, offers.Count, offer.Id, "Extracting"));
                var one = await InstallArchiveAsync(archive, gameRoot, offer.Id, offer.Kind, cancellationToken).ConfigureAwait(false);
                installed.AddRange(one.Installed);
                skipped.AddRange(one.Skipped);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException) {
                skipped.Add(Skipped(label, "the download did not finish"));
            } finally {
                TryDelete(temp.FullName);
            }
        }

        return new ContentInstallResult(installed, skipped);
    }

    public Task<ContentInstallResult> InstallLocalAsync(
        string archivePath,
        string gameRoot,
        string id,
        InstalledContentKind kind,
        CancellationToken cancellationToken) =>
        InstallArchiveAsync(archivePath, gameRoot, id, kind, cancellationToken);

    public void Dispose() => _http.Dispose();

    private async Task<ContentInstallResult> InstallArchiveAsync(
        string archivePath,
        string gameRoot,
        string id,
        InstalledContentKind kind,
        CancellationToken cancellationToken)
    {
        var label = Label(kind, id);
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot)) {
            return new ContentInstallResult([], ["The game folder is missing, so content cannot be installed."]);
        }

        if (ResolveTool() == null) {
            return new ContentInstallResult([], ["7-Zip is not installed, so these archives cannot be extracted."]);
        }

        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath)) {
            return new ContentInstallResult([], [Skipped(label, "the archive is missing")]);
        }

        var temp = Directory.CreateTempSubdirectory("actools-extract-");
        try {
            var reason = await ExtractAsync(archivePath, temp.FullName, cancellationToken).ConfigureAwait(false);
            if (reason != null) {
                return new ContentInstallResult([], [Skipped(label, reason)]);
            }

            return InstallExtracted(temp.FullName, gameRoot, id + ".7z", id, kind);
        } finally {
            TryDelete(temp.FullName);
        }
    }

    private async Task<string?> DownloadAsync(Uri start, string destination, CancellationToken cancellationToken)
    {
        var current = start;
        for (var hop = 0; hop < 5; hop++) {
            var resolved = await RefuseResolvedAsync(current, cancellationToken).ConfigureAwait(false);
            if (resolved != null) {
                return resolved;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            if (code is 301 or 302 or 303 or 307 or 308) {
                var location = response.Headers.Location;
                if (location == null) {
                    return "the download was redirected away from the archive";
                }

                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!ContentDownloadUrl.IsAllowed(next.AbsoluteUri)) {
                    return "it is not a public web address";
                }

                current = next;
                continue;
            }

            if (code != 200) {
                return "the server returned HTTP " + code.ToString(CultureInfo.InvariantCulture);
            }

            if (response.Content.Headers.ContentLength is long advertised && advertised > _maxBytes) {
                return "it is larger than 2 GB";
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            var buffer = new byte[81920];
            var signature = new byte[SevenZipSignature.Length];
            var signatureGot = 0;
            long total = 0;
            while (true) {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                total += read;
                if (total > _maxBytes) {
                    return "it is larger than 2 GB";
                }

                if (signatureGot < signature.Length) {
                    var take = Math.Min(read, signature.Length - signatureGot);
                    buffer.AsSpan(0, take).CopyTo(signature.AsSpan(signatureGot));
                    signatureGot += take;
                    if (signatureGot == signature.Length && !signature.AsSpan().SequenceEqual(SevenZipSignature)) {
                        return "the download was not a 7z archive";
                    }
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (total == 0 || signatureGot < signature.Length) {
                return "the download was not a 7z archive";
            }

            return null;
        }

        return "the download was redirected too many times";
    }

    private async Task<string?> RefuseResolvedAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!_checkDns || uri.HostNameType != UriHostNameType.Dns) {
            return null;
        }

        IPAddress[] addresses;
        try {
            addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) when (ex is SocketException or IOException) {
            return "the name " + uri.IdnHost + " did not resolve";
        }

        if (addresses.Length == 0) {
            return "the name " + uri.IdnHost + " did not resolve";
        }

        foreach (var address in addresses) {
            if (!ContentDownloadUrl.IsPublicAddress(address)) {
                return "it is not a public web address";
            }
        }

        return null;
    }

    private async Task<string?> ExtractAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        var tool = ResolveTool();
        if (tool == null) {
            return "7-Zip is not installed, so these archives cannot be extracted";
        }

        var listed = await RunAsync(tool, ["l", "-slt", "-bd", archivePath], cancellationToken).ConfigureAwait(false);
        if (listed.ExitCode != 0) {
            return "7-Zip could not read the archive";
        }

        var problem = RejectListing(listed.Output);
        if (problem != null) {
            return problem;
        }

        Directory.CreateDirectory(destination);
        var extracted = await RunAsync(tool, ["x", "-y", "-bd", "-o" + destination, archivePath], cancellationToken).ConfigureAwait(false);
        if (extracted.ExitCode != 0) {
            return "7-Zip could not extract the archive";
        }

        return null;
    }

    private string? RejectListing(string listing)
    {
        var count = 0;
        long bytes = 0;
        var pastHeader = false;
        string? path = null;
        string? attributes = null;
        long size = 0;
        var sawSize = false;
        var encrypted = false;

        void Flush()
        {
            if (!pastHeader || string.IsNullOrEmpty(path)) {
                path = null;
                attributes = null;
                size = 0;
                sawSize = false;
                encrypted = false;
                return;
            }

            count++;
            if (count > MaxEntries) {
                throw new InvalidDataException("many");
            }

            if (encrypted) {
                throw new InvalidDataException("password");
            }

            if (!IsListedPathSafe(path)) {
                throw new InvalidDataException("escape");
            }

            if (!UnixModeAllows(attributes)) {
                throw new InvalidDataException("link");
            }

            if (sawSize && size > 0) {
                bytes = checked(bytes + size);
            }

            path = null;
            attributes = null;
            size = 0;
            sawSize = false;
            encrypted = false;
        }

        try {
            foreach (var raw in listing.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')) {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("----------", StringComparison.Ordinal)) {
                    pastHeader = true;
                    Flush();
                    continue;
                }

                if (!pastHeader) {
                    continue;
                }

                if (line.Length == 0) {
                    Flush();
                    continue;
                }

                if (line.StartsWith("Path = ", StringComparison.Ordinal)) {
                    path = line["Path = ".Length..];
                } else if (line.StartsWith("Attributes = ", StringComparison.Ordinal)) {
                    attributes = line["Attributes = ".Length..];
                } else if (line.StartsWith("Encrypted = ", StringComparison.Ordinal)) {
                    encrypted = line["Encrypted = ".Length..].Trim() == "+";
                } else if (line.StartsWith("Size = ", StringComparison.Ordinal)
                    && long.TryParse(line["Size = ".Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    && parsed >= 0) {
                    size = parsed;
                    sawSize = true;
                }
            }

            Flush();
        } catch (InvalidDataException ex) when (ex.Message == "password") {
            return "password-protected archives are not supported";
        } catch (InvalidDataException ex) when (ex.Message == "escape") {
            return "an entry escapes the destination";
        } catch (InvalidDataException ex) when (ex.Message == "link") {
            return "an entry is a symbolic link";
        } catch (InvalidDataException ex) when (ex.Message == "many") {
            return "it has too many files";
        } catch (OverflowException) {
            return "it is larger than 2 GB";
        }

        if (count == 0) {
            return "no car or track was found in the archive";
        }

        if (count > MaxEntries || bytes > _maxBytes) {
            return count > MaxEntries ? "it has too many files" : "it is larger than 2 GB";
        }

        return null;
    }

    private ContentInstallResult InstallExtracted(string extractedRoot, string gameRoot, string archiveName, string fallbackId, InstalledContentKind kind)
    {
        var label = Label(kind, fallbackId);
        var names = new List<string>();
        var rejection = Collect(extractedRoot, names);
        if (rejection != null) {
            return new ContentInstallResult([], [Skipped(label, rejection)]);
        }

        var matching = new List<ArchiveEntryPlan>();
        foreach (var plan in ContentArchiveInstaller.Plan(names, archiveName)) {
            if (plan.Kind == kind) {
                matching.Add(plan);
            }
        }

        if (matching.Count == 0) {
            var noun = kind == InstalledContentKind.Car ? "car" : "track";
            return new ContentInstallResult([], [Skipped(label, "no " + noun + " was found in the archive")]);
        }

        var installed = new List<InstalledPiece>();
        var skipped = new List<string>();
        if (matching.Count == 1) {
            CopyPlan(extractedRoot, matching[0], gameRoot, fallbackId, kind, installed, skipped);
        } else {
            foreach (var plan in matching) {
                CopyPlan(extractedRoot, plan, gameRoot, plan.Id, kind, installed, skipped);
            }
        }

        return new ContentInstallResult(installed, skipped);
    }

    private string? Collect(string root, List<string> names)
    {
        long bytes = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0) {
            var directory = pending.Pop();
            IEnumerable<string> entries;
            try {
                entries = Directory.EnumerateFileSystemEntries(directory);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                return "an entry could not be read";
            }

            foreach (var entry in entries) {
                if (names.Count > MaxEntries) {
                    return "it has too many files";
                }

                if (IsReparse(entry)) {
                    return "an entry is a symbolic link";
                }

                var full = Path.GetFullPath(entry);
                if (!IsInside(root, full)) {
                    return "an entry escapes the destination";
                }

                var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
                if (!IsSafeRelative(relative)) {
                    return "an entry escapes the destination";
                }

                FileAttributes attributes;
                try {
                    attributes = File.GetAttributes(entry);
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    return "an entry could not be read";
                }

                if ((attributes & FileAttributes.Directory) != 0) {
                    names.Add(relative + "/");
                    pending.Push(entry);
                    continue;
                }

                long length;
                try {
                    length = new FileInfo(entry).Length;
                } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                    return "an entry could not be read";
                }

                try {
                    bytes = checked(bytes + length);
                } catch (OverflowException) {
                    return "it is larger than 2 GB";
                }

                if (bytes > _maxBytes) {
                    return "it is larger than 2 GB";
                }

                names.Add(relative);
            }
        }

        return null;
    }

    private void CopyPlan(
        string extractedRoot,
        ArchiveEntryPlan plan,
        string gameRoot,
        string id,
        InstalledContentKind kind,
        List<InstalledPiece> installed,
        List<string> skipped)
    {
        var label = Label(kind, id);
        var dest = Destination(gameRoot, kind, id);
        var contentRoot = Path.GetFullPath(Path.Combine(gameRoot, "content"));
        if (!IsInside(contentRoot, dest)) {
            skipped.Add(Skipped(label, "an entry escapes the destination"));
            return;
        }

        if (IsReparse(dest) || IsReparse(Path.GetDirectoryName(dest) ?? dest)) {
            skipped.Add(Skipped(label, "an entry is a symbolic link"));
            return;
        }

        if (Directory.Exists(dest)) {
            skipped.Add(label + " already exists.");
            return;
        }

        var prefix = plan.ArchivePrefix;
        try {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.EnumerateFiles(extractedRoot, "*", SearchOption.AllDirectories)) {
                if (IsReparse(file)) {
                    throw new InvalidDataException("An entry is a symbolic link.");
                }

                var full = Path.GetFullPath(file);
                if (!IsInside(extractedRoot, full)) {
                    throw new InvalidDataException("An entry escapes the destination.");
                }

                var name = Path.GetRelativePath(extractedRoot, full).Replace('\\', '/');
                if (prefix.Length > 0 && !name.StartsWith(prefix, StringComparison.Ordinal)) {
                    continue;
                }

                var tail = prefix.Length == 0 ? name : name[prefix.Length..];
                if (tail.Length == 0 || !IsSafeRelative(tail)) {
                    throw new InvalidDataException("An entry escapes the destination.");
                }

                var target = Path.GetFullPath(Path.Combine(dest, tail.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsInside(dest, target)) {
                    throw new InvalidDataException("An entry escapes the destination.");
                }

                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent)) {
                    Directory.CreateDirectory(parent);
                }

                // CSP cars and tracks keep the shaders and surface values they shipped with.
                File.Copy(full, target, overwrite: false);
            }

            installed.Add(new InstalledPiece(KindName(kind), id, dest));
        } catch (InvalidDataException ex) {
            TryDelete(dest);
            var reason = ex.Message.Contains("symbolic", StringComparison.Ordinal)
                ? "an entry is a symbolic link"
                : "an entry escapes the destination";
            skipped.Add(Skipped(label, reason));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            TryDelete(dest);
            skipped.Add(Skipped(label, "the archive entry could not be copied"));
        }
    }

    private string? ResolveTool()
    {
        if (_sevenZip != null) {
            return File.Exists(_sevenZip) ? _sevenZip : null;
        }

        return FindSevenZip();
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try {
            if (!process.Start()) {
                return (-1, "");
            }
        } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
            return (-1, "");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            try {
                process.Kill(entireProcessTree: true);
            } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) {
            }

            throw;
        }

        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        if (error.Length > 0) {
            output = output + "\n" + error;
        }

        return (process.ExitCode, output);
    }

    private static bool IsListedPathSafe(string path)
    {
        var name = path.Replace('\\', '/').Trim();
        if (name.Length == 0 || name.StartsWith('/') || name.Contains(':', StringComparison.Ordinal)) {
            return false;
        }

        return IsSafeRelative(name.TrimEnd('/'));
    }

    private static bool UnixModeAllows(string? attributes)
    {
        if (string.IsNullOrEmpty(attributes)) {
            return true;
        }

        foreach (var token in attributes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (token.Length < 10 || token[0] is not ('-' or 'd' or 'l' or 'b' or 'c' or 'p' or 's')) {
                continue;
            }

            if (token[0] is not ('-' or 'd')) {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeRelative(string relative)
    {
        if (relative.Length == 0) {
            return false;
        }

        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries)) {
            if (part is "." or "..") {
                return false;
            }

            foreach (var c in part) {
                if (c is '/' or '\\' or ':' or '<' or '>' or '"' or '|' or '?' or '*' || c < ' ') {
                    return false;
                }
            }
        }

        return true;
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

    private static bool IsInside(string root, string candidate)
    {
        var fullRoot = Path.GetFullPath(root);
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(prefix, StringComparison.Ordinal);
    }

    private static string Destination(string gameRoot, InstalledContentKind kind, string id) =>
        Path.GetFullPath(Path.Combine(gameRoot, "content", kind == InstalledContentKind.Car ? "cars" : "tracks", id));

    private static string Label(InstalledContentKind kind, string id) => KindName(kind) + " " + id;

    private static string KindName(InstalledContentKind kind) =>
        kind == InstalledContentKind.Car ? "car" : "track";

    private static string Skipped(string label, string reason) => label + " was skipped because " + reason + ".";

    private static void TryDelete(string directory)
    {
        try {
            if (Directory.Exists(directory)) {
                Directory.Delete(directory, true);
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
        }
    }

    private static SocketsHttpHandler CreateHandler() => new() {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        UseCookies = false,
    };
}
