using System.Runtime.InteropServices;
using AcTools.Platform;

namespace AcTools.Platform.Linux;

/// <summary>
/// Lists joysticks through libSDL2. The names are not written into controls.ini.
/// </summary>
public sealed class SdlInputDevices : IInputDevices
{
    private const uint Joystick = 0x00000200;

    private static readonly object Gate = new();

    public InputDeviceList List()
    {
        lock (Gate) {
            try {
                SdlNative.SDL_SetHint("SDL_NO_SIGNAL_HANDLERS", "1");
                if (SdlNative.SDL_InitSubSystem(Joystick) != 0) {
                    return new InputDeviceList([], "SDL2 could not start joystick support, so connected devices are not listed.");
                }

                try {
                    var count = SdlNative.SDL_NumJoysticks();
                    if (count < 0) {
                        return new InputDeviceList([], "SDL2 could not list joysticks.");
                    }

                    var devices = new List<InputDevice>(count);
                    for (var i = 0; i < count; i++) {
                        var namePointer = SdlNative.SDL_JoystickNameForIndex(i);
                        var name = namePointer == 0
                            ? "Joystick " + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : Marshal.PtrToStringUTF8(namePointer) ?? "";
                        if (string.IsNullOrWhiteSpace(name)) {
                            name = "Joystick " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }

                        var bytes = SdlNative.SDL_JoystickGetDeviceGUID(i).Bytes();
                        int? vendor = null;
                        int? product = null;
                        if (SdlGuid.TryUsb(bytes, out var vendorId, out var productId)) {
                            vendor = vendorId;
                            product = productId;
                        }

                        devices.Add(new InputDevice(name.Trim(), SdlGuid.Hex(bytes), vendor, product));
                    }

                    return new InputDeviceList(devices, null);
                } finally {
                    SdlNative.SDL_QuitSubSystem(Joystick);
                }
            } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) {
                return new InputDeviceList([], "SDL2 is not installed, so connected devices are not listed.");
            }
        }
    }
}

public static class SdlGuid
{
    public static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>
    /// Reads vendor and product from an SDL joystick GUID.
    /// SDL 2.0.14 and later store a name checksum in bytes 2 and 3. The ids stay at bytes 4 and 8.
    /// Bus 0 and bus 0xFF are virtual devices and are left unnamed.
    /// </summary>
    public static bool TryUsb(ReadOnlySpan<byte> bytes, out int vendor, out int product)
    {
        vendor = 0;
        product = 0;
        if (bytes.Length < 16) {
            return false;
        }

        var bus = bytes[0] | (bytes[1] << 8);
        var standard = bus is > 0 and < 0x20
            && bytes[6] == 0 && bytes[7] == 0
            && bytes[10] == 0 && bytes[11] == 0;
        if (!standard) {
            return false;
        }

        vendor = bytes[4] | (bytes[5] << 8);
        product = bytes[8] | (bytes[9] << 8);
        return vendor != 0 || product != 0;
    }
}

internal static partial class SdlNative
{
    private const string Library = "libSDL2-2.0.so.0";

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SDL_SetHint(string name, string value);

    [LibraryImport(Library)]
    internal static partial int SDL_InitSubSystem(uint flags);

    [LibraryImport(Library)]
    internal static partial void SDL_QuitSubSystem(uint flags);

    [LibraryImport(Library)]
    internal static partial int SDL_NumJoysticks();

    [LibraryImport(Library)]
    internal static partial nint SDL_JoystickNameForIndex(int deviceIndex);

    [LibraryImport(Library)]
    internal static partial JoystickGuid SDL_JoystickGetDeviceGUID(int deviceIndex);

    [StructLayout(LayoutKind.Sequential)]
    internal struct JoystickGuid
    {
        public byte B0;
        public byte B1;
        public byte B2;
        public byte B3;
        public byte B4;
        public byte B5;
        public byte B6;
        public byte B7;
        public byte B8;
        public byte B9;
        public byte B10;
        public byte B11;
        public byte B12;
        public byte B13;
        public byte B14;
        public byte B15;

        public byte[] Bytes() => [B0, B1, B2, B3, B4, B5, B6, B7, B8, B9, B10, B11, B12, B13, B14, B15];
    }
}
