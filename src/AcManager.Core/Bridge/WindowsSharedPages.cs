using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace AcManager.Core.Bridge;

public interface ISharedPages
{
    bool TryCopy(byte kind, Span<byte> destination);
}

public interface IMappedPage : IDisposable
{
    bool TryCopy(Span<byte> destination);
}

/// <summary>
/// Opens the three Assetto Corsa mappings. A missing mapping is tried again on the next read.
/// </summary>
public sealed class WindowsSharedPages : ISharedPages, IDisposable
{
    public const string PhysicsName = @"Local\acpmf_physics";

    public const string GraphicsName = @"Local\acpmf_graphics";

    public const string StaticName = @"Local\acpmf_static";

    private readonly Func<string, IMappedPage?> _open;

    private readonly Dictionary<string, IMappedPage> _openMaps = new(StringComparer.Ordinal);

    [SupportedOSPlatform("windows")]
    public WindowsSharedPages()
        : this(OpenOnWindows)
    {
    }

    [SupportedOSPlatform("windows")]
    private static IMappedPage OpenOnWindows(string name) => new MappedFilePage(name);

    public WindowsSharedPages(Func<string, IMappedPage?> open)
    {
        _open = open;
    }

    public static string? Name(byte kind) => kind switch {
        SessionBridgeProtocol.Physics => PhysicsName,
        SessionBridgeProtocol.Graphics => GraphicsName,
        SessionBridgeProtocol.Static => StaticName,
        _ => null,
    };

    public bool TryCopy(byte kind, Span<byte> destination)
    {
        var name = Name(kind);
        if (name == null) {
            return false;
        }

        if (!_openMaps.TryGetValue(name, out var mapped)) {
            try {
                mapped = _open(name);
            } catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException or PlatformNotSupportedException) {
                return false;
            }

            if (mapped == null) {
                return false;
            }

            _openMaps[name] = mapped;
        }

        return mapped.TryCopy(destination);
    }

    public void Dispose()
    {
        foreach (var mapped in _openMaps.Values) {
            mapped.Dispose();
        }

        _openMaps.Clear();
    }

    private sealed class MappedFilePage : IMappedPage
    {
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _view;

        [SupportedOSPlatform("windows")]
        public MappedFilePage(string name)
        {
            _file = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            _view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        public bool TryCopy(Span<byte> destination)
        {
            if (destination.Length == 0 || _view.Capacity < destination.Length) {
                return false;
            }

            var rented = ArrayPool<byte>.Shared.Rent(destination.Length);
            try {
                var count = _view.ReadArray(0, rented, 0, destination.Length);
                if (count != destination.Length) {
                    return false;
                }

                rented.AsSpan(0, destination.Length).CopyTo(destination);
                return true;
            } finally {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        public void Dispose()
        {
            _view.Dispose();
            _file.Dispose();
        }
    }
}
